import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {AchType, AchLevel, AchDoc} from "../utils/constants"; // Removed ACH_TYPES

// ========================= Helpers =========================

export const achDefRef = (typeId: AchType) => db.collection("appdata").doc("achievements").collection("types").doc(typeId);
export const achUserRef = (uid: string, typeId: AchType) => db.collection("users").doc(uid).collection("achievements").doc(typeId);

export async function readAchDef(typeId: AchType): Promise<AchDoc> {
    const snap = await achDefRef(typeId).get();
    if (!snap.exists) throw new HttpsError("not-found", `Achievement def missing: ${typeId}`);
    const d = snap.data() || {};
    const levels = Array.isArray(d.levels) ? d.levels : [];
    const norm: AchLevel[] = levels.map((x: any) => ({
        threshold: Number(x?.threshold ?? 0) || 0,
        rewardGet: Number(x?.rewardGet ?? 0) || 0,
    }));
    return {
        levels: norm.slice(0, 5),
        displayName: typeof d.displayName === "string" ? d.displayName : undefined,
        description: typeof d.description === "string" ? d.description : undefined,
        iconUrl: typeof d.iconUrl === "string" ? d.iconUrl : undefined,
        order: Number(d.order ?? 0) || 0,
    };
}

export function computeLevel(progress: number, levels: AchLevel[]): number {
    let lvl = 0;
    for (let i = 0; i < levels.length; i++) {
        if (progress >= levels[i].threshold) lvl = i + 1; else break;
    }
    return lvl; // 0..5
}

export async function upsertUserAch(uid: string, typeId: AchType, progress: number) {
    const def = await readAchDef(typeId);
    const level = computeLevel(progress, def.levels);
    const nextThreshold = level < def.levels.length ? def.levels[level].threshold : null;
    const ref = achUserRef(uid, typeId);
    await ref.set({
        progress,
        level,
        nextThreshold,
        updatedAt: FieldValue.serverTimestamp(),
    }, {merge: true});
}

export async function grantAchReward(uid: string, typeId: AchType, level: number) {
    const def = await readAchDef(typeId);
    if (level < 1 || level > def.levels.length) throw new HttpsError("invalid-argument", "Invalid level");
    const reward = def.levels[level - 1].rewardGet;
    const uRef = db.collection("users").doc(uid);
    const aRef = achUserRef(uid, typeId);

    return await db.runTransaction(async (tx) => {
        const [uSnap, aSnap] = await Promise.all([tx.get(uRef), tx.get(aRef)]);
        if (!aSnap.exists) throw new HttpsError("failed-precondition", "Achievement progress missing");
        const curLevel = Number(aSnap.get("level") ?? 0) || 0;
        if (curLevel < level) throw new HttpsError("failed-precondition", "Level not reached");
        const claimed: number[] = Array.isArray(aSnap.get("claimedLevels")) ? aSnap.get("claimedLevels") : [];
        if (claimed.includes(level)) throw new HttpsError("already-exists", "Already claimed");

        const curCurrency = Number(uSnap.get("currency") ?? 0) || 0;
        tx.set(uRef, {currency: curCurrency + reward, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        tx.set(aRef, {claimedLevels: FieldValue.arrayUnion(level), lastClaimedAt: Timestamp.now()}, {merge: true});
        return {reward, newCurrency: curCurrency + reward};
    });
}

// ========================= Exports =========================

// -------- getAchievementsSnapshot (callable) --------
export const getAchievementsSnapshot = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    // 1) Load ALL achievement type docs (dynamic; no hardcoded ids)
    const typesCol = db.collection("appdata").doc("achievements").collection("types");
    const typeSnap = await typesCol.get();

    type DefPayload = {
        typeId: string;
        displayName: string;
        description: string;
        iconUrl: string;
        order: number;
        maxLevel: number;
        thresholds: number[];
        rewards: number[];
    };

    const defs: DefPayload[] = [];

    for (const doc of typeSnap.docs) {
        const id = doc.id as string;
        const raw = (doc.data() || {}) as Record<string, any>;
        const levelsArr = Array.isArray(raw.levels) ? raw.levels : [];
        const levels: AchLevel[] = levelsArr.map((x: any) => ({
            threshold: Number(x?.threshold ?? 0) || 0,
            rewardGet: Number(x?.rewardGet ?? 0) || 0,
        }));

        const thresholds = levels.map(l => l.threshold);
        const rewards = levels.map(l => l.rewardGet);

        defs.push({
            typeId: id,
            displayName: typeof raw.displayName === "string" ? raw.displayName : id,
            description: typeof raw.description === "string" ? raw.description : "",
            iconUrl: typeof raw.iconUrl === "string" ? raw.iconUrl : "",
            order: Number(raw.order ?? 0) || 0,
            maxLevel: levels.length,
            thresholds,
            rewards,
        });
    }

    // Order by explicit order, then typeId for stability
    defs.sort((a, b) => (a.order - b.order) || a.typeId.localeCompare(b.typeId));

    // 2) Load user states for these types
    const aCol = db.collection("users").doc(uid).collection("achievements");
    const stateSnaps = await Promise.all(defs.map(d => aCol.doc(d.typeId).get()));

    type StatePayload =
        { typeId: string; progress: number; level: number; claimedLevels: number[]; nextThreshold: number | null };
    const states: StatePayload[] = defs.map((d, i) => {
        const s = stateSnaps[i];
        const progress = s.exists ? Number(s.get("progress") ?? 0) || 0 : 0;
        const level = s.exists ? Number(s.get("level") ?? 0) || 0 : 0;
        const claimed = s.exists && Array.isArray(s.get("claimedLevels")) ? (s.get("claimedLevels") as number[]) : [];
        const nextThreshold = s.exists ? ((s.get("nextThreshold") ?? null) as number | null) : (d.thresholds[level] ?? null);
        return {typeId: d.typeId, progress, level, claimedLevels: claimed, nextThreshold};
    });

    return {ok: true, defs, states};
});

// -------- claimAchievementReward (callable) --------
export const claimAchievementReward = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const typeId = String(req.data?.typeId || "").trim() as AchType;
    const level = Number(req.data?.level ?? 0);
    if (!typeId || !level) throw new HttpsError("invalid-argument", "typeId and level required");
    const res = await grantAchReward(uid, typeId, level);
    return {ok: true, rewardGet: res.reward, newCurrency: res.newCurrency};
});
