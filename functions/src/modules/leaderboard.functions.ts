import {onCall, HttpsError} from "firebase-functions/v2/https";
import {onDocumentWritten} from "firebase-functions/v2/firestore";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {DB_ID} from "../utils/constants";
import {getActiveSeasonId} from "../utils/helpers";



// -------- syncLeaderboard (Trigger) --------
export const syncLeaderboard = onDocumentWritten(
    {document: "users/{uid}", database: DB_ID},
    async (event) => {
        const uid = event.params.uid;
        const after = event.data?.after?.data();
        const before = event.data?.before?.data();

        if (!after) return;

        // Optimization: Only run if crucial fields changed
        const score = Number(after.maxScore || 0);
        const oldScore = Number(before?.maxScore || 0);

        const username = (typeof after.username === "string" ? after.username : "").trim() || "Guest";
        const photoUrl = (typeof after.photoUrl === "string" ? after.photoUrl : "");
        const elitePassExpiresAt = (after.elitePassExpiresAt as Timestamp | null) || null;

        // Always update All-Time Leaderboard
        const allTimeRef = db.collection("leaderboards").doc("all_time").collection("entries");

        // 1. Calculate Rank EXACTLY (Count Aggregation) - only if score changed
        if (score !== oldScore) {
            try {
                // Count how many people have MORE score than this user
                const countSnap = await allTimeRef.where("score", ">", score).count().get();
                const rank = countSnap.data().count + 1;

                // Update User Profile with new Rank
                await db.collection("users").doc(uid).update({rank});
            } catch (e) {
                console.error(`[syncLeaderboard] Rank calc failed for ${uid}:`, e);
            }
        }

        // 2. Update All-Time Entry
        await allTimeRef.doc(uid).set({
            username,
            score,
            photoUrl,
            elitePassExpiresAt,
            updatedAt: FieldValue.serverTimestamp(),
        }, {merge: true});

        // 3. Season Logic
        const seasonId = await getActiveSeasonId();
        if (seasonId) {
            // Check for seasonal scores map in user doc
            const seasonalScores = after.seasonalMaxScores || {};
            const currentSeasonScore = seasonalScores[seasonId];

            // Only update if we have a valid score for this season
            if (typeof currentSeasonScore === "number") {
                await db.collection("leaderboards").doc(seasonId).collection("entries").doc(uid).set({
                    username,
                    score: currentSeasonScore, // Use the season specific score
                    photoUrl,
                    elitePassExpiresAt,
                    updatedAt: FieldValue.serverTimestamp(),
                }, {merge: true});
            }
        }
    }
);

// -------- getLeaderboardsSnapshot (Callable) --------
export const getLeaderboardsSnapshot = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const now = Timestamp.now();

    // params
    const leaderboardId = (req.data?.leaderboardId as string) || "all_time";
    const limitIn = Number(req.data?.limit ?? 100);
    const limit = Math.max(1, Math.min(limitIn, 100));

    const coll = db.collection("leaderboards").doc(leaderboardId).collection("entries");
    let q = coll.orderBy("score", "desc").limit(limit);

    const startAfterScoreRaw = req.data?.startAfterScore;
    if (startAfterScoreRaw !== undefined && startAfterScoreRaw !== null) {
        const startAfterScore = Number(startAfterScoreRaw);
        if (!isNaN(startAfterScore)) {
            q = q.startAfter(startAfterScore);
        }
    }

    const snap = await q.get();
    const items = snap.docs.map((d) => {
        const da = d.data();
        const epExp = (da.elitePassExpiresAt as Timestamp | undefined) || null;
        const isElite = !!epExp && epExp.toMillis() > now.toMillis();
        return {
            uid: d.id,
            username: da.username,
            score: da.score,
            photoUrl: da.photoUrl,
            isElite,
            rank: 0, // Client calculates rank 1-100 logic by index
        };
    });

    // Handle "Me" entry
    let selfEntry: any = null;
    if (req.data?.includeSelf) {
        // 1. Try to find myself in the fetched list
        const myItem = items.find((i) => i.uid === uid);
        if (myItem) {
            selfEntry = {...myItem};
            selfEntry.rank = items.indexOf(myItem) + 1;
        } else {
            // 2. Fetched separately
            const entrySnap = await coll.doc(uid).get();
            if (entrySnap.exists) {
                const da = entrySnap.data() || {};
                const epExp = (da.elitePassExpiresAt as Timestamp | undefined) || null;
                const isElite = !!epExp && epExp.toMillis() > now.toMillis();

                let myRank = 0;

                if (leaderboardId === "all_time") {
                    // For all_time, we trust users/{uid}.rank updated by Trigger
                    const uSnap = await db.collection("users").doc(uid).get();
                    myRank = Number(uSnap.get("rank") ?? 0) || 0;
                } else {
                    // For Season, calculate rank on fly: count(score > myScore)
                    const myScore = Number(da.score ?? 0);
                    if (myScore > 0) {
                        const cSnap = await coll.where("score", ">", myScore).count().get();
                        myRank = cSnap.data().count + 1;
                    }
                }

                selfEntry = {
                    uid,
                    username: da.username,
                    score: da.score,
                    photoUrl: da.photoUrl,
                    isElite,
                    rank: myRank,
                };
            } else {
                // Not in leaderboard at all
                const uSnap = await db.collection("users").doc(uid).get();
                const uData = uSnap.data() || {};
                selfEntry = {
                    uid,
                    username: uData.username || "Guest",
                    score: 0,
                    photoUrl: uData.photoUrl,
                    isElite: false,
                    rank: 0,
                };
            }
        }
    }

    return {ok: true, items, selfEntry};
});
