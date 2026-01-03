"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.changeUsername = exports.recomputeRanks = exports.unequipItem = exports.equipItem = exports.getActiveConsumables = exports.purchaseItem = exports.getInventorySnapshot = exports.checkOwnership = exports.createItem = exports.getAllItems = exports.requestSession = exports.getSequencedMaps = exports.indexMapMeta = exports.getGalleryItems = exports.listReferredUsers = exports.claimAutopilot = exports.toggleAutopilot = exports.getAutopilotStatus = exports.grantBonusEnergy = exports.spendEnergy = exports.getEnergyStatus = exports.getEnergySnapshot = exports.submitSessionResult = exports.applyReferralCode = exports.updateDailyStreak = exports.ensureUserProfile = exports.createUserProfile = exports.verifyPurchase = exports.checkElitePass = exports.purchaseElitePass = exports.getLeaderboardsSnapshot = exports.syncLeaderboard = exports.claimAchievementReward = exports.getAchievementsSnapshot = exports.getStreakStatus = exports.claimStreak = exports.recordLogin = void 0;
const admin = __importStar(require("firebase-admin"));
const functionsV1 = __importStar(require("firebase-functions/v1"));
const firestore_1 = require("firebase-functions/v2/firestore");
// ...
const https_1 = require("firebase-functions/v2/https");
const options_1 = require("firebase-functions/v2/options");
const firestore_2 = require("@google-cloud/firestore");
// import {google} from "googleapis"; // Lazy loaded
console.log("[STARTUP] Loading index.ts...");
try {
    admin.initializeApp();
    console.log("[STARTUP] Firebase Admin initialized");
}
catch (e) {
    console.error("[STARTUP] Firebase Admin init failed", e);
}
// nam5 -> us-central1
(0, options_1.setGlobalOptions)({ region: "us-central1", memory: "512MiB" });
// --- ID Normalization Helper (use everywhere for itemId) ---
const normId = (s) => (s || "").trim().toLowerCase();
const DB_ID = "getfi";
const PROJECT_ID = process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT_ID || "";
const DB_PATH = `projects/${PROJECT_ID}/databases/${DB_ID}`;
console.log(`[boot] Firestore DB selected: ${DB_PATH}`);
const SEASON = "current";
const RANK_BATCH = 500; // how many users to rank per page
const RANK_LOCK_DOC = `leaderboards/${SEASON}/meta/rank_job`;
async function acquireRankLock(dbNow, holdMs = 2 * 60 * 1000) {
    const ref = db.doc(RANK_LOCK_DOC);
    try {
        await db.runTransaction(async (tx) => {
            const snap = await tx.get(ref);
            const until = snap.exists ? snap.get("lockedUntil") : null;
            const unlocked = !until || until.toMillis() <= dbNow.toMillis();
            if (!unlocked) {
                throw new Error("locked");
            }
            tx.set(ref, {
                lockedUntil: firestore_2.Timestamp.fromMillis(dbNow.toMillis() + holdMs),
                updatedAt: firestore_2.FieldValue.serverTimestamp()
            }, { merge: true });
        });
        return true;
    }
    catch (e) {
        return false;
    }
}
async function releaseRankLock() {
    const ref = db.doc(RANK_LOCK_DOC);
    try {
        await ref.set({
            lockedUntil: firestore_2.Timestamp.fromMillis(0),
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
    }
    catch (_a) { }
}
async function recomputeAllRanks() {
    var _a;
    const now = firestore_2.Timestamp.now();
    const got = await acquireRankLock(now);
    if (!got) {
        console.log("[ranks] another job is running; skip");
        return { count: 0 };
    }
    let ranked = 0;
    try {
        // page through all users ordered by maxScore desc
        let lastScore = null;
        // Note: Firestore pagination with a single orderBy avoids composite index
        while (true) {
            let q = db.collection("users").orderBy("maxScore", "desc").limit(RANK_BATCH);
            if (lastScore !== null) {
                q = q.startAfter(lastScore);
            }
            const snap = await q.get();
            if (snap.empty)
                break;
            const batch = db.batch();
            snap.docs.forEach((doc, i) => {
                const rank = ranked + i + 1; // 1-based
                batch.set(doc.ref, { rank, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
            });
            await batch.commit();
            ranked += snap.size;
            const last = snap.docs[snap.docs.length - 1];
            lastScore = Number((_a = last.get("maxScore")) !== null && _a !== void 0 ? _a : 0) || 0;
            // extend lock while we are still working
            await db.doc(RANK_LOCK_DOC).set({
                lockedUntil: firestore_2.Timestamp.fromMillis(firestore_2.Timestamp.now().toMillis() + 2 * 60 * 1000),
                updatedAt: firestore_2.FieldValue.serverTimestamp()
            }, { merge: true });
        }
        console.log(`[ranks] recomputed for ${ranked} users`);
        return { count: ranked };
    }
    finally {
        await releaseRankLock();
    }
}
// UTC date helper (YYYY-MM-DD)
function utcDateString(ts) {
    return new Date(ts.toMillis()).toISOString().slice(0, 10);
}
const db = new firestore_2.Firestore({ databaseId: DB_ID });
// ========================= Streak System (server-side only) =========================
// Doc layout:
// - Config: appdata/streakdata { reward:number }
// - User state: users/{uid}/meta/streak {
//     totalDays:number, unclaimedDays:number, lastLoginDate:string(YYYY-MM-DD UTC),
//     lastClaimAt?:Timestamp, createdAt:Timestamp, updatedAt:serverTimestamp
//   }
const streakConfigRef = db.collection("appdata").doc("streakdata");
const userStreakRef = (uid) => db.collection("users").doc(uid).collection("meta").doc("streak");
// recordLogin: DEPRECATED. Kept as a thin wrapper to the new updateDailyStreak core.
exports.recordLogin = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const res = await applyDailyStreakIncrement(uid);
    return Object.assign(Object.assign({ ok: true }, res), { deprecated: true });
});
// claimStreak: Grants accumulated unclaimedDays * reward to user currency and resets unclaimedDays.
exports.claimStreak = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const now = firestore_2.Timestamp.now();
    const uRef = db.collection("users").doc(uid);
    const sRef = userStreakRef(uid);
    const res = await db.runTransaction(async (tx) => {
        var _a, _b, _c, _d;
        const [uSnap, sSnap, cfgSnap] = await Promise.all([tx.get(uRef), tx.get(sRef), tx.get(streakConfigRef)]);
        if (!uSnap.exists)
            throw new https_1.HttpsError("failed-precondition", "User doc missing");
        const unclaimed = sSnap.exists ? Number(snapNum(sSnap.get("unclaimedDays"))) : 0;
        if (unclaimed <= 0) {
            const curCurrency = Number((_a = uSnap.get("currency")) !== null && _a !== void 0 ? _a : 0) || 0;
            const rewardPerDay = cfgSnap.exists ? Number((_b = cfgSnap.get("reward")) !== null && _b !== void 0 ? _b : 0) || 0 : 0;
            return { granted: 0, rewardPerDay: Math.max(0, rewardPerDay), unclaimedDays: 0, newCurrency: curCurrency };
        }
        const rewardPerDay = cfgSnap.exists ? Number((_c = cfgSnap.get("reward")) !== null && _c !== void 0 ? _c : 0) || 0 : 0;
        const grant = Math.max(0, rewardPerDay) * unclaimed;
        const curCurrency = Number((_d = uSnap.get("currency")) !== null && _d !== void 0 ? _d : 0) || 0;
        const newCurrency = curCurrency + grant;
        tx.set(uRef, { currency: newCurrency, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        tx.set(sRef, { unclaimedDays: 0, lastClaimAt: now, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        // optional: audit log
        const logRef = uRef.collection("streakClaims").doc();
        tx.set(logRef, {
            at: now,
            daysClaimed: unclaimed,
            rewardPerDay,
            granted: grant,
            currencyAfter: newCurrency,
        });
        return { granted: grant, rewardPerDay, unclaimedDays: 0, newCurrency };
    });
    return Object.assign({ ok: true }, res);
});
// getStreakStatus: For UI — shows whether a claim is available and the next UTC midnight countdown.
exports.getStreakStatus = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const res = await applyDailyStreakIncrement(uid);
    return Object.assign({ ok: true }, res);
});
// small numeric helper for safe casting
function snapNum(v) {
    const n = Number(v);
    return Number.isFinite(n) ? n : 0;
}
// ========================= Achievements =========================
// Types: map to server-side progress sources on users/{uid}
const ACH_TYPES = {
    ENDLESS_ROLLER: "endless_roller", // sessionsPlayed
    SCORE_CHAMPION: "score_champion", // maxScore
    TOKEN_HUNTER: "token_hunter", // cumulativeCurrencyEarned
    COMBO_GOD: "combo_god", // maxCombo
    MARKET_WHISPER: "market_whisperer", // itemsPurchasedCount
    TIME_DRIFTER: "time_drifter", // totalPlaytimeMinutes
    HABIT_MAKER: "habit_maker", // streak (daily login)
    POWERUP_EXP: "powerup_explorer", // powerUpsCollected
    SIGNAL_BOOST: "signal_booster", // referrals
};
const achDefRef = (typeId) => db.collection("appdata").doc("achievements").collection("types").doc(typeId);
const achUserRef = (uid, typeId) => db.collection("users").doc(uid).collection("achievements").doc(typeId);
async function readAchDef(typeId) {
    var _a;
    const snap = await achDefRef(typeId).get();
    if (!snap.exists)
        throw new https_1.HttpsError("not-found", `Achievement def missing: ${typeId}`);
    const d = snap.data() || {};
    const levels = Array.isArray(d.levels) ? d.levels : [];
    const norm = levels.map((x) => {
        var _a, _b;
        return ({
            threshold: Number((_a = x === null || x === void 0 ? void 0 : x.threshold) !== null && _a !== void 0 ? _a : 0) || 0,
            rewardGet: Number((_b = x === null || x === void 0 ? void 0 : x.rewardGet) !== null && _b !== void 0 ? _b : 0) || 0,
        });
    });
    return {
        levels: norm.slice(0, 5),
        displayName: typeof d.displayName === "string" ? d.displayName : undefined,
        description: typeof d.description === "string" ? d.description : undefined,
        iconUrl: typeof d.iconUrl === "string" ? d.iconUrl : undefined,
        order: Number((_a = d.order) !== null && _a !== void 0 ? _a : 0) || 0,
    };
}
function computeLevel(progress, levels) {
    let lvl = 0;
    for (let i = 0; i < levels.length; i++) {
        if (progress >= levels[i].threshold)
            lvl = i + 1;
        else
            break;
    }
    return lvl; // 0..5
}
async function upsertUserAch(uid, typeId, progress) {
    const def = await readAchDef(typeId);
    const level = computeLevel(progress, def.levels);
    const nextThreshold = level < def.levels.length ? def.levels[level].threshold : null;
    const ref = achUserRef(uid, typeId);
    await ref.set({
        progress,
        level,
        nextThreshold,
        updatedAt: firestore_2.FieldValue.serverTimestamp(),
    }, { merge: true });
}
async function grantAchReward(uid, typeId, level) {
    const def = await readAchDef(typeId);
    if (level < 1 || level > def.levels.length)
        throw new https_1.HttpsError("invalid-argument", "Invalid level");
    const reward = def.levels[level - 1].rewardGet;
    const uRef = db.collection("users").doc(uid);
    const aRef = achUserRef(uid, typeId);
    return await db.runTransaction(async (tx) => {
        var _a, _b;
        const [uSnap, aSnap] = await Promise.all([tx.get(uRef), tx.get(aRef)]);
        if (!aSnap.exists)
            throw new https_1.HttpsError("failed-precondition", "Achievement progress missing");
        const curLevel = Number((_a = aSnap.get("level")) !== null && _a !== void 0 ? _a : 0) || 0;
        if (curLevel < level)
            throw new https_1.HttpsError("failed-precondition", "Level not reached");
        const claimed = Array.isArray(aSnap.get("claimedLevels")) ? aSnap.get("claimedLevels") : [];
        if (claimed.includes(level))
            throw new https_1.HttpsError("already-exists", "Already claimed");
        const curCurrency = Number((_b = uSnap.get("currency")) !== null && _b !== void 0 ? _b : 0) || 0;
        tx.set(uRef, { currency: curCurrency + reward, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        tx.set(aRef, { claimedLevels: firestore_2.FieldValue.arrayUnion(level), lastClaimedAt: firestore_2.Timestamp.now() }, { merge: true });
        return { reward, newCurrency: curCurrency + reward };
    });
}
// -------- getAchievementsSnapshot (callable) --------
exports.getAchievementsSnapshot = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    // 1) Load ALL achievement type docs (dynamic; no hardcoded ids)
    const typesCol = db.collection("appdata").doc("achievements").collection("types");
    const typeSnap = await typesCol.get();
    const defs = [];
    for (const doc of typeSnap.docs) {
        const id = doc.id;
        const raw = (doc.data() || {});
        const levelsArr = Array.isArray(raw.levels) ? raw.levels : [];
        const levels = levelsArr.map((x) => {
            var _a, _b;
            return ({
                threshold: Number((_a = x === null || x === void 0 ? void 0 : x.threshold) !== null && _a !== void 0 ? _a : 0) || 0,
                rewardGet: Number((_b = x === null || x === void 0 ? void 0 : x.rewardGet) !== null && _b !== void 0 ? _b : 0) || 0,
            });
        });
        const thresholds = levels.map(l => l.threshold);
        const rewards = levels.map(l => l.rewardGet);
        defs.push({
            typeId: id,
            displayName: typeof raw.displayName === "string" ? raw.displayName : id,
            description: typeof raw.description === "string" ? raw.description : "",
            iconUrl: typeof raw.iconUrl === "string" ? raw.iconUrl : "",
            order: Number((_b = raw.order) !== null && _b !== void 0 ? _b : 0) || 0,
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
    const states = defs.map((d, i) => {
        var _a, _b, _c, _d;
        const s = stateSnaps[i];
        const progress = s.exists ? Number((_a = s.get("progress")) !== null && _a !== void 0 ? _a : 0) || 0 : 0;
        const level = s.exists ? Number((_b = s.get("level")) !== null && _b !== void 0 ? _b : 0) || 0 : 0;
        const claimed = s.exists && Array.isArray(s.get("claimedLevels")) ? s.get("claimedLevels") : [];
        const nextThreshold = s.exists ? ((_c = s.get("nextThreshold")) !== null && _c !== void 0 ? _c : null) : ((_d = d.thresholds[level]) !== null && _d !== void 0 ? _d : null);
        return { typeId: d.typeId, progress, level, claimedLevels: claimed, nextThreshold };
    });
    return { ok: true, defs, states };
});
// -------- claimAchievementReward (callable) --------
exports.claimAchievementReward = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const typeId = String(((_b = req.data) === null || _b === void 0 ? void 0 : _b.typeId) || "").trim();
    const level = Number((_d = (_c = req.data) === null || _c === void 0 ? void 0 : _c.level) !== null && _d !== void 0 ? _d : 0);
    if (!typeId || !level)
        throw new https_1.HttpsError("invalid-argument", "typeId and level required");
    const res = await grantAchReward(uid, typeId, level);
    return { ok: true, rewardGet: res.reward, newCurrency: res.newCurrency };
});
// ---- Stats helpers for equip/unequip merging ----
function parseStatsJson(s) {
    if (typeof s !== "string")
        return {};
    try {
        const o = JSON.parse(s);
        if (o && typeof o === "object")
            return o;
        return {};
    }
    catch (_a) {
        return {};
    }
}
function mergeStats(base, delta, sign) {
    var _a;
    const out = Object.assign({}, base);
    for (const k of Object.keys(delta)) {
        const v = Number(delta[k]);
        if (!Number.isFinite(v))
            continue;
        const cur = Number((_a = out[k]) !== null && _a !== void 0 ? _a : 0);
        out[k] = cur + sign * v;
    }
    return out;
}
function extractItemStats(raw) {
    const out = {};
    for (const [k, v] of Object.entries(raw || {})) {
        if (k.startsWith("itemstat_")) {
            const statKey = k.replace("itemstat_", "");
            out[statKey] = Number(v) || 0;
        }
    }
    return out;
}
// ---- Consumables lazy cleanup helper ----
async function cleanupExpiredConsumablesInTx(tx, userRef, now) {
    const activeCol = userRef.collection("activeConsumables");
    // READS first: all reads before writes
    const expiredSnap = await tx.get(activeCol.where("expiresAt", "<=", now));
    if (expiredSnap.empty)
        return;
    let totalDelta = {};
    const itemRefs = [];
    const toDelete = [];
    expiredSnap.docs.forEach((d) => {
        const itemId = d.id;
        const itemRef = db
            .collection("appdata")
            .doc("items")
            .collection(itemId)
            .doc("itemdata");
        itemRefs.push(itemRef);
        toDelete.push(d.ref);
    });
    // READ: item defs (stats)
    const itemSnaps = await Promise.all(itemRefs.map((r) => tx.get(r)));
    itemSnaps.forEach((s) => {
        if (!s.exists)
            return;
        const stats = extractItemStats(s.data() || {});
        totalDelta = mergeStats(totalDelta, stats, -1); // subtract
    });
    // WRITE: apply merged subtraction to user's stats
    if (Object.keys(totalDelta).length > 0) {
        const uSnap = await tx.get(userRef);
        const baseStats = parseStatsJson(uSnap.get("statsJson"));
        const merged = mergeStats(baseStats, totalDelta, 1); // totalDelta already negative
        tx.update(userRef, {
            statsJson: JSON.stringify(merged),
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
        });
    }
    // WRITE: delete expired docs
    toDelete.forEach((ref) => tx.delete(ref));
}
// ---------------- Leaderboard Sync ----------------
exports.syncLeaderboard = (0, firestore_1.onDocumentWritten)({ document: "users/{uid}", database: DB_ID }, async (event) => {
    var _a, _b;
    const uid = event.params.uid;
    const after = (_b = (_a = event.data) === null || _a === void 0 ? void 0 : _a.after) === null || _b === void 0 ? void 0 : _b.data();
    if (!after)
        return;
    const username = (typeof after.username === "string" ? after.username : "").trim()
        || "Guest";
    const rawMaxScore = after.maxScore;
    const score = typeof rawMaxScore === "number" ? rawMaxScore : 0;
    const seasonRef = db.collection("leaderboards").doc(SEASON);
    // 1) public entry
    const elitePassExpiresAt = after.elitePassExpiresAt || null;
    await seasonRef.collection("entries").doc(uid).set({
        username,
        score,
        elitePassExpiresAt, // carry expiry to leaderboard entry for client-side active check
        updatedAt: firestore_2.FieldValue.serverTimestamp(),
    }, { merge: true });
    // NOTE: Removed materialization of topN and background rank recomputation.
});
// ---------------- getLeaderboardsSnapshot (callable) ----------------
// Returns a snapshot of the leaderboard from leaderboards/{SEASON}/entries.
// Client should page using startAfterScore if needed. No ranks are written/returned.
exports.getLeaderboardsSnapshot = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d, _e;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const now = firestore_2.Timestamp.now();
    // params
    const limitIn = Number((_c = (_b = req.data) === null || _b === void 0 ? void 0 : _b.limit) !== null && _c !== void 0 ? _c : 100);
    const startAfterScoreRaw = (_d = req.data) === null || _d === void 0 ? void 0 : _d.startAfterScore;
    const includeSelf = !!((_e = req.data) === null || _e === void 0 ? void 0 : _e.includeSelf); // if true, echo caller's current entry
    const limit = Math.max(1, Math.min(limitIn, 500));
    const seasonRef = db.collection("leaderboards").doc(SEASON);
    let q = seasonRef.collection("entries").orderBy("score", "desc").limit(limit);
    if (startAfterScoreRaw !== undefined && startAfterScoreRaw !== null) {
        const s = Number(startAfterScoreRaw);
        if (Number.isFinite(s))
            q = q.startAfter(s);
    }
    const snap = await q.get();
    const items = snap.docs.map((d) => {
        var _a, _b, _c;
        const data = (d.data() || {});
        const username = (typeof data.username === "string" ? data.username : "").trim() || "Guest";
        const score = typeof data.score === "number" ? data.score : 0;
        const updatedAt = (_c = (_b = (_a = data.updatedAt) === null || _a === void 0 ? void 0 : _a.toDate()) === null || _b === void 0 ? void 0 : _b.toISOString()) !== null && _c !== void 0 ? _c : null;
        const eliteTs = data.elitePassExpiresAt || undefined;
        const elitePassExpiresAtMillis = eliteTs ? eliteTs.toMillis() : null;
        return { uid: d.id, username, score, updatedAt, elitePassExpiresAtMillis };
    });
    const hasMore = snap.size >= limit;
    const next = hasMore && snap.docs.length > 0
        ? { startAfterScore: (typeof snap.docs[snap.docs.length - 1].get("score") === 'number' ? snap.docs[snap.docs.length - 1].get("score") : 0) }
        : null;
    let me = null;
    if (includeSelf) {
        // Prefer leaderboard entry; fallback to users doc
        const [myEntrySnap, userSnap] = await Promise.all([
            seasonRef.collection("entries").doc(uid).get(),
            db.collection("users").doc(uid).get()
        ]);
        if (myEntrySnap.exists) {
            const md = myEntrySnap.data() || {};
            me = {
                uid,
                username: (typeof md.username === "string" ? md.username : "").trim() || "Guest",
                score: typeof md.score === "number" ? md.score : 0,
            };
        }
        else if (userSnap.exists) {
            const ud = userSnap.data() || {};
            me = {
                uid,
                username: (typeof ud.username === "string" ? ud.username : "").trim() || "Guest",
                score: typeof ud.maxScore === "number" ? ud.maxScore : 0,
            };
        }
    }
    return { ok: true, season: SEASON, serverNowMillis: now.toMillis(), count: items.length, hasMore, next, items, me };
});
// ---------------- Elite Pass ----------------
exports.purchaseElitePass = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d, _e;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const purchaseId = ((_c = (_b = req.data) === null || _b === void 0 ? void 0 : _b.purchaseId) !== null && _c !== void 0 ? _c : "").toString().trim();
    const now = firestore_2.Timestamp.now();
    const userRef = db.collection("users").doc(uid);
    // idempotency
    if (purchaseId) {
        const pRef = userRef.collection("elitePassPurchases").doc(purchaseId);
        const pSnap = await pRef.get();
        if (pSnap.exists) {
            const uSnap = await userRef.get();
            const exp = uSnap.exists
                ? uSnap.get("elitePassExpiresAt")
                : null;
            const active = !!exp && exp.toMillis() > now.toMillis();
            return {
                active,
                expiresAt: (_d = exp === null || exp === void 0 ? void 0 : exp.toDate().toISOString()) !== null && _d !== void 0 ? _d : null,
            };
        }
    }
    await db.runTransaction(async (tx) => {
        const uSnap = await tx.get(userRef);
        const existing = uSnap.exists
            ? uSnap.get("elitePassExpiresAt")
            : null;
        const baseMillis = existing && existing.toMillis() > now.toMillis()
            ? existing.toMillis()
            : now.toMillis();
        const thirtyMs = 30 * 24 * 60 * 60 * 1000;
        const newExpiry = firestore_2.Timestamp.fromMillis(baseMillis + thirtyMs);
        tx.set(userRef, {
            hasElitePass: true,
            elitePassExpiresAt: newExpiry,
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
        if (purchaseId) {
            tx.set(userRef.collection("elitePassPurchases").doc(purchaseId), { processedAt: now, newExpiry }, { merge: true });
        }
    });
    const final = await userRef.get();
    const exp = final.get("elitePassExpiresAt");
    const active = !!exp && exp.toMillis() > now.toMillis();
    return {
        active,
        expiresAt: (_e = exp === null || exp === void 0 ? void 0 : exp.toDate().toISOString()) !== null && _e !== void 0 ? _e : null,
    };
});
exports.checkElitePass = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const now = firestore_2.Timestamp.now();
    const snap = await db.collection("users").doc(uid).get();
    if (!snap.exists)
        return {
            active: false,
            expiresAt: null,
        };
    const exp = snap.get("elitePassExpiresAt");
    const active = !!exp && exp.toMillis() > now.toMillis();
    return {
        active,
        expiresAt: (_b = exp === null || exp === void 0 ? void 0 : exp.toDate().toISOString()) !== null && _b !== void 0 ? _b : null,
    };
});
// ---------------- Referral ----------------
// benzer karakterler yok
const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
function randomReferralKey(len = 12) {
    let s = "";
    for (let i = 0; i < len; i++) {
        s += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
    }
    return s;
}
async function reserveUniqueReferralKey(uid) {
    for (let i = 0; i < 6; i++) {
        const k = randomReferralKey(12);
        const ref = db.collection("referralKeys").doc(k);
        const snap = await ref.get();
        if (!snap.exists) {
            await ref.set({ ownerUid: uid, createdAt: firestore_2.Timestamp.now() });
            return k;
        }
    }
    throw new Error("Could not allocate unique referral key");
}
async function ensureReferralKeyFor(uid) {
    const userRef = db.collection("users").doc(uid);
    return await db.runTransaction(async (tx) => {
        const snap = await tx.get(userRef);
        if (!snap.exists)
            throw new Error("user doc missing");
        const current = snap.get("referralKey") || "";
        if (current)
            return current;
        const key = await reserveUniqueReferralKey(uid);
        tx.set(userRef, { referralKey: key, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        return key;
    });
}
// ========================= IAP Verification =========================
// Config for iOS
// Config for iOS moved to verifyAppleStore to avoid load-time crash
// Load Google Service Account
let googleAuthClient = null;
async function getGoogleAuth() {
    if (googleAuthClient)
        return googleAuthClient;
    const { google } = require("googleapis");
    try {
        // Determine path to service account (copied to lib/ during build)
        // In Cloud Functions, __dirname is usually /workspace/lib or /workspace/src depending on structure
        // We try to require it relative to this file.
        const key = require("./service-account.json");
        const auth = new google.auth.GoogleAuth({
            credentials: {
                client_email: key.client_email,
                private_key: key.private_key,
            },
            scopes: ["https://www.googleapis.com/auth/androidpublisher"],
        });
        googleAuthClient = await auth.getClient();
        return googleAuthClient;
    }
    catch (e) {
        console.error("[IAP] Failed to load service-account.json", e);
        throw new Error("Server configuration error: Google Auth failed");
    }
}
// Helper to verify Apple Receipt
async function verifyAppleStore(receipt) {
    var _a, _b, _c;
    const APPLE_SHARED_SECRET = String(((_c = (((_b = (_a = functionsV1).config) === null || _b === void 0 ? void 0 : _b.call(_a)) || {}).iap) === null || _c === void 0 ? void 0 : _c.apple_secret) || "");
    const excludeOldTransactions = true;
    try {
        // Receipt from Unity is often a base64 encoded string OR a JSON with "Payload" being base64.
        let base64Receipt = receipt;
        try {
            const json = JSON.parse(receipt);
            if (json.Payload)
                base64Receipt = json.Payload;
            else if (json.payload)
                base64Receipt = json.payload;
        }
        catch (_d) { }
        // Remove any pure whitespace
        base64Receipt = base64Receipt.trim();
        const body = {
            "receipt-data": base64Receipt,
            "password": APPLE_SHARED_SECRET,
            "exclude-old-transactions": excludeOldTransactions
        };
        // Try Production first
        let response = await fetch("https://buy.itunes.apple.com/verifyReceipt", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });
        let data = await response.json();
        // If status 21007, try Sandbox
        if (data.status === 21007) {
            response = await fetch("https://sandbox.itunes.apple.com/verifyReceipt", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(body)
            });
            data = await response.json();
        }
        if (data.status === 0) {
            return true;
        }
        console.warn("[IAP] Apple verification failed. Status:", data.status);
        return false;
    }
    catch (e) {
        console.error("[IAP] Apple verification exception", e);
        return false;
    }
}
// Helper to verify Google Play receipt
async function verifyGooglePlay(productId, receiptJson) {
    try {
        const { google } = require("googleapis");
        // Parse Unity receipt
        const receiptObj = JSON.parse(receiptJson);
        // Unity's GooglePlay receipt format: { "Payload": "{ \"json\": \"...\", \"signature\": \"...\" }" }
        // Or sometimes directly inside "Payload".
        // Let's decode the payload first.
        // Typically Unity sends the raw receipt string which might look like:
        // {"Store":"GooglePlay","TransactionID":"...","Payload":"{ ... }"}
        let payloadStr = receiptObj.Payload || receiptObj.payload;
        if (!payloadStr) {
            // fallback: maybe the whole json IS the payload details?
            // If standard Unity IAP structure is used, Payload is an inner JSON string.
            console.warn("[IAP] Google receipt missing Payload field", receiptObj);
            return false;
        }
        const payload = JSON.parse(payloadStr);
        // payload content structure:
        // { "json": "{\"orderId\":...}", "signature": "..." }
        const innerJson = JSON.parse(payload.json);
        const token = innerJson.purchaseToken;
        const packageName = innerJson.packageName;
        const sku = innerJson.productId;
        if (!token || !packageName || !sku) {
            console.error("[IAP] Malformed Google receipt payload", innerJson);
            return false;
        }
        // Call Google API
        const auth = await getGoogleAuth();
        const androidPublisher = google.androidpublisher({ version: "v3", auth });
        // Try as product (consumable/non-consumable)
        try {
            const res = await androidPublisher.purchases.products.get({
                packageName,
                productId: sku,
                token,
            });
            if (res.data.purchaseState === 0) { // 0 = Purchased
                return true;
            }
        }
        catch (e) {
            // ignore, might be subscription
        }
        // Try as subscription
        try {
            const res = await androidPublisher.purchases.subscriptions.get({
                packageName,
                subscriptionId: sku,
                token,
            });
            // check paymentState, expiryTimeMillis, etc.
            // paymentState: 1 = Received
            // expiryTimeMillis > now
            if (res.data.expiryTimeMillis) {
                const expiry = Number(res.data.expiryTimeMillis);
                if (expiry > Date.now())
                    return true;
            }
        }
        catch (e) {
            console.warn("[IAP] Google verification failed for both product and sub", e);
        }
        return false;
    }
    catch (e) {
        console.error("[IAP] Google verification exception", e);
        return false;
    }
}
exports.verifyPurchase = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const productId = (_b = req.data) === null || _b === void 0 ? void 0 : _b.productId;
    const receipt = (_c = req.data) === null || _c === void 0 ? void 0 : _c.receipt; // This is the full receipt string from Unity
    if (!productId || !receipt) {
        throw new https_1.HttpsError("invalid-argument", "Missing productId or receipt");
    }
    console.log(`[verifyPurchase] Start for ${uid} - ${productId}`);
    // 1. Detect Store
    let isGoogle = false;
    let isApple = false;
    // Simple heuristic or parsing
    if (receipt.includes("GooglePlay"))
        isGoogle = true;
    else if (receipt.includes("AppleAppStore"))
        isApple = true;
    else {
        // Attempt parse
        try {
            const r = JSON.parse(receipt);
            if (r.Store === "GooglePlay")
                isGoogle = true;
            else if (r.Store === "AppleAppStore" || r.Store === "MacAppStore")
                isApple = true;
        }
        catch (_d) { }
    }
    // 2. Verify
    let valid = false;
    if (isGoogle) {
        valid = await verifyGooglePlay(productId, receipt);
    }
    else if (isApple) {
        valid = await verifyAppleStore(receipt);
    }
    else {
        // Unknown store (Editor? FakeStore?)
        console.warn("[IAP] Unknown store in receipt", receipt);
        // If testing in Editor (fake store)
        try {
            const r = JSON.parse(receipt);
            if (r.Store === "fake" || r.Store === "FakeStore") {
                console.log("[IAP] FakeStore detected, auto-verifying for testing.");
                valid = true;
            }
        }
        catch (_e) { }
        if (!valid) {
            throw new https_1.HttpsError("failed-precondition", "Unknown store");
        }
    }
    if (!valid) {
        throw new https_1.HttpsError("permission-denied", "Receipt verification failed");
    }
    // 3. Grant Rewards (Idempotent)
    // We rely on transactionID normally, but for simplicity here we will just grant.
    // IMPROVEMENT: Store transactionIDs to prevent replay attacks.
    const result = { success: true, message: "Verified", rewards: {} };
    const userRef = db.collection("users").doc(uid);
    // MAPPING
    // Consumables
    if (productId.includes("diamond")) {
        let amount = 0;
        if (productId.endsWith("diamond5"))
            amount = 5;
        else if (productId.endsWith("diamond10"))
            amount = 10;
        else if (productId.endsWith("diamond25"))
            amount = 25;
        else if (productId.endsWith("diamond60"))
            amount = 60;
        else if (productId.endsWith("diamond150"))
            amount = 150;
        else if (productId.endsWith("diamond400"))
            amount = 400;
        else if (productId.endsWith("diamond1000"))
            amount = 1000;
        if (amount > 0) {
            await userRef.update({
                premiumCurrency: firestore_2.FieldValue.increment(amount),
                updatedAt: firestore_2.FieldValue.serverTimestamp()
            });
            result.rewards["diamonds"] = amount;
        }
    }
    // Subscriptions
    else if (productId.includes("elitepass")) {
        // Logic similar to purchaseElitePass but just refreshing expiry
        const now = firestore_2.Timestamp.now();
        let days = 30; // Default monthly
        if (productId.includes("annual")) {
            days = 365;
        }
        const durationMs = days * 24 * 60 * 60 * 1000;
        // In a real app, query the subscription info for exact expiry
        await db.runTransaction(async (tx) => {
            const snap = await tx.get(userRef);
            const currentExp = snap.exists ? snap.get("elitePassExpiresAt") : null;
            let baseTime = now.toMillis();
            if (currentExp && currentExp.toMillis() > baseTime) {
                baseTime = currentExp.toMillis();
            }
            const newExp = firestore_2.Timestamp.fromMillis(baseTime + durationMs);
            tx.set(userRef, {
                hasElitePass: true,
                elitePassExpiresAt: newExp,
                updatedAt: firestore_2.FieldValue.serverTimestamp()
            }, { merge: true });
        });
        result.message = "Elite Pass Extended";
    }
    // Non-Consumables
    else if (productId.includes("removeads")) {
        await userRef.update({
            removeAds: true,
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        });
        result.message = "Ads Removed";
    }
    return result;
});
// Grant referral reward items to ownerUid if their referrals reach new thresholds
async function grantReferralThresholdItems(ownerUid, referrals) {
    var _a;
    if (!ownerUid || !Number.isFinite(referrals) || referrals <= 0)
        return;
    console.log("[referralItems] START", { ownerUid, referrals });
    // Root for appdata/items/...
    const itemsRoot = db.collection("appdata").doc("items");
    // Collect candidate referral-only items whose threshold is <= current referrals
    const subCols = await itemsRoot.listCollections();
    console.log("[referralItems] COLS", subCols.map((c) => c.id));
    const candidates = [];
    for (const subCol of subCols) {
        const docSnap = await subCol.doc("itemdata").get();
        if (!docSnap.exists)
            continue;
        const data = (docSnap.data() || {});
        console.log("[referralItems] ITEMDATA", { itemId: subCol.id, data });
        const threshold = Number((_a = data.itemReferralThreshold) !== null && _a !== void 0 ? _a : 0) || 0;
        if (threshold <= 0)
            continue;
        if (referrals < threshold)
            continue; // not yet eligible
        console.log("[referralItems] CANDIDATE", { itemId: subCol.id, threshold });
        const isConsumable = !!data.itemIsConsumable;
        const itemId = normId(subCol.id);
        candidates.push({ itemId, threshold, isConsumable });
    }
    console.log("[referralItems] CANDIDATES_FINAL", candidates);
    if (candidates.length === 0)
        return;
    const userRef = db.collection("users").doc(ownerUid);
    const invCol = userRef.collection("inventory");
    const now = firestore_2.Timestamp.now();
    await db.runTransaction(async (tx) => {
        // === READ PHASE: all tx.get calls first ===
        const invRefs = candidates.map((c) => invCol.doc(c.itemId));
        const invSnaps = await Promise.all(invRefs.map((r) => tx.get(r)));
        // Precompute existing data so we don't read after writes
        const invStates = invSnaps.map((snap) => {
            const exists = snap.exists;
            const data = exists ? (snap.data() || {}) : {};
            return { exists, data: data };
        });
        // === WRITE PHASE: only tx.set calls, no more tx.get ===
        candidates.forEach((c, index) => {
            var _a, _b;
            const invRef = invRefs[index];
            const invState = invStates[index];
            const invData = invState.data;
            const prevThresh = Number((_a = invData.referralGrantedThreshold) !== null && _a !== void 0 ? _a : 0) || 0;
            console.log("[referralItems] TX_CHECK", {
                itemId: c.itemId,
                prevThresh,
                cThreshold: c.threshold,
            });
            // Prevent double-grant per item by tracking the highest referral threshold already granted
            if (prevThresh >= c.threshold) {
                return; // already granted for this (or a higher) threshold
            }
            const base = {
                grantedByReferral: true,
                referralGrantedThreshold: c.threshold,
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            };
            if (!invState.exists) {
                base.createdAt = now;
            }
            const prevQty = Number((_b = invData.quantity) !== null && _b !== void 0 ? _b : 0) || 0;
            base.owned = true;
            if (c.isConsumable) {
                // consumables should still count as owned for UI, and stack quantity
                base.quantity = prevQty + 1;
            }
            else {
                // non-consumables: ensure at least 1
                base.quantity = prevQty > 0 ? prevQty : 1;
            }
            console.log("[referralItems] TX_SET", { itemId: c.itemId, base });
            tx.set(invRef, base, { merge: true });
        });
    });
}
async function applyReferralCodeToUser(uid, codeRaw) {
    var _a;
    const code = (codeRaw || "").toUpperCase().trim();
    if (!/^[A-Z0-9]{12}$/.test(code)) {
        throw new https_1.HttpsError("invalid-argument", "Invalid referral code");
    }
    const keyRef = db.collection("referralKeys").doc(code);
    const keySnap = await keyRef.get();
    if (!keySnap.exists) {
        throw new https_1.HttpsError("not-found", "Referral code not found");
    }
    const ownerUid = keySnap.get("ownerUid") || "";
    if (!ownerUid || ownerUid === uid) {
        throw new https_1.HttpsError("failed-precondition", "Cannot use this code");
    }
    const userRef = db.collection("users").doc(uid);
    const ownerRef = db.collection("users").doc(ownerUid);
    await db.runTransaction(async (tx) => {
        const u = await tx.get(userRef);
        if (!u.exists) {
            throw new https_1.HttpsError("failed-precondition", "User doc missing");
        }
        const already = u.get("referredByKey") || "";
        if (already)
            return; // idempotent
        tx.set(userRef, {
            referredByKey: code,
            referredByUid: ownerUid,
            referralAppliedAt: firestore_2.Timestamp.now(),
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
        tx.set(ownerRef, {
            referrals: firestore_2.FieldValue.increment(1),
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
    });
    try {
        const ownerSnap = await db.collection("users").doc(ownerUid).get();
        const referralsCount = Number((_a = ownerSnap.get("referrals")) !== null && _a !== void 0 ? _a : 0) || 0;
        await upsertUserAch(ownerUid, ACH_TYPES.SIGNAL_BOOST, referralsCount);
        await grantReferralThresholdItems(ownerUid, referralsCount);
    }
    catch (e) {
        console.warn("[ach/referral] post-referral processing failed", e);
    }
    return { applied: true, referredByUid: ownerUid };
}
// -------- profile create (Auth trigger) --------
exports.createUserProfile = functionsV1
    .region("us-central1")
    .auth.user()
    .onCreate(async (user) => {
    var _a;
    const uid = user.uid;
    const email = (_a = user.email) !== null && _a !== void 0 ? _a : "";
    const userRef = db.collection("users").doc(uid);
    await db.runTransaction(async (tx) => {
        const snap = await tx.get(userRef);
        if (snap.exists)
            return; // idempotent
        tx.set(userRef, {
            mail: email,
            username: "",
            currency: 0,
            premiumCurrency: 0,
            energyCurrent: 6,
            energyMax: 6,
            energyRegenPeriodSec: 14400,
            energyUpdatedAt: firestore_2.Timestamp.now(),
            statsJson: JSON.stringify({
                comboPower: 0,
                coinMultiplierPercent: 0,
                magnetPowerPercent: 0,
                gameplaySpeedMultiplierPercent: 0,
                playerAcceleration: 0.1,
                playerSpeed: 0,
                playerSizePercent: 0
            }),
            streak: 0,
            bestStreak: 0,
            lastLoginLocalDate: "",
            tzOffsetMinutes: 0,
            trustFactor: 100,
            rank: 0,
            maxScore: 0,
            hasElitePass: false,
            // init as expired (avoid nulls for Unity deserialization)
            elitePassExpiresAt: firestore_2.Timestamp.fromMillis(Date.now() - 24 * 60 * 60 * 1000),
            lastLogin: firestore_2.Timestamp.now(),
            createdAt: firestore_2.FieldValue.serverTimestamp(),
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
            sessionsPlayed: 0,
            cumulativeCurrencyEarned: 0,
            itemsPurchasedCount: 0,
            totalPlaytimeSec: 0,
            totalPlaytimeMinutes: 0,
            totalPlaytimeMinutesFloat: 0,
            powerUpsCollected: 0,
            maxCombo: 0,
        });
        // Eagerly create the streak subdoc in the same transaction
        const sRef = userStreakRef(uid);
        const nowTs = firestore_2.Timestamp.now();
        tx.set(sRef, {
            totalDays: 0,
            unclaimedDays: 0,
            lastLoginDate: "",
            createdAt: nowTs,
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
        }, { merge: true });
    });
    await ensureReferralKeyFor(uid);
});
// -------- profile ensure + optional referral apply --------
exports.ensureUserProfile = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d, _e, _f;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    const email = (_d = (_c = (_b = req.auth) === null || _b === void 0 ? void 0 : _b.token) === null || _c === void 0 ? void 0 : _c.email) !== null && _d !== void 0 ? _d : "";
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    await db.runTransaction(async (tx) => {
        const snap = await tx.get(userRef);
        if (!snap.exists) {
            tx.set(userRef, {
                mail: email,
                username: "",
                currency: 0,
                premiumCurrency: 0,
                energyCurrent: 6,
                energyMax: 6,
                energyRegenPeriodSec: 14400,
                energyUpdatedAt: firestore_2.Timestamp.now(),
                statsJson: JSON.stringify({
                    comboPower: 0,
                    coinMultiplierPercent: 0,
                    magnetPowerPercent: 0,
                    gameplaySpeedMultiplierPercent: 0,
                    playerAcceleration: 0.1,
                    playerSpeed: 0,
                    playerSizePercent: 0
                }),
                streak: 0,
                bestStreak: 0,
                lastLoginLocalDate: "",
                tzOffsetMinutes: 0,
                trustFactor: 100,
                rank: 0,
                maxScore: 0,
                hasElitePass: false,
                // init as expired (avoid nulls for Unity deserialization)
                elitePassExpiresAt: firestore_2.Timestamp.fromMillis(Date.now() - 24 * 60 * 60 * 1000),
                lastLogin: firestore_2.Timestamp.now(),
                createdAt: firestore_2.FieldValue.serverTimestamp(),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
                sessionsPlayed: 0,
                cumulativeCurrencyEarned: 0,
                itemsPurchasedCount: 0,
                totalPlaytimeSec: 0,
                totalPlaytimeMinutes: 0,
                totalPlaytimeMinutesFloat: 0,
                powerUpsCollected: 0,
                maxCombo: 0,
            });
            // Eagerly create the streak subdoc in the same transaction
            const sRef = userStreakRef(uid);
            const nowTs = firestore_2.Timestamp.now();
            tx.set(sRef, {
                totalDays: 0,
                unclaimedDays: 0,
                lastLoginDate: "",
                createdAt: nowTs,
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            }, { merge: true });
        }
        else {
            const needInit = !snap.get("elitePassExpiresAt");
            const patch = {
                lastLogin: firestore_2.Timestamp.now(),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            };
            if (needInit) {
                // backfill as expired if missing/null
                patch.elitePassExpiresAt =
                    firestore_2.Timestamp.fromMillis(Date.now() - 24 * 60 * 60 * 1000);
            }
            // Backfill missing progress counters for legacy users
            for (const k of [
                "sessionsPlayed",
                "cumulativeCurrencyEarned",
                "itemsPurchasedCount",
                "totalPlaytimeSec",
                "totalPlaytimeMinutes",
                "totalPlaytimeMinutesFloat",
                "powerUpsCollected",
                "maxCombo",
                "bestStreak",
                "lastLoginLocalDate",
                "tzOffsetMinutes",
            ]) {
                if (snap.get(k) === undefined)
                    patch[k] = 0;
            }
            if (snap.get("premiumCurrency") === undefined)
                patch.premiumCurrency = 0;
            // Ensure streak subdoc exists without clobbering existing values
            const sRef = userStreakRef(uid);
            const sSnap2 = await tx.get(sRef);
            if (!sSnap2.exists) {
                tx.set(sRef, {
                    totalDays: 0,
                    unclaimedDays: 0,
                    lastLoginDate: "",
                    createdAt: firestore_2.Timestamp.now(),
                    updatedAt: firestore_2.FieldValue.serverTimestamp(),
                }, { merge: true });
            }
            else {
                // just touch updatedAt to keep a heartbeat; don't reset counters
                tx.set(sRef, { updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
            }
            tx.set(userRef, patch, { merge: true });
        }
    });
    const key = await ensureReferralKeyFor(uid);
    const code = ((_f = (_e = req.data) === null || _e === void 0 ? void 0 : _e.referralCode) !== null && _f !== void 0 ? _f : "").toString();
    if (code)
        await applyReferralCodeToUser(uid, code);
    return {
        ok: true,
        referralKey: key,
    };
});
// Shared core for streak increment logic. Idempotent per UTC day.
async function applyDailyStreakIncrement(uid) {
    const now = firestore_2.Timestamp.now();
    const today = utcDateString(now); // YYYY-MM-DD in UTC
    const userRef = db.collection("users").doc(uid);
    const sRef = userStreakRef(uid);
    const { totalDays, unclaimedDays, rewardPerDay, todayCounted } = await db.runTransaction(async (tx) => {
        var _a, _b;
        const [uSnap, sSnap, cfgSnap] = await Promise.all([
            tx.get(userRef),
            tx.get(sRef),
            tx.get(streakConfigRef),
        ]);
        if (!uSnap.exists) {
            throw new https_1.HttpsError("failed-precondition", "User doc missing");
        }
        const rewardPerDay = cfgSnap.exists ? Number((_a = cfgSnap.get("reward")) !== null && _a !== void 0 ? _a : 0) || 0 : 0;
        const lastDate = sSnap.exists ? sSnap.get("lastLoginDate") || "" : "";
        const prevTotal = sSnap.exists ? Number(snapNum(sSnap.get("totalDays"))) : 0;
        const prevUnclaimed = sSnap.exists ? Number(snapNum(sSnap.get("unclaimedDays"))) : 0;
        let totalDays = prevTotal;
        let unclaimedDays = prevUnclaimed;
        let todayCounted = false;
        if (lastDate !== today) {
            totalDays = prevTotal + 1;
            unclaimedDays = prevUnclaimed + 1;
            todayCounted = true;
        }
        tx.set(sRef, {
            totalDays,
            unclaimedDays,
            lastLoginDate: today,
            createdAt: sSnap.exists ? (_b = sSnap.get("createdAt")) !== null && _b !== void 0 ? _b : now : now,
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
        }, { merge: true });
        tx.set(userRef, { updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        return { totalDays, unclaimedDays, rewardPerDay, todayCounted };
    });
    const d = new Date(now.toMillis());
    d.setUTCHours(24, 0, 0, 0);
    const nextUtcMidnightMillis = d.getTime();
    const safeRewardPerDay = Math.max(0, Number(rewardPerDay) || 0);
    const safeUnclaimed = Math.max(0, Number(unclaimedDays) || 0);
    const pending = safeRewardPerDay * safeUnclaimed;
    return {
        serverNowMillis: now.toMillis(),
        nextUtcMidnightMillis,
        totalDays: Number(totalDays) || 0,
        unclaimedDays: safeUnclaimed,
        rewardPerDay: safeRewardPerDay,
        pendingTotalReward: pending,
        claimAvailable: pending > 0,
        todayCounted: !!todayCounted,
    };
}
exports.updateDailyStreak = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const res = await applyDailyStreakIncrement(uid);
    return Object.assign({ ok: true }, res);
});
// -------- apply referral later (optional) --------
exports.applyReferralCode = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const code = ((_c = (_b = req.data) === null || _b === void 0 ? void 0 : _b.referralCode) !== null && _c !== void 0 ? _c : "").toString();
    const result = await applyReferralCodeToUser(uid, code);
    return result;
});
// -------- session end: submit results (idempotent) --------
exports.submitSessionResult = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d, _e, _f, _g, _h;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const p = req.data || {};
    const sessionId = (p.sessionId || "").toString().trim();
    const earnedCurrency = Number(p.earnedCurrency) || 0;
    const earnedScore = Number(p.earnedScore) || 0;
    // NEW: Game Mode & Result (for energy refund logic)
    // const mode = (p.mode || "endless").toString().trim().toLowerCase();
    // const success = !!p.success;
    if (!sessionId)
        throw new https_1.HttpsError("invalid-argument", "sessionId required");
    // Session doc ref
    const userRef = db.collection("users").doc(uid);
    const sessRef = userRef.collection("sessions").doc(sessionId);
    // Transaction for safe write
    const res = await db.runTransaction(async (tx) => {
        var _a, _b, _c, _d, _e, _f, _g;
        // 1) Session state check
        const sSnap = await tx.get(sessRef);
        if (!sSnap.exists) {
            // Session yoksa (belki cok eski veya creation fail oldu)
            return { alreadyProcessed: false, valid: false };
        }
        const sData = sSnap.data() || {};
        if (sData.state === "completed" || sData.processedAt) {
            // Already processed
            return { alreadyProcessed: true, valid: true };
        }
        // 2) User data read (for currency update & maxScore check)
        const uSnap = await tx.get(userRef);
        const prevMaxCombo = Number((_a = uSnap.get("maxCombo")) !== null && _a !== void 0 ? _a : 0) || 0;
        const prevSessions = Number((_b = uSnap.get("sessionsPlayed")) !== null && _b !== void 0 ? _b : 0) || 0;
        const prevCumEarn = Number((_c = uSnap.get("cumulativeCurrencyEarned")) !== null && _c !== void 0 ? _c : 0) || 0;
        const prevPlaySec = Number((_d = uSnap.get("totalPlaytimeSec")) !== null && _d !== void 0 ? _d : 0) || 0;
        const prevPups = Number((_e = uSnap.get("powerUpsCollected")) !== null && _e !== void 0 ? _e : 0) || 0;
        const prevCurrency = Number((_f = uSnap.get("currency")) !== null && _f !== void 0 ? _f : 0) || 0;
        const prevBest = Number((_g = uSnap.get("maxScore")) !== null && _g !== void 0 ? _g : 0) || 0;
        const playtimeSec = Number(p.playtimeSec) || 0;
        const powerUpsCollectedInSession = Number(p.powerUpsCollected) || 0;
        const maxComboInSession = Number(p.maxCombo) || 0;
        const newCurrency = prevCurrency + earnedCurrency;
        const newBest = Math.max(prevBest, earnedScore);
        const newSessions = prevSessions + 1;
        const newCumEarn = prevCumEarn + earnedCurrency;
        const newPlaySec = prevPlaySec + Math.max(0, Math.floor(playtimeSec));
        const newPlayMin = Math.floor(newPlaySec / 60);
        const newPlayMinFloat = newPlaySec / 60;
        const newPups = prevPups + Math.max(0, powerUpsCollectedInSession);
        const newMaxCombo = Math.max(prevMaxCombo, Math.max(0, maxComboInSession));
        tx.set(userRef, {
            currency: newCurrency,
            maxScore: newBest,
            sessionsPlayed: newSessions,
            cumulativeCurrencyEarned: newCumEarn,
            totalPlaytimeSec: newPlaySec,
            totalPlaytimeMinutes: newPlayMin,
            totalPlaytimeMinutesFloat: newPlayMinFloat,
            powerUpsCollected: newPups,
            maxCombo: newMaxCombo,
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
        // mark session as processed
        tx.set(sessRef, { state: "completed", earnedCurrency, earnedScore, processedAt: firestore_2.Timestamp.now() }, { merge: true });
        return { alreadyProcessed: false, currency: newCurrency, maxScore: newBest };
    });
    try {
        const u = await db.collection("users").doc(uid).get();
        await Promise.all([
            upsertUserAch(uid, ACH_TYPES.ENDLESS_ROLLER, Number((_b = u.get("sessionsPlayed")) !== null && _b !== void 0 ? _b : 0) || 0),
            upsertUserAch(uid, ACH_TYPES.SCORE_CHAMPION, Number((_c = u.get("maxScore")) !== null && _c !== void 0 ? _c : 0) || 0),
            upsertUserAch(uid, ACH_TYPES.TOKEN_HUNTER, Number((_d = u.get("cumulativeCurrencyEarned")) !== null && _d !== void 0 ? _d : 0) || 0),
            upsertUserAch(uid, ACH_TYPES.COMBO_GOD, Number((_e = u.get("maxCombo")) !== null && _e !== void 0 ? _e : 0) || 0),
            upsertUserAch(uid, ACH_TYPES.TIME_DRIFTER, Number((_g = (_f = u.get("totalPlaytimeMinutesFloat")) !== null && _f !== void 0 ? _f : u.get("totalPlaytimeMinutes")) !== null && _g !== void 0 ? _g : 0) || 0),
            upsertUserAch(uid, ACH_TYPES.POWERUP_EXP, Number((_h = u.get("powerUpsCollected")) !== null && _h !== void 0 ? _h : 0) || 0),
        ]);
    }
    catch (e) {
        console.warn("[ach] post-session evaluate failed", e);
    }
    return res;
});
// -------- Energy (lazy regen) helpers --------
async function lazyRegenInTx(tx, userRef, now) {
    var _a, _b, _c;
    const snap = await tx.get(userRef);
    if (!snap.exists)
        throw new https_1.HttpsError("failed-precondition", "User doc missing");
    const max = Number((_a = snap.get("energyMax")) !== null && _a !== void 0 ? _a : 6) || 6;
    const period = Number((_b = snap.get("energyRegenPeriodSec")) !== null && _b !== void 0 ? _b : 14400) || 14400; // seconds
    let cur = Number((_c = snap.get("energyCurrent")) !== null && _c !== void 0 ? _c : 0) || 0;
    let updatedAt = snap.get("energyUpdatedAt") || now;
    if (cur < max) {
        const elapsedMs = Math.max(0, now.toMillis() - updatedAt.toMillis());
        const ticks = Math.floor(elapsedMs / (period * 1000));
        if (ticks > 0) {
            const newCur = Math.min(max, cur + ticks);
            const newUpdated = firestore_2.Timestamp.fromMillis(updatedAt.toMillis() + ticks * period * 1000);
            tx.set(userRef, {
                energyCurrent: newCur, energyUpdatedAt: newUpdated,
                updatedAt: firestore_2.FieldValue.serverTimestamp()
            }, { merge: true });
            cur = newCur;
            updatedAt = newUpdated;
        }
    }
    const nextAt = cur < max
        ? firestore_2.Timestamp.fromMillis(updatedAt.toMillis() + (period * 1000))
        : null;
    return { cur, max, period, nextAt };
}
// -------- getEnergySnapshot (callable, preferred by clients) --------
exports.getEnergySnapshot = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const now = firestore_2.Timestamp.now();
    // Pre-clean expired consumables in a separate transaction
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });
    const st = await db.runTransaction(async (tx) => {
        return await lazyRegenInTx(tx, userRef, now);
    });
    const nextMs = st.cur < st.max
        ? st.nextAt ? st.nextAt.toMillis() : (firestore_2.Timestamp.fromMillis(now.toMillis() + st.period * 1000).toMillis())
        : null;
    return {
        ok: true,
        energyCurrent: st.cur,
        energyMax: st.max,
        regenPeriodSec: st.period,
        nextEnergyAtMillis: nextMs,
    };
});
// -------- getEnergyStatus (callable) --------
exports.getEnergyStatus = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const now = firestore_2.Timestamp.now();
    // Pre-clean expired consumables in a separate transaction
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });
    const st = await db.runTransaction(async (tx) => {
        return await lazyRegenInTx(tx, userRef, now);
    });
    return {
        ok: true,
        energyCurrent: st.cur,
        energyMax: st.max,
        regenPeriodSec: st.period,
        nextEnergyAt: st.nextAt ? st.nextAt.toDate().toISOString() : null,
        nextEnergyAtMillis: st.nextAt ? st.nextAt.toMillis() : null,
    };
});
// -------- spendEnergy (callable) --------
exports.spendEnergy = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const sessionId = String(((_b = req.data) === null || _b === void 0 ? void 0 : _b.sessionId) || "").trim();
    const userRef = db.collection("users").doc(uid);
    const now = firestore_2.Timestamp.now();
    // Pre-clean expired consumables in a separate transaction
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });
    const res = await db.runTransaction(async (tx) => {
        // idempotency (optional but cheap)
        if (sessionId) {
            const sRef = userRef.collection("energySpends").doc(sessionId);
            const sSnap = await tx.get(sRef);
            if (sSnap.exists) {
                const st0 = await lazyRegenInTx(tx, userRef, now);
                return {
                    alreadyProcessed: true, cur: st0.cur, max: st0.max,
                    period: st0.period, nextAt: st0.nextAt
                };
            }
        }
        const st = await lazyRegenInTx(tx, userRef, now);
        if (st.cur <= 0) {
            throw new https_1.HttpsError("failed-precondition", "Not enough energy");
        }
        const newCur = st.cur - 1;
        // spending resets the timer to now for a clean 4h window
        tx.set(userRef, {
            energyCurrent: newCur, energyUpdatedAt: now,
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
        if (sessionId) {
            tx.set(userRef.collection("energySpends").doc(sessionId), { spentAt: now }, { merge: true });
        }
        const nextAt = firestore_2.Timestamp.fromMillis(now.toMillis() + st.period * 1000);
        return {
            alreadyProcessed: false, cur: newCur, max: st.max,
            period: st.period, nextAt
        };
    });
    return {
        ok: true,
        alreadyProcessed: !!res.alreadyProcessed,
        energyCurrent: res.cur,
        energyMax: res.max,
        regenPeriodSec: res.period,
        nextEnergyAt: res.nextAt ? res.nextAt.toDate()
            .toISOString() : null,
    };
});
// -------- grantBonusEnergy (callable, +1 life via ad / reward) --------
exports.grantBonusEnergy = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    // İstersen burada ileride adToken / source vs. doğrulayabilirsin:
    // const adToken = String(req.data?.adToken || "");
    const userRef = db.collection("users").doc(uid);
    const now = firestore_2.Timestamp.now();
    // 1) Önce expired consumable'ları temizle (senin diğer energy fonksiyonlarınla aynı pattern)
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });
    // 2) Enerjiyi lazy regen + bonus life aynı transaction içinde
    const res = await db.runTransaction(async (tx) => {
        const st = await lazyRegenInTx(tx, userRef, now); // READ (tx.get içeriyor)
        // Zaten full ise hiçbir şey verme (idempotent / no-op)
        if (st.cur >= st.max) {
            return {
                granted: 0,
                cur: st.cur,
                max: st.max,
                period: st.period,
                nextAt: st.nextAt,
            };
        }
        const newCur = Math.min(st.max, st.cur + 1);
        const granted = newCur - st.cur; // normalde 1
        // Bonus life verirken energyUpdatedAt'e dokunmuyoruz:
        // regen zamanlaması aynı kalıyor, sadece fazladan 1 life eklenmiş oluyor.
        tx.set(userRef, {
            energyCurrent: newCur,
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
        }, { merge: true }); // WRITE
        // nextAt: lazyRegenInTx zaten hesapladı; full olduysa null olabilir
        const nextAt = newCur < st.max
            ? st.nextAt ||
                firestore_2.Timestamp.fromMillis(now.toMillis() + st.period * 1000)
            : null;
        return { granted, cur: newCur, max: st.max, period: st.period, nextAt };
    });
    const nextMs = res.nextAt ? res.nextAt.toMillis() : null;
    return {
        ok: true,
        grantedEnergy: res.granted,
        energyCurrent: res.cur,
        energyMax: res.max,
        regenPeriodSec: res.period,
        nextEnergyAtMillis: nextMs,
    };
});
// ========================= Autopilot (AFK) =========================
// Global config at appdata/autopilotconfig:
//  - normalUserEarningPerHour:number
//  - eliteUserEarningPerHour:number
//  - normalUserMaxAutopilotDurationInHours:number
// User state at users/{uid}/autopilot:
//  - autopilotWallet:number
//  - isAutopilotOn:boolean
//  - autopilotActivationDate:number|null (ms)
//  - autopilotLastClaimedAt:number (ms)
//  - totalEarnedViaAutopilot:number
//  - updatedAt:serverTimestamp
const autopilotConfigRef = db.collection("appdata").doc("autopilotconfig");
const userAutopilotRef = (uid) => db.collection("users").doc(uid).collection("autopilot").doc("state");
function clampNum(n, def = 0) {
    const v = Number(n);
    return Number.isFinite(v) ? v : def;
}
function eliteActive(userSnap, nowMs) {
    const ts = userSnap.get("elitePassExpiresAt");
    return !!ts && ts.toMillis() > nowMs;
}
/**
 * Lazy settlement for Autopilot.
 * - Works for both Normal and Elite (both accrue into autopilotWallet)
 * - Normal is capped by maxHours; Elite has no cap
 * - Uses windowStart = max(autopilotLastClaimedAt, autopilotActivationDate) when ON
 */
async function settleAutopilotInTx(tx, uid, now) {
    const userRef = db.collection("users").doc(uid);
    const autoRef = userAutopilotRef(uid);
    const [cfgSnap, userSnap, autoSnap] = await Promise.all([
        tx.get(autopilotConfigRef),
        tx.get(userRef),
        tx.get(autoRef),
    ]);
    if (!userSnap.exists)
        throw new https_1.HttpsError("failed-precondition", "User doc missing");
    if (!cfgSnap.exists)
        throw new https_1.HttpsError("failed-precondition", "autopilotconfig missing");
    const config = {
        normalRate: clampNum(cfgSnap.get("normalUserEarningPerHour"), 0),
        eliteRate: clampNum(cfgSnap.get("eliteUserEarningPerHour"), 0),
        maxHours: Math.max(0, clampNum(cfgSnap.get("normalUserMaxAutopilotDurationInHours"), 12)),
    };
    const nowMs = now.toMillis();
    const isElite = eliteActive(userSnap, nowMs);
    const userData = userSnap.data() || {};
    const curCurrency = clampNum(userData.currency, 0);
    let auto = autoSnap.exists ? (autoSnap.data() || {}) : {};
    if (!autoSnap.exists) {
        auto = {
            autopilotWallet: 0,
            isAutopilotOn: false,
            autopilotActivationDate: null,
            autopilotLastClaimedAt: nowMs,
            totalEarnedViaAutopilot: 0,
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
        };
        tx.set(autoRef, auto, { merge: true });
    }
    let gained = 0;
    if (isElite || auto.isAutopilotOn === true) {
        const lastClaim = clampNum(auto.autopilotLastClaimedAt, nowMs);
        let activation = null;
        if (isElite && auto.autopilotActivationDate === null) {
            activation = lastClaim;
        }
        else {
            activation = auto.autopilotActivationDate === null ? lastClaim : clampNum(auto.autopilotActivationDate, lastClaim);
        }
        const windowStart = Math.max(lastClaim, activation);
        const elapsedMs = Math.max(0, nowMs - windowStart);
        const ratePerHour = isElite ? config.eliteRate : config.normalRate;
        const potentialRaw = ratePerHour * (elapsedMs / 3600000);
        const potential = Math.floor(potentialRaw * 100) / 100; // accrue with 2-decimal precision
        if (potential > 0) {
            if (isElite) {
                // No cap for elite
                auto.autopilotWallet = clampNum(auto.autopilotWallet, 0) + potential;
                gained = potential;
            }
            else {
                // Cap for normal
                const capGainRaw = Math.max(0, config.normalRate * config.maxHours);
                const capGain = Math.floor(capGainRaw * 100) / 100; // cap with 2-decimal precision
                const current = clampNum(auto.autopilotWallet, 0);
                const newWallet = Math.min(current + potential, capGain);
                gained = Math.max(0, newWallet - current);
                auto.autopilotWallet = newWallet;
            }
            auto.totalEarnedViaAutopilot = clampNum(auto.totalEarnedViaAutopilot, 0) + gained;
            auto.updatedAt = firestore_2.FieldValue.serverTimestamp();
            // We do NOT advance autopilotLastClaimedAt here; it only moves on claim.
            tx.set(autoRef, auto, { merge: true });
        }
    }
    return { userRef, autoRef, userData: Object.assign(Object.assign({}, userData), { currency: curCurrency }), auto, config, isElite };
}
// -------- getAutopilotStatus (callable) --------
exports.getAutopilotStatus = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const now = firestore_2.Timestamp.now();
    // Run settlement in a tx to keep reads/writes ordered
    const out = await db.runTransaction(async (tx) => {
        const s = await settleAutopilotInTx(tx, uid, now);
        // Compute helpers for payload
        const wallet = clampNum(s.auto.autopilotWallet, 0);
        const capSec = Math.floor(s.config.normalRate > 0 ? s.config.maxHours * 3600 : 0);
        const lastClaimMs = clampNum(s.auto.autopilotLastClaimedAt, now.toMillis());
        const activationMs = typeof s.auto.autopilotActivationDate === 'number' ? s.auto.autopilotActivationDate : null;
        const windowStartMs = activationMs !== null ? Math.max(lastClaimMs, activationMs) : lastClaimMs;
        let timeToCapSeconds = null;
        let isClaimReady = false;
        if (s.isElite) {
            timeToCapSeconds = null; // Elite has no time cap
            isClaimReady = true; // Elite can claim anytime
        }
        else {
            const elapsedSec = Math.max(0, Math.floor((now.toMillis() - windowStartMs) / 1000));
            timeToCapSeconds = capSec > 0 ? Math.max(0, capSec - elapsedSec) : 0;
            isClaimReady = capSec > 0 ? (elapsedSec >= capSec) : false;
        }
        return {
            isElite: s.isElite,
            isAutopilotOn: !!s.auto.isAutopilotOn,
            autopilotWallet: wallet,
            currency: clampNum(s.userData.currency, 0),
            normalUserEarningPerHour: s.config.normalRate,
            eliteUserEarningPerHour: s.config.eliteRate,
            normalUserMaxAutopilotDurationInHours: s.config.maxHours,
            autopilotActivationDateMillis: typeof s.auto.autopilotActivationDate === 'number' ? s.auto.autopilotActivationDate : null,
            autopilotLastClaimedAtMillis: clampNum(s.auto.autopilotLastClaimedAt, 0),
            timeToCapSeconds,
            isClaimReady,
        };
    });
    return Object.assign({ ok: true, serverNowMillis: now.toMillis() }, out);
});
// -------- toggleAutopilot (callable) --------
exports.toggleAutopilot = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const on = !!((_b = req.data) === null || _b === void 0 ? void 0 : _b.on);
    const now = firestore_2.Timestamp.now();
    const userRef = db.collection("users").doc(uid);
    const autoRef = userAutopilotRef(uid);
    await db.runTransaction(async (tx) => {
        // First settle current window so we don't lose any elapsed time
        await settleAutopilotInTx(tx, uid, now);
        if (on) {
            tx.set(autoRef, {
                isAutopilotOn: true,
                autopilotActivationDate: now.toMillis(),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            }, { merge: true });
        }
        else {
            // Turning OFF: close the window by simply disabling and clearing activation
            tx.set(autoRef, {
                isAutopilotOn: false,
                autopilotActivationDate: null,
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            }, { merge: true });
        }
        // Touch user updatedAt for visibility
        tx.set(userRef, { updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
    });
    return { ok: true, isAutopilotOn: on };
});
// -------- claimAutopilot (callable) --------
exports.claimAutopilot = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const now = firestore_2.Timestamp.now();
    const userRef = db.collection("users").doc(uid);
    const autoRef = userAutopilotRef(uid);
    // PHASE 1: run settlement in its own transaction (reads -> writes, then exit)
    await db.runTransaction(async (tx) => {
        await settleAutopilotInTx(tx, uid, now);
    });
    // PHASE 2: do the claim in a clean transaction, with all reads first, then writes
    const res = await db.runTransaction(async (tx) => {
        // === READS (all of them, first) ===
        const [uSnap, aSnap, configSnap] = await Promise.all([
            tx.get(userRef),
            tx.get(autoRef),
            tx.get(autopilotConfigRef),
        ]);
        if (!uSnap.exists || !aSnap.exists) {
            throw new https_1.HttpsError("failed-precondition", "Missing user/autopilot state");
        }
        const curCurrency = clampNum(uSnap.get("currency"), 0);
        const wallet = clampNum(aSnap.get("autopilotWallet"), 0);
        const nowMs = now.toMillis();
        const isElite = eliteActive(uSnap, nowMs);
        if (!isElite) {
            if (!configSnap.exists) {
                throw new https_1.HttpsError("failed-precondition", "autopilotconfig missing");
            }
            const normalRate = clampNum(configSnap.get("normalUserEarningPerHour"), 0);
            const maxHours = Math.max(0, clampNum(configSnap.get("normalUserMaxAutopilotDurationInHours"), 12));
            // time-based readiness check for normal users
            const capSec = Math.floor((normalRate > 0 ? maxHours : 0) * 3600);
            const lastClaimMs = clampNum(aSnap.get("autopilotLastClaimedAt"), now.toMillis());
            const activationMs = typeof aSnap.get("autopilotActivationDate") === "number"
                ? aSnap.get("autopilotActivationDate")
                : null;
            const windowStartMs = activationMs !== null ? Math.max(lastClaimMs, activationMs) : lastClaimMs;
            const elapsedSec = Math.max(0, Math.floor((now.toMillis() - windowStartMs) / 1000));
            if (capSec > 0 && elapsedSec < capSec) {
                throw new https_1.HttpsError("failed-precondition", "Not ready to claim");
            }
        }
        // === WRITES (after reads) ===
        if (wallet > 0) {
            tx.set(userRef, { currency: curCurrency + wallet, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
            const baseUpdate = {
                autopilotWallet: 0,
                autopilotLastClaimedAt: now.toMillis(),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            };
            if (!isElite) {
                baseUpdate.isAutopilotOn = false;
                baseUpdate.autopilotActivationDate = null;
            }
            tx.set(autoRef, baseUpdate, { merge: true });
        }
        else {
            const baseUpdate = {
                autopilotLastClaimedAt: now.toMillis(),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            };
            if (!isElite) {
                baseUpdate.isAutopilotOn = false;
                baseUpdate.autopilotActivationDate = null;
            }
            tx.set(autoRef, baseUpdate, { merge: true });
        }
        return { claimed: wallet, currencyAfter: curCurrency + wallet };
    });
    return { ok: true, claimed: res.claimed, currencyAfter: res.currencyAfter };
});
exports.listReferredUsers = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d, _e;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const limit = Math.max(1, Math.min(Number((_c = (_b = req.data) === null || _b === void 0 ? void 0 : _b.limit) !== null && _c !== void 0 ? _c : 100), 500));
    const includeEarnings = !!((_d = req.data) === null || _d === void 0 ? void 0 : _d.includeEarnings);
    const usersCol = db.collection("users");
    const mapDoc = async (doc) => {
        var _a, _b, _c, _d, _e, _f;
        const d = doc.data() || {};
        const childUid = doc.id;
        const username = (typeof d.username === "string" ? d.username : "").trim() || "Guest";
        const currency = typeof d.currency === "number" ? d.currency : 0;
        const createdAt = (_c = (_b = (_a = d.createdAt) === null || _a === void 0 ? void 0 : _a.toDate()) === null || _b === void 0 ? void 0 : _b.toISOString()) !== null && _c !== void 0 ? _c : null;
        const referralAppliedAt = (_f = (_e = (_d = d.referralAppliedAt) === null || _d === void 0 ? void 0 : _d.toDate()) === null || _e === void 0 ? void 0 : _e.toISOString()) !== null && _f !== void 0 ? _f : null;
        let earnedTotal = 0;
        if (includeEarnings) {
            const rcRef = usersCol
                .doc(uid)
                .collection("referralsChildren")
                .doc(childUid);
            const rcSnap = await rcRef.get();
            if (rcSnap.exists) {
                const et = rcSnap.get("earnedTotal");
                earnedTotal = typeof et === "number" ? et : 0;
            }
        }
        return {
            uid: childUid,
            username,
            currency,
            createdAt,
            referralAppliedAt,
            earnedTotal,
        };
    };
    try {
        const q = usersCol
            .where("referredByUid", "==", uid)
            .orderBy("createdAt", "desc")
            .limit(limit);
        const snap = await q.get();
        const items = await Promise.all(snap.docs.map(mapDoc));
        return { ok: true, sorted: true, items };
    }
    catch (err) {
        const msg = String((_e = err === null || err === void 0 ? void 0 : err.message) !== null && _e !== void 0 ? _e : "");
        const needsIndex = (err === null || err === void 0 ? void 0 : err.code) === 9 || /index/i.test(msg);
        if (!needsIndex) {
            console.error("listReferredUsers error:", err);
            throw new https_1.HttpsError("internal", "Query failed.");
        }
        // Fallback: same filter without orderBy (no composite index needed)
        try {
            const snap = await usersCol
                .where("referredByUid", "==", uid)
                .limit(limit)
                .get();
            const items = await Promise.all(snap.docs.map(mapDoc));
            return {
                ok: true,
                sorted: false,
                note: "Add composite index on users: referredByUid ASC, createdAt DESC (database 'getfi') to enable sorted results.",
                items,
            };
        }
        catch (e2) {
            console.error("listReferredUsers fallback error:", e2);
            throw new https_1.HttpsError("internal", "Query failed (fallback).");
        }
    }
});
exports.getGalleryItems = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const collectionPath = ((_a = req.data) === null || _a === void 0 ? void 0 : _a.collectionPath) ||
        "appdata/galleryitems/itemdata";
    let ids = Array.isArray((_b = req.data) === null || _b === void 0 ? void 0 : _b.ids)
        ? req.data.ids.slice(0, 10).map((x) => String(x))
        : ["galleryview_1", "galleryview_2", "galleryview_3"];
    if (!collectionPath.startsWith("appdata/")) {
        throw new https_1.HttpsError("permission-denied", "Invalid collectionPath");
    }
    const pickString = (obj, keys) => {
        for (const k of keys) {
            const v = obj === null || obj === void 0 ? void 0 : obj[k];
            if (typeof v === "string" && v.trim().length > 0)
                return v;
        }
        return "";
    };
    try {
        const col = db.collection(collectionPath);
        const snaps = await Promise.all(ids.map((id) => col.doc(id).get()));
        const items = snaps
            .filter((s) => s.exists)
            .map((s) => {
            const d = s.data() || {};
            // Çoklu isim varyasyonları:
            const pngUrl = pickString(d, [
                "png_url",
                "pngUrl",
                "image_url",
                "imageUrl",
                "url",
            ]);
            const descriptionText = pickString(d, [
                "description_text",
                "descriptionText",
                "desc",
                "text",
            ]);
            const guidanceKey = pickString(d, [
                "guidance_string_key",
                "guidanceKey",
                "guidance",
                "action",
            ]);
            // Sunucu logu (debug)
            console.log(`[getGalleryItems] ${s.id} pngUrl='${pngUrl}' guidance='${guidanceKey}' descLen=${descriptionText.length}`);
            return {
                id: s.id,
                pngUrl,
                descriptionText,
                guidanceKey,
            };
        });
        return { ok: true, items };
    }
    catch (e) {
        console.error("getGalleryItems error:", e);
        throw new https_1.HttpsError("internal", "Failed to fetch gallery items.");
    }
});
exports.indexMapMeta = (0, firestore_1.onDocumentWritten)({ document: "appdata/maps/{mapId}/raw", database: DB_ID }, async (event) => {
    var _a, _b, _c;
    const mapId = event.params.mapId;
    // If the raw doc was deleted -> remove from pools and meta
    if (!((_b = (_a = event.data) === null || _a === void 0 ? void 0 : _a.after) === null || _b === void 0 ? void 0 : _b.exists)) {
        const metaDoc = db.collection("appdata").doc("maps_meta");
        const byMapRef = metaDoc.collection("by_map").doc(mapId);
        const poolsCol = metaDoc.collection("pools");
        await db.runTransaction(async (tx) => {
            tx.delete(byMapRef);
            for (const d of [1, 2, 3]) {
                const pRef = poolsCol.doc(String(d));
                tx.set(pRef, {
                    ids: firestore_2.FieldValue.arrayRemove(mapId),
                    updatedAt: firestore_2.FieldValue.serverTimestamp(),
                }, { merge: true });
            }
        });
        console.log(`[indexMapMeta] raw deleted: ${mapId}`);
        return;
    }
    // Created/Updated: parse difficultyTag from the single string field
    const after = event.data.after.data() || {};
    let jsonStr = "";
    if (typeof after.json === "string") {
        jsonStr = after.json;
    }
    else {
        const keys = Object.keys(after);
        if (keys.length === 1 && typeof after[keys[0]] === "string") {
            jsonStr = after[keys[0]];
        }
    }
    if (!jsonStr) {
        console.warn(`[indexMapMeta] '${mapId}' has no single string field to parse`);
        return;
    }
    let difficultyTag = 0;
    try {
        const parsed = JSON.parse(jsonStr);
        const d = Number((_c = parsed === null || parsed === void 0 ? void 0 : parsed.difficultyTag) !== null && _c !== void 0 ? _c : 0);
        if ([1, 2, 3].includes(d))
            difficultyTag = d;
    }
    catch (e) {
        console.error(`[indexMapMeta] JSON parse error for '${mapId}':`, e);
        return;
    }
    if (!difficultyTag) {
        console.warn(`[indexMapMeta] '${mapId}' missing/invalid difficultyTag`);
        return;
    }
    const metaDoc = db.collection("appdata").doc("maps_meta");
    const byMapRef = metaDoc.collection("by_map").doc(mapId);
    const poolsCol = metaDoc.collection("pools");
    const now = firestore_2.Timestamp.now();
    await db.runTransaction(async (tx) => {
        var _a;
        // Upsert by_map/{mapId}
        const prev = await tx.get(byMapRef);
        const prevDiff = prev.exists
            ? Number((_a = prev.get("difficultyTag")) !== null && _a !== void 0 ? _a : 0)
            : 0;
        tx.set(byMapRef, Object.assign({ difficultyTag, updatedAt: firestore_2.FieldValue.serverTimestamp() }, (prev.exists ? {} : { createdAt: now })), { merge: true });
        // Maintain pools/{1|2|3}.ids arrays
        for (const d of [1, 2, 3]) {
            const pRef = poolsCol.doc(String(d));
            if (d === difficultyTag) {
                tx.set(pRef, {
                    ids: firestore_2.FieldValue.arrayUnion(mapId),
                    updatedAt: firestore_2.FieldValue.serverTimestamp(),
                }, { merge: true });
            }
            else if (prevDiff && d === prevDiff) {
                tx.set(pRef, {
                    ids: firestore_2.FieldValue.arrayRemove(mapId),
                    updatedAt: firestore_2.FieldValue.serverTimestamp(),
                }, { merge: true });
            }
            else {
                // no-op
            }
        }
    });
    console.log(`[indexMapMeta] indexed '${mapId}' diff=${difficultyTag}`);
});
// ---------------- Sequenced Random Maps (callable) ----------------
exports.getSequencedMaps = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d, _e, _f, _g, _h, _j, _k;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const clamp = (n, a, b) => Math.max(a, Math.min(b, n));
    const count = clamp(Number((_c = (_b = req.data) === null || _b === void 0 ? void 0 : _b.count) !== null && _c !== void 0 ? _c : 24), 1, 50);
    const seedIn = String((_e = (_d = req.data) === null || _d === void 0 ? void 0 : _d.seed) !== null && _e !== void 0 ? _e : "");
    // tiny deterministic RNG (xorshift32-like) from a seed string
    const makeRng = (s) => {
        let h = 2166136261 >>> 0;
        for (let i = 0; i < s.length; i++) {
            h ^= s.charCodeAt(i);
            h = Math.imul(h, 16777619) >>> 0;
        }
        return () => {
            h ^= h << 13;
            h >>>= 0;
            h ^= h >>> 17;
            h >>>= 0;
            h ^= h << 5;
            h >>>= 0;
            return (h >>> 0) / 4294967296;
        };
    };
    const rng = seedIn ? makeRng(seedIn) : Math.random;
    // 1) read recurring difficulty curve
    const curveRef = db.collection("appdata").doc("recurring_difficulty_curve");
    const curveSnap = await curveRef.get();
    const curve = curveSnap.exists ? (_f = curveSnap.data()) !== null && _f !== void 0 ? _f : {} : {};
    const easy = clamp(Number((_g = curve.easy) !== null && _g !== void 0 ? _g : 3), 0, 24);
    const medium = clamp(Number((_h = curve.medium) !== null && _h !== void 0 ? _h : 2), 0, 24);
    const hard = clamp(Number((_j = curve.hard) !== null && _j !== void 0 ? _j : 1), 0, 24);
    const cycle = [
        ...Array(easy).fill(1),
        ...Array(medium).fill(2),
        ...Array(hard).fill(3),
    ];
    if (cycle.length === 0) {
        throw new https_1.HttpsError("failed-precondition", "Empty pattern");
    }
    const pattern = [];
    while (pattern.length < count) {
        for (const d of cycle) {
            if (pattern.length >= count)
                break;
            pattern.push(d);
        }
    }
    // 2) load pools
    const poolsCol = db.collection("appdata")
        .doc("maps_meta").collection("pools");
    const [p1, p2, p3] = await Promise.all([1, 2, 3].map(async (d) => {
        const s = await poolsCol.doc(String(d)).get();
        const ids = (s.exists ? s.get("ids") : []) || [];
        // copy and shallow-shuffle for variety
        const arr = ids.slice();
        for (let i = arr.length - 1; i > 0; i--) {
            const j = Math.floor((rng() || Math.random()) * (i + 1));
            const t = arr[i];
            arr[i] = arr[j];
            arr[j] = t;
        }
        return { d, ids, bag: arr };
    }));
    const poolByDiff = {
        1: p1, 2: p2, 3: p3
    };
    // helper to take one id from pool; allows repeats if exhausted
    const takeFrom = (d) => {
        const p = poolByDiff[d];
        if (!p)
            throw new https_1.HttpsError("not-found", `Pool ${d} missing`);
        if (p.bag.length === 0) {
            if (p.ids.length === 0) {
                throw new https_1.HttpsError("not-found", `Pool ${d} is empty`);
            }
            // refill (allow repeats after exhaustion)
            p.bag = p.ids.slice();
        }
        return p.bag.pop();
    };
    // 3) pick ids by pattern
    const chosen = [];
    for (const d of pattern) {
        const id = takeFrom(d);
        chosen.push({ mapId: id, difficultyTag: d });
    }
    // 4) fetch raw jsons in parallel (dedup reads)
    const uniqueIds = Array.from(new Set(chosen.map(x => x.mapId)));
    const rawCol = db.collection("appdata").doc("maps");
    const snaps = await Promise.all(uniqueIds.map(id => rawCol.collection(id).doc("raw").get()));
    const byId = {};
    for (let i = 0; i < uniqueIds.length; i++) {
        const id = uniqueIds[i];
        const s = snaps[i];
        if (!s.exists) {
            throw new https_1.HttpsError("not-found", `raw missing: ${id}`);
        }
        const d = (_k = s.data()) !== null && _k !== void 0 ? _k : {};
        let jsonStr = "";
        if (typeof d.json === "string") {
            jsonStr = d.json;
        }
        else {
            const ks = Object.keys(d);
            if (ks.length === 1 && typeof d[ks[0]] === "string") {
                jsonStr = d[ks[0]];
            }
        }
        if (typeof jsonStr !== "string" || jsonStr.length === 0) {
            throw new https_1.HttpsError("data-loss", `raw.json not string for ${id}`);
        }
        byId[id] = jsonStr;
    }
    // 5) assemble entries
    const entries = chosen.map(x => {
        const js = byId[x.mapId];
        if (typeof js !== "string") {
            throw new https_1.HttpsError("data-loss", `json not string for ${x.mapId}`);
        }
        return { mapId: x.mapId, difficultyTag: x.difficultyTag, json: js };
    });
    console.log(`[getSequencedMaps] uid=${uid} count=${count} pat=${pattern.join("")}`);
    return {
        ok: true,
        count,
        pattern,
        entries
    };
});
// -------- requestSession (callable) --------
exports.requestSession = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const now = firestore_2.Timestamp.now();
    // Pre-clean expired consumables in a separate transaction
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });
    const out = await db.runTransaction(async (tx) => {
        // 1) lazy regen inside tx
        const st = await lazyRegenInTx(tx, userRef, now);
        if (st.cur <= 0) {
            throw new https_1.HttpsError("failed-precondition", "Not enough energy");
        }
        // 2) spend 1 energy and reset timer window to now
        const newCur = st.cur - 1;
        tx.set(userRef, {
            energyCurrent: newCur, energyUpdatedAt: now,
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
        // 3) create server-owned session doc
        const sessionId = `${now.toMillis()}_${Math.random().toString(36).slice(2, 10)}`;
        const sessRef = userRef.collection("sessions").doc(sessionId);
        tx.set(sessRef, { state: "granted", startedAt: now }, { merge: true });
        const nextAt = firestore_2.Timestamp.fromMillis(now.toMillis() + st.period * 1000);
        return {
            sessionId, energyCurrent: newCur, energyMax: st.max,
            regenPeriodSec: st.period, nextEnergyAt: nextAt
        };
    });
    const nextMs = out.nextEnergyAt ? out.nextEnergyAt.toMillis() : null;
    const resp = {
        ok: true,
        sessionId: out.sessionId,
        energyCurrent: out.energyCurrent,
        energyMax: out.energyMax,
        regenPeriodSec: out.regenPeriodSec,
        nextEnergyAtMillis: nextMs,
    };
    console.log("requestSession returning", resp);
    return resp;
});
// ========================= getAllItems =========================
// /appdata/items/... altındaki tüm itemları JSON string olarak döndürür.
exports.getAllItems = (0, https_1.onCall)(async (request) => {
    try {
        console.log(`[getAllItems:start] db=${DB_ID}`);
        const itemsCol = db.collection("appdata").doc("items");
        const itemsSnap = await itemsCol.listCollections();
        const out = {};
        for (const subCol of itemsSnap) {
            const docSnap = await subCol.doc("itemdata").get();
            if (!docSnap.exists)
                continue;
            out[subCol.id] = docSnap.data();
        }
        const sample = Object.keys(out).slice(0, 5);
        console.log(`[getAllItems:done] count=${Object.keys(out).length} sample=[${sample.join(', ')}]`);
        return {
            ok: true,
            items: out,
            count: Object.keys(out).length,
        };
    }
    catch (err) {
        console.error("[getAllItems] error", err);
        return {
            ok: false,
            error: err.message || "unknown",
        };
    }
});
// ========================= createItem =========================
// Unity Editor'dan (Odin butonuyla) çağırmak için:
// Callable name: createItem
// Path: appdata/items/{itemId}/itemdata  (itemId = "item_" + slug(itemName))
exports.createItem = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const p = req.data || {};
    // ---- helper'lar
    const num = (v, def = 0) => Number.isFinite(Number(v)) ? Number(v) : def;
    const str = (v, def = "") => typeof v === "string" ? v : def;
    const bool = (v, def = false) => typeof v === "boolean" ? v : !!def;
    const itemName = str(p.itemName, "itemname (demo)").trim();
    // "item_<slug>"
    const slug = itemName
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "_")
        .replace(/^_+|_+$/g, "");
    const baseId = `item_${slug || "noname"}`;
    // yazılacak veri (tüm alanlar)
    const docData = {
        itemDescription: str(p.itemDescription, "item description demo"),
        itemDollarPrice: num(p.itemDollarPrice, 0),
        itemGetPrice: num(p.itemGetPrice, 0.05),
        itemPremiumPrice: num(p.itemPremiumPrice, 0),
        itemIconUrl: str(p.itemIconUrl, "https://cdn-icons-png.freepik.com/256/4957/4957671.png"),
        itemIsConsumable: bool(p.itemIsConsumable, false),
        itemIsRewardedAd: bool(p.itemIsRewardedAd, false),
        itemName,
        itemReferralThreshold: num(p.itemReferralThreshold, 0),
        itemstat_coinMultiplierPercent: num(p.itemstat_coinMultiplierPercent, 0),
        itemstat_comboPower: num(p.itemstat_comboPower, 0),
        itemstat_gameplaySpeedMultiplierPercent: num(p.itemstat_gameplaySpeedMultiplierPercent, 0),
        itemstat_magnetPowerPercent: num(p.itemstat_magnetPowerPercent, 0),
        itemstat_playerAcceleration: num(p.itemstat_playerAcceleration, 0),
        itemstat_playerSizePercent: num(p.itemstat_playerSizePercent, 0),
        itemstat_playerSpeed: num(p.itemstat_playerSpeed, 0),
        createdAt: firestore_2.Timestamp.now(),
        updatedAt: firestore_2.FieldValue.serverTimestamp(),
        createdBy: uid,
    };
    // Aynı isim varsa çakışmayı çöz: baseId, sonra kısa random ekle
    let itemId = baseId;
    for (let attempt = 0; attempt < 5; attempt++) {
        const ref = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
        const snap = await ref.get();
        if (!snap.exists) {
            await ref.set(docData, { merge: false });
            return {
                ok: true,
                itemId,
                path: `appdata/items/${itemId}/itemdata`,
            };
        }
        // çakıştı; yeni bir ek kuyruk dene
        itemId = `${baseId}_${Math.random().toString(36).slice(2, 6)}`;
    }
    throw new https_1.HttpsError("aborted", "Could not allocate a unique itemId after several attempts.");
});
// ========================= checkOwnership =========================
// Kullanıcının sahip olduğu item ID'lerini döndürür (normalize edilerek).
// Sahiplik kriteri: owned == true  **veya** quantity > 0  (consumable destekli)
exports.checkOwnership = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    console.log(`[checkOwnership:start] uid=${uid} db=${DB_ID}`);
    const userRef = db.collection("users").doc(uid);
    const invCol = userRef.collection("inventory");
    // 1) owned=true olanlar
    const ownedQ = await invCol.where("owned", "==", true).get();
    // 2) quantity>0 olanlar (consumable'lar için)
    // Not: Firestore 'OR' desteklemediği için iki sorguyu birleştiriyoruz.
    let qtyQDocs = [];
    try {
        const qtyQ = await invCol.where("quantity", ">", 0).get();
        qtyQDocs = qtyQ.docs;
    }
    catch (e) {
        // quantity alanı yoksa da sorun değil; sadece owned=true setine güveniriz.
        qtyQDocs = [];
    }
    // 3) Birleştir + normalize et + tekilleştir
    const set = new Set();
    ownedQ.forEach(d => set.add(normId(d.id)));
    qtyQDocs.forEach(d => set.add(normId(d.id)));
    // 4) İsteğe bağlı: equipped listesi de dönmek istersen ileride buraya eklenebilir.
    const itemIds = Array.from(set.values()).sort();
    console.log(`[checkOwnership:done] uid=${uid} ownedQ=${ownedQ.size} qtyQ=${qtyQDocs.length} count=${itemIds.length}`);
    return {
        ok: true,
        count: itemIds.length,
        itemIds,
    };
});
// ========================= Inventory System =========================
exports.getInventorySnapshot = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const invCol = userRef.collection("inventory");
    const loadRef = userRef.collection("loadout").doc("current");
    const [invSnap, loadSnap] = await Promise.all([
        invCol.get(),
        loadRef.get()
    ]);
    console.log(`[getInventorySnapshot] uid=${uid} invDocs=${invSnap.size} loadoutDoc=${loadSnap.exists} db=${DB_ID}`);
    const inventory = {};
    invSnap.forEach((d) => {
        const id = normId(d.id);
        // include normalized id in payload for clients if needed
        inventory[id] = Object.assign({ id }, d.data());
    });
    const equippedItemIds = loadSnap.exists
        ? (loadSnap.get("equippedItemIds") || []).map((x) => normId(x))
        : [];
    return { ok: true, inventory, equippedItemIds };
});
// ---------------- purchaseItem ----------------
exports.purchaseItem = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    // method: "GET" | "IAP" | "AD" | "PREMIUM"
    const { itemId: rawItemId, method, platform, receipt, orderId, adToken, } = (req.data || {});
    const itemId = normId(rawItemId);
    if (!itemId)
        throw new https_1.HttpsError("invalid-argument", "itemId required.");
    const m = String(method || "").toUpperCase();
    if (!["GET", "IAP", "AD", "PREMIUM"].includes(m)) {
        throw new https_1.HttpsError("invalid-argument", "Invalid method. Use GET | IAP | AD | PREMIUM.");
    }
    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const acRef = userRef.collection("activeConsumables").doc(itemId);
    const now = firestore_2.Timestamp.now();
    // ---- Pre-verify tokens outside transaction to avoid granting on failed verification ----
    const verifyIapReceipt = async (platform, receipt, orderId) => {
        if (!platform || !receipt || !orderId) {
            throw new https_1.HttpsError("invalid-argument", "platform, receipt and orderId are required for IAP.");
        }
        const lockRef = userRef.collection("iapReceipts").doc(orderId);
        const existing = await lockRef.get();
        if (existing.exists) {
            throw new https_1.HttpsError("already-exists", "This purchase receipt was already processed.");
        }
        await lockRef.set({ usedAt: now, platform, previewHash: String(receipt).slice(0, 32) }, { merge: true });
    };
    const verifyAdGrant = async (adToken) => {
        if (!adToken)
            throw new https_1.HttpsError("invalid-argument", "adToken required for AD method.");
        const grantRef = userRef.collection("adGrants").doc(adToken);
        const g = await grantRef.get();
        if (g.exists) {
            throw new https_1.HttpsError("already-exists", "This ad grant token was already used.");
        }
        await grantRef.set({ usedAt: now }, { merge: true });
    };
    if (m === "IAP") {
        await verifyIapReceipt(platform, receipt, orderId);
    }
    else if (m === "AD") {
        await verifyAdGrant(adToken);
    }
    const res = await db.runTransaction(async (tx) => {
        var _a, _b, _c, _d, _e, _f, _g, _h, _j, _k, _l;
        // ---- READS FIRST (all of them) ----
        const [itemSnap, userSnap, invSnap, acSnap] = await Promise.all([
            tx.get(itemRef),
            tx.get(userRef),
            tx.get(invRef),
            tx.get(acRef),
        ]);
        if (!itemSnap.exists)
            throw new https_1.HttpsError("not-found", "Item not found.");
        const item = itemSnap.data() || {};
        const isReferralOnly = Number((_a = item.itemReferralThreshold) !== null && _a !== void 0 ? _a : 0) > 0;
        if (isReferralOnly) {
            throw new https_1.HttpsError("failed-precondition", "Referral-only item cannot be purchased.");
        }
        const isConsumable = !!item.itemIsConsumable;
        const priceGet = Number((_b = item.itemGetPrice) !== null && _b !== void 0 ? _b : 0) || 0; // in-game currency
        const pricePremium = Number((_c = item.itemPremiumPrice) !== null && _c !== void 0 ? _c : 0) || 0; // premium currency
        const priceUsd = Number((_d = item.itemDollarPrice) !== null && _d !== void 0 ? _d : 0) || 0; // real money price hint
        const isAd = !!item.itemIsRewardedAd;
        // Validate method vs item flags
        if (m === "GET" && priceGet <= 0) {
            throw new https_1.HttpsError("failed-precondition", "This item is not purchasable with game currency.");
        }
        if (m === "IAP" && priceUsd <= 0) {
            throw new https_1.HttpsError("failed-precondition", "This item is not an IAP item.");
        }
        if (m === "AD" && !isAd) {
            throw new https_1.HttpsError("failed-precondition", "This item is not ad-reward purchasable.");
        }
        if (m === "PREMIUM" && pricePremium <= 0) {
            throw new https_1.HttpsError("failed-precondition", "This item is not purchasable with premium currency.");
        }
        // Ownership check (non-consumables cannot be purchased twice)
        const alreadyOwned = invSnap.exists && !!invSnap.get("owned");
        if (alreadyOwned && !isConsumable) {
            throw new https_1.HttpsError("failed-precondition", "Already owned.");
        }
        // ---- WRITES AFTER READS ----
        // Charge / touch user depending on method
        if (m === "GET") {
            const curBalance = Number((_e = userSnap.get("currency")) !== null && _e !== void 0 ? _e : 0) || 0;
            if (curBalance < priceGet) {
                throw new https_1.HttpsError("failed-precondition", "Not enough currency.");
            }
            tx.update(userRef, { currency: curBalance - priceGet, updatedAt: firestore_2.FieldValue.serverTimestamp() });
        }
        else if (m === "PREMIUM") {
            const curPremium = Number((_f = userSnap.get("premiumCurrency")) !== null && _f !== void 0 ? _f : 0) || 0;
            if (curPremium < pricePremium) {
                throw new https_1.HttpsError("failed-precondition", "Not enough premium currency.");
            }
            tx.update(userRef, { premiumCurrency: curPremium - pricePremium, updatedAt: firestore_2.FieldValue.serverTimestamp() });
        }
        else {
            // touch the user doc for bookkeeping
            tx.set(userRef, { updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        }
        // Prepare audit expiry holder
        let newExpiry = null;
        // Upsert inventory / Activate consumable
        // Upsert inventory / Activate consumable
        if (isConsumable) {
            // Determine previous active state from existing activeConsumables doc
            const prevExpiry = acSnap.exists
                ? acSnap.get("expiresAt")
                : null;
            const wasActive = !!prevExpiry && prevExpiry.toMillis() > now.toMillis();
            const baseMillis = (wasActive && prevExpiry) ? prevExpiry.toMillis() : now.toMillis();
            const durationMs = 24 * 60 * 60 * 1000; // 24h per purchase
            newExpiry = firestore_2.Timestamp.fromMillis(baseMillis + durationMs);
            // Persist/extend active window (no inventory write for consumables)
            tx.set(acRef, {
                itemId,
                active: true,
                expiresAt: newExpiry,
                lastActivatedAt: now,
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            }, { merge: true });
            // On first activation only, add stats to user
            if (!wasActive) {
                const itemStats = extractItemStats(item);
                if (Object.keys(itemStats).length > 0) {
                    const baseStats = parseStatsJson(userSnap.get("statsJson"));
                    const merged = mergeStats(baseStats, itemStats, 1);
                    tx.update(userRef, {
                        statsJson: JSON.stringify(merged),
                        updatedAt: firestore_2.FieldValue.serverTimestamp(),
                    });
                }
            }
            // (No inventory doc creation for consumables)
        }
        else {
            // Non-consumables: mark owned as before
            const invData = {
                owned: true,
                equipped: invSnap.get("equipped") === true, // preserve equip
                quantity: 0,
                itemIsConsumable: false,
                lastChangedAt: firestore_2.FieldValue.serverTimestamp(),
                acquiredAt: invSnap.exists ? (_g = invSnap.get("acquiredAt")) !== null && _g !== void 0 ? _g : now : now,
            };
            tx.set(invRef, invData, { merge: true });
        }
        // Audit trail (use exact newExpiry when consumable)
        const logRef = userRef.collection("purchases").doc();
        tx.set(logRef, {
            itemId,
            method: m,
            priceGet: m === "GET" ? priceGet : 0,
            pricePremium: m === "PREMIUM" ? pricePremium : 0,
            priceUsd: m === "IAP" ? priceUsd : 0,
            isConsumable,
            expiresAt: newExpiry, // precise expiry if consumable
            at: now,
        });
        // Increment purchased items counter atomically
        tx.set(userRef, {
            itemsPurchasedCount: firestore_2.FieldValue.increment(1),
            updatedAt: firestore_2.FieldValue.serverTimestamp()
        }, { merge: true });
        // Build response snapshot
        const currencyLeft = m === "GET"
            ? Math.max(0, (Number((_h = userSnap.get("currency")) !== null && _h !== void 0 ? _h : 0) || 0) - priceGet)
            : Number((_j = userSnap.get("currency")) !== null && _j !== void 0 ? _j : 0) || 0;
        const premiumCurrencyLeft = m === "PREMIUM"
            ? Math.max(0, (Number((_k = userSnap.get("premiumCurrency")) !== null && _k !== void 0 ? _k : 0) || 0) - pricePremium)
            : Number((_l = userSnap.get("premiumCurrency")) !== null && _l !== void 0 ? _l : 0) || 0;
        return {
            ok: true,
            itemId,
            owned: !isConsumable,
            isConsumable,
            currencyLeft,
            premiumCurrencyLeft,
            expiresAt: newExpiry ? newExpiry.toDate().toISOString() : null,
            expiresAtMillis: newExpiry ? newExpiry.toMillis() : null,
        };
    });
    // Post-achievement update (outside txn)
    try {
        const u = await db.collection("users").doc(uid).get();
        await upsertUserAch(uid, ACH_TYPES.MARKET_WHISPER, Number((_b = u.get("itemsPurchasedCount")) !== null && _b !== void 0 ? _b : 0) || 0);
    }
    catch (e) {
        console.warn("[ach] purchase evaluate failed", e);
    }
    return res;
});
// ---------------- getActiveConsumables (callable) ----------------
// Returns active consumables with future expiry; expired ones are omitted on read.
exports.getActiveConsumables = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const now = firestore_2.Timestamp.now();
    // Pre-clean expired consumables in a separate transaction
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });
    const snap = await userRef
        .collection("activeConsumables")
        .where("expiresAt", ">", now)
        .get();
    const items = snap.docs.map((d) => {
        const data = d.data() || {};
        const exp = data.expiresAt || null;
        return {
            itemId: d.id,
            active: true,
            expiresAt: exp ? exp.toDate().toISOString() : null,
            expiresAtMillis: exp ? exp.toMillis() : null,
        };
    });
    return { ok: true, serverNowMillis: now.toMillis(), items };
});
// ---------------- equipItem ----------------
exports.equipItem = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const rawItemId = ((_b = req.data) === null || _b === void 0 ? void 0 : _b.itemId) || "";
    const itemId = normId(rawItemId);
    if (!itemId)
        throw new https_1.HttpsError("invalid-argument", "itemId required.");
    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const loadRef = userRef.collection("loadout").doc("current");
    await db.runTransaction(async (tx) => {
        // ---- READS FIRST (required by Firestore) ----
        const [invSnap, itemSnap, loadSnap, userSnap] = await Promise.all([
            tx.get(invRef),
            tx.get(itemRef),
            tx.get(loadRef),
            tx.get(userRef),
        ]);
        if (!invSnap.exists || !invSnap.get("owned")) {
            throw new https_1.HttpsError("failed-precondition", "Item not owned.");
        }
        if (!itemSnap.exists) {
            throw new https_1.HttpsError("not-found", "Item not found.");
        }
        const isConsumable = !!itemSnap.get("itemIsConsumable");
        if (isConsumable) {
            throw new https_1.HttpsError("failed-precondition", "Consumables cannot be equipped.");
        }
        // Normalize current equipped list
        let equipped = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
        equipped = equipped.map((x) => normId(x));
        const wasEquipped = equipped.includes(itemId);
        if (!wasEquipped) {
            equipped.push(itemId);
        }
        // ---- WRITES AFTER ALL READS ----
        tx.set(loadRef, { equippedItemIds: equipped, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        tx.set(invRef, { equipped: true, lastChangedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        // Merge item stats into user's stats only on first-time equip
        if (!wasEquipped) {
            const baseStats = parseStatsJson(userSnap.get("statsJson"));
            const itemStats = extractItemStats(itemSnap.data() || {});
            const merged = mergeStats(baseStats, itemStats, 1);
            tx.update(userRef, {
                statsJson: JSON.stringify(merged),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            });
        }
    });
    return { ok: true, itemId };
});
// ---------------- unequipItem ----------------
exports.unequipItem = (0, https_1.onCall)(async (req) => {
    var _a, _b;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const rawItemId = ((_b = req.data) === null || _b === void 0 ? void 0 : _b.itemId) || "";
    const itemId = normId(rawItemId);
    if (!itemId)
        throw new https_1.HttpsError("invalid-argument", "itemId required.");
    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const loadRef = userRef.collection("loadout").doc("current");
    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
    await db.runTransaction(async (tx) => {
        // ---- READS FIRST ----
        const [loadSnap, userSnap, itemSnap] = await Promise.all([
            tx.get(loadRef),
            tx.get(userRef),
            tx.get(itemRef),
        ]);
        let before = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
        const beforeNorm = before.map((x) => normId(x));
        const wasEquipped = beforeNorm.includes(itemId);
        const afterEquipped = beforeNorm.filter((x) => x !== itemId);
        // ---- WRITES AFTER ALL READS ----
        tx.set(loadRef, { equippedItemIds: afterEquipped, updatedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        tx.set(invRef, { equipped: false, lastChangedAt: firestore_2.FieldValue.serverTimestamp() }, { merge: true });
        // Subtract item stats from user's stats only if it was previously equipped
        if (wasEquipped) {
            if (!itemSnap.exists)
                throw new https_1.HttpsError("not-found", "Item not found.");
            const baseStats = parseStatsJson(userSnap.get("statsJson"));
            const itemStats = extractItemStats(itemSnap.data() || {});
            const merged = mergeStats(baseStats, itemStats, -1);
            tx.update(userRef, {
                statsJson: JSON.stringify(merged),
                updatedAt: firestore_2.FieldValue.serverTimestamp(),
            });
        }
    });
    return { ok: true, itemId };
});
// -------- recomputeRanks (callable) --------
exports.recomputeRanks = (0, https_1.onCall)(async (req) => {
    var _a;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const res = await recomputeAllRanks();
    return { ok: true, updated: res.count };
});
// ========================= Change Username =========================
exports.changeUsername = (0, https_1.onCall)(async (req) => {
    var _a, _b, _c, _d;
    const uid = (_a = req.auth) === null || _a === void 0 ? void 0 : _a.uid;
    if (!uid)
        throw new https_1.HttpsError("unauthenticated", "Auth required.");
    const newNameRaw = String((_c = (_b = req.data) === null || _b === void 0 ? void 0 : _b.newName) !== null && _c !== void 0 ? _c : "").trim();
    const now = firestore_2.Timestamp.now();
    const newNameLower = newNameRaw.toLowerCase();
    // --- Basic format validation ---
    if (newNameRaw.length < 3 || newNameRaw.length > 20) {
        throw new https_1.HttpsError("invalid-argument", "USERNAME_INVALID_LENGTH");
    }
    if (!/^[a-zA-Z0-9._-]+$/.test(newNameRaw)) {
        throw new https_1.HttpsError("invalid-argument", "USERNAME_INVALID_CHARS");
    }
    // --- Banned keyword rules from Firestore (config driven) ---
    // Path: appdata/usernamerules  (doc)
    // Field: bannedKeywords (array)  veya bannedkeywords (array)
    let bannedList = [
        // fallback (Firestore'dan bir şey bulamazsa)
        "fuck",
        "amk",
        "siktir",
        "orospu",
        "piç",
        "aq",
        "porno",
    ];
    try {
        const rulesSnap = await db.collection("appdata").doc("usernamerules").get();
        if (rulesSnap.exists) {
            const d = (rulesSnap.data() || {});
            let fromField = (_d = d.bannedKeywords) !== null && _d !== void 0 ? _d : d.bannedkeywords;
            // CASE 1: Already an array
            if (Array.isArray(fromField)) {
                bannedList = fromField
                    .map((x) => String(x || "").toLowerCase().trim())
                    .filter((s) => s.length > 0);
                // CASE 2: A raw string → attempt JSON parse first
            }
            else if (typeof fromField === "string" && fromField.trim().length > 0) {
                const raw = fromField.trim();
                let parsedOk = false;
                // Try JSON parse
                try {
                    const parsed = JSON.parse(raw);
                    if (parsed && Array.isArray(parsed.bannedKeywords)) {
                        bannedList = parsed.bannedKeywords
                            .map((x) => String(x || "").toLowerCase().trim())
                            .filter((s) => s.length > 0);
                        parsedOk = true;
                    }
                }
                catch (_) { }
                // Fallback: split by comma/whitespace
                if (!parsedOk) {
                    bannedList = raw
                        .split(/[,\s]+/)
                        .map((x) => x.toLowerCase().trim())
                        .filter((s) => s.length > 0);
                }
            }
        }
    }
    catch (e) {
        console.warn("[changeUsername] could not load usernamerules, using fallback list:", e);
    }
    const lower = newNameLower;
    for (const bad of bannedList) {
        if (!bad)
            continue;
        if (lower.includes(bad)) {
            throw new https_1.HttpsError("invalid-argument", "USERNAME_BAD_WORD");
        }
    }
    const userRef = db.collection("users").doc(uid);
    const nameRef = db.collection("usernames").doc(newNameLower);
    await db.runTransaction(async (tx) => {
        const [uSnap, nameSnap] = await Promise.all([
            tx.get(userRef),
            tx.get(nameRef),
        ]);
        if (!uSnap.exists) {
            throw new https_1.HttpsError("failed-precondition", "USER_DOC_MISSING");
        }
        const data = uSnap.data() || {};
        const lastChangedAt = data.usernameLastChangedAt;
        // --- Haftada 1 değişim limiti ---
        if (lastChangedAt) {
            const diff = now.toMillis() - lastChangedAt.toMillis();
            const WEEK_MS = 7 * 24 * 60 * 60 * 1000;
            if (diff < WEEK_MS) {
                throw new https_1.HttpsError("failed-precondition", "USERNAME_CHANGE_TOO_SOON");
            }
        }
        // --- Benzersiz username kontrolü ---
        if (nameSnap.exists) {
            const owner = nameSnap.get("uid");
            if (owner !== uid) {
                throw new https_1.HttpsError("already-exists", "USERNAME_TAKEN");
            }
        }
        // Eski username rezervasyonunu sil
        const oldName = (data.username || "").toLowerCase();
        if (oldName) {
            const oldRef = db.collection("usernames").doc(oldName);
            tx.delete(oldRef);
        }
        // Yeni username'i rezerve et
        tx.set(nameRef, { uid, updatedAt: now }, { merge: true });
        // User doc'u güncelle
        tx.set(userRef, {
            username: newNameRaw,
            usernameLastChangedAt: now,
            updatedAt: firestore_2.FieldValue.serverTimestamp(),
        }, { merge: true });
    });
    return { ok: true, newName: newNameRaw };
});
//# sourceMappingURL=index.js.map