import {onCall, HttpsError} from "firebase-functions/v2/https";
import {onDocumentWritten} from "firebase-functions/v2/firestore";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {DB_ID, SEASON, RANK_BATCH, RANK_LOCK_DOC} from "../utils/constants";

// -------- Helper: Rank Locking --------
async function acquireRankLock(dbNow: Timestamp, holdMs = 2 * 60 * 1000): Promise<boolean> {
    const ref = db.doc(RANK_LOCK_DOC);
    try {
        await db.runTransaction(async (tx) => {
            const snap = await tx.get(ref);
            const until = snap.exists ? (snap.get("lockedUntil") as Timestamp | null) : null;
            const unlocked = !until || until.toMillis() <= dbNow.toMillis();
            if (!unlocked) {
                throw new Error("locked");
            }
            tx.set(ref, {
                lockedUntil: Timestamp.fromMillis(dbNow.toMillis() + holdMs),
                updatedAt: FieldValue.serverTimestamp()
            }, {merge: true});
        });
        return true;
    } catch (e) {
        return false;
    }
}

async function releaseRankLock() {
    const ref = db.doc(RANK_LOCK_DOC);
    try {
        await ref.set({
            lockedUntil: Timestamp.fromMillis(0),
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});
    } catch { }
}

// -------- Helper: Recompute All Ranks --------
async function recomputeAllRanks(): Promise<{ count: number }> {
    const now = Timestamp.now();
    const got = await acquireRankLock(now);
    if (!got) {
        console.log("[ranks] another job is running; skip");
        return {count: 0};
    }

    let ranked = 0;
    try {
        // page through all users ordered by maxScore desc
        let lastScore: number | null = null;
        // Note: Firestore pagination with a single orderBy avoids composite index
        while (true) {
            let q = db.collection("users").orderBy("maxScore", "desc").limit(RANK_BATCH);
            if (lastScore !== null) {
                q = q.startAfter(lastScore);
            }
            const snap = await q.get();
            if (snap.empty) break;

            const batch = db.batch();
            snap.docs.forEach((doc, i) => {
                const rank = ranked + i + 1; // 1-based
                batch.set(doc.ref, {rank, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
            });
            await batch.commit();

            ranked += snap.size;
            const last = snap.docs[snap.docs.length - 1];
            lastScore = Number(last.get("maxScore") ?? 0) || 0;

            // extend lock while we are still working
            await db.doc(RANK_LOCK_DOC).set({
                lockedUntil: Timestamp.fromMillis(Timestamp.now().toMillis() + 2 * 60 * 1000),
                updatedAt: FieldValue.serverTimestamp()
            }, {merge: true});
        }

        console.log(`[ranks] recomputed for ${ranked} users`);
        return {count: ranked};
    } finally {
        await releaseRankLock();
    }
}

// -------- syncLeaderboard (Trigger) --------
export const syncLeaderboard = onDocumentWritten(
    {document: "users/{uid}", database: DB_ID},
    async (event) => {
        const uid = event.params.uid;
        const after = event.data?.after?.data();
        if (!after) return;

        const username =
            (typeof after.username === "string" ? after.username : "").trim()
            || "Guest";

        const rawMaxScore = after.maxScore;
        const score = typeof rawMaxScore === "number" ? rawMaxScore : 0;

        const seasonRef = db.collection("leaderboards").doc(SEASON);

        // 1) public entry
        const elitePassExpiresAt = (after.elitePassExpiresAt as Timestamp | null) || null;
        await seasonRef.collection("entries").doc(uid).set(
            {
                username,
                score,
                elitePassExpiresAt, // carry expiry to leaderboard entry for client-side active check
                updatedAt: FieldValue.serverTimestamp(),
            },
            {merge: true}
        );
        // NOTE: Removed materialization of topN and background rank recomputation.
    }
);

// -------- getLeaderboardsSnapshot (Callable) --------
export const getLeaderboardsSnapshot = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const now = Timestamp.now();

    // params
    const limitIn = Number(req.data?.limit ?? 100);
    const startAfterScoreRaw = req.data?.startAfterScore;
    const includeSelf = !!req.data?.includeSelf; // if true, echo caller's current entry

    const limit = Math.max(1, Math.min(limitIn, 500));
    const seasonRef = db.collection("leaderboards").doc(SEASON);
    let q = seasonRef.collection("entries").orderBy("score", "desc").limit(limit);

    if (startAfterScoreRaw !== undefined && startAfterScoreRaw !== null) {
        const startAfterScore = Number(startAfterScoreRaw);
        if (!isNaN(startAfterScore)) {
            q = q.startAfter(startAfterScore);
        }
    }

    const snap = await q.get();
    const items = snap.docs.map((d) => {
        const da = d.data();
        // Check elite status on the fly based on expiry
        const epExp = (da.elitePassExpiresAt as Timestamp | undefined) || null;
        const isElite = !!epExp && epExp.toMillis() > now.toMillis();
        return {
            uid: d.id,
            username: da.username,
            score: da.score,
            isElite,
            rank: 0, // client calculates visual rank or we can store it computed
        };
    });

    let selfEntry: any = null;
    if (includeSelf) {
        const sSnap = await seasonRef.collection("entries").doc(uid).get();
        if (sSnap.exists) {
            const da = sSnap.data() || {};
            const epExp = (da.elitePassExpiresAt as Timestamp | undefined) || null;
            const isElite = !!epExp && epExp.toMillis() > now.toMillis();
            // Need precise rank?
            // If we are strictly score-based, rank = count(score > myScore) + 1. Expensive to count real-time.
            // We can use the 'rank' field on user doc if recomputeAllRanks runs periodically.
            const uSnap = await db.collection("users").doc(uid).get();
            const rank = Number(uSnap.get("rank") ?? 0) || 0;

            selfEntry = {
                uid,
                username: da.username,
                score: da.score,
                isElite,
                rank,
            };
        }
    }

    return {ok: true, items, selfEntry};
});

// -------- recomputeRanks (Callable) --------
export const recomputeRanks = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const res = await recomputeAllRanks();
    return {ok: true, updated: res.count};
});
