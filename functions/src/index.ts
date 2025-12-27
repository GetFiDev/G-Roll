import * as admin from "firebase-admin";
import * as functionsV1 from "firebase-functions/v1";
import { onDocumentWritten } from "firebase-functions/v2/firestore";
import { onCall, HttpsError } from "firebase-functions/v2/https";
import { setGlobalOptions } from "firebase-functions/v2/options";
import { Firestore, Timestamp, FieldValue } from "@google-cloud/firestore";

admin.initializeApp();
// nam5 -> us-central1

setGlobalOptions({ region: "us-central1" });

// --- ID Normalization Helper (use everywhere for itemId) ---
const normId = (s?: string) => (s || "").trim().toLowerCase();

const DB_ID = "getfi";
const PROJECT_ID = process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT_ID || "";
const DB_PATH = `projects/${PROJECT_ID}/databases/${DB_ID}`;
console.log(`[boot] Firestore DB selected: ${DB_PATH}`);
const SEASON = "current";

const RANK_BATCH = 500; // how many users to rank per page
const RANK_LOCK_DOC = `leaderboards/${SEASON}/meta/rank_job`;

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
      }, { merge: true });
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
    }, { merge: true });
  } catch { }
}

async function recomputeAllRanks(): Promise<{ count: number }> {
  const now = Timestamp.now();
  const got = await acquireRankLock(now);
  if (!got) {
    console.log("[ranks] another job is running; skip");
    return { count: 0 };
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
        batch.set(doc.ref, { rank, updatedAt: FieldValue.serverTimestamp() }, { merge: true });
      });
      await batch.commit();

      ranked += snap.size;
      const last = snap.docs[snap.docs.length - 1];
      lastScore = Number(last.get("maxScore") ?? 0) || 0;

      // extend lock while we are still working
      await db.doc(RANK_LOCK_DOC).set({
        lockedUntil: Timestamp.fromMillis(Timestamp.now().toMillis() + 2 * 60 * 1000),
        updatedAt: FieldValue.serverTimestamp()
      }, { merge: true });
    }

    console.log(`[ranks] recomputed for ${ranked} users`);
    return { count: ranked };
  } finally {
    await releaseRankLock();
  }
}



// UTC date helper (YYYY-MM-DD)
function utcDateString(ts: Timestamp): string {
  return new Date(ts.toMillis()).toISOString().slice(0, 10);
}


const db = new Firestore({ databaseId: DB_ID });

// ========================= Streak System (server-side only) =========================
// Doc layout:
// - Config: appdata/streakdata { reward:number }
// - User state: users/{uid}/meta/streak {
//     totalDays:number, unclaimedDays:number, lastLoginDate:string(YYYY-MM-DD UTC),
//     lastClaimAt?:Timestamp, createdAt:Timestamp, updatedAt:serverTimestamp
//   }

const streakConfigRef = db.collection("appdata").doc("streakdata");
const userStreakRef = (uid: string) => db.collection("users").doc(uid).collection("meta").doc("streak");

// recordLogin: DEPRECATED. Kept as a thin wrapper to the new updateDailyStreak core.
export const recordLogin = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const res = await applyDailyStreakIncrement(uid);
  return { ok: true, ...res, deprecated: true };
});

// claimStreak: Grants accumulated unclaimedDays * reward to user currency and resets unclaimedDays.
export const claimStreak = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const now = Timestamp.now();
  const uRef = db.collection("users").doc(uid);
  const sRef = userStreakRef(uid);

  const res = await db.runTransaction(async (tx) => {
    const [uSnap, sSnap, cfgSnap] = await Promise.all([tx.get(uRef), tx.get(sRef), tx.get(streakConfigRef)]);

    if (!uSnap.exists) throw new HttpsError("failed-precondition", "User doc missing");
    const unclaimed = sSnap.exists ? Number(snapNum(sSnap.get("unclaimedDays"))) : 0;
    if (unclaimed <= 0) {
      const curCurrency = Number(uSnap.get("currency") ?? 0) || 0;
      const rewardPerDay = cfgSnap.exists ? Number(cfgSnap.get("reward") ?? 0) || 0 : 0;
      return { granted: 0, rewardPerDay: Math.max(0, rewardPerDay), unclaimedDays: 0, newCurrency: curCurrency };
    }

    const rewardPerDay = cfgSnap.exists ? Number(cfgSnap.get("reward") ?? 0) || 0 : 0;
    const grant = Math.max(0, rewardPerDay) * unclaimed;

    const curCurrency = Number(uSnap.get("currency") ?? 0) || 0;
    const newCurrency = curCurrency + grant;

    tx.set(uRef, { currency: newCurrency, updatedAt: FieldValue.serverTimestamp() }, { merge: true });
    tx.set(sRef, { unclaimedDays: 0, lastClaimAt: now, updatedAt: FieldValue.serverTimestamp() }, { merge: true });

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

  return { ok: true, ...res };
});

// getStreakStatus: For UI — shows whether a claim is available and the next UTC midnight countdown.
export const getStreakStatus = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const res = await applyDailyStreakIncrement(uid);
  return { ok: true, ...res };
});

// small numeric helper for safe casting
function snapNum(v: any): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}

// ========================= Achievements =========================
// Types: map to server-side progress sources on users/{uid}
const ACH_TYPES = {
  ENDLESS_ROLLER: "endless_roller",        // sessionsPlayed
  SCORE_CHAMPION: "score_champion",         // maxScore
  TOKEN_HUNTER: "token_hunter",           // cumulativeCurrencyEarned
  COMBO_GOD: "combo_god",              // maxCombo
  MARKET_WHISPER: "market_whisperer",       // itemsPurchasedCount
  TIME_DRIFTER: "time_drifter",           // totalPlaytimeMinutes
  HABIT_MAKER: "habit_maker",            // streak (daily login)
  POWERUP_EXP: "powerup_explorer",       // powerUpsCollected
  SIGNAL_BOOST: "signal_booster",         // referrals
} as const;

type AchType = typeof ACH_TYPES[keyof typeof ACH_TYPES];

type AchLevel = { threshold: number; rewardGet: number };

type AchDoc = {
  levels: AchLevel[]; // length=5 expected
  displayName?: string;
  description?: string;
  iconUrl?: string;
  order?: number;
};

const achDefRef = (typeId: AchType) => db.collection("appdata").doc("achievements").collection("types").doc(typeId);
const achUserRef = (uid: string, typeId: AchType) => db.collection("users").doc(uid).collection("achievements").doc(typeId);

async function readAchDef(typeId: AchType): Promise<AchDoc> {
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

function computeLevel(progress: number, levels: AchLevel[]): number {
  let lvl = 0;
  for (let i = 0; i < levels.length; i++) {
    if (progress >= levels[i].threshold) lvl = i + 1; else break;
  }
  return lvl; // 0..5
}

async function upsertUserAch(uid: string, typeId: AchType, progress: number) {
  const def = await readAchDef(typeId);
  const level = computeLevel(progress, def.levels);
  const nextThreshold = level < def.levels.length ? def.levels[level].threshold : null;
  const ref = achUserRef(uid, typeId);
  await ref.set({
    progress,
    level,
    nextThreshold,
    updatedAt: FieldValue.serverTimestamp(),
  }, { merge: true });
}

async function grantAchReward(uid: string, typeId: AchType, level: number) {
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
    tx.set(uRef, { currency: curCurrency + reward, updatedAt: FieldValue.serverTimestamp() }, { merge: true });
    tx.set(aRef, { claimedLevels: FieldValue.arrayUnion(level), lastClaimedAt: Timestamp.now() }, { merge: true });
    return { reward, newCurrency: curCurrency + reward };
  });
}

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
    return { typeId: d.typeId, progress, level, claimedLevels: claimed, nextThreshold };
  });

  return { ok: true, defs, states };
});

// -------- claimAchievementReward (callable) --------
export const claimAchievementReward = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const typeId = String(req.data?.typeId || "").trim() as AchType;
  const level = Number(req.data?.level ?? 0);
  if (!typeId || !level) throw new HttpsError("invalid-argument", "typeId and level required");
  const res = await grantAchReward(uid, typeId, level);
  return { ok: true, rewardGet: res.reward, newCurrency: res.newCurrency };
});

// ---- Stats helpers for equip/unequip merging ----
function parseStatsJson(s: any): Record<string, number> {
  if (typeof s !== "string") return {};
  try {
    const o = JSON.parse(s);
    if (o && typeof o === "object") return o as Record<string, number>;
    return {};
  } catch {
    return {};
  }
}

function mergeStats(base: Record<string, number>, delta: Record<string, number>, sign: 1 | -1) {
  const out: Record<string, number> = { ...base };
  for (const k of Object.keys(delta)) {
    const v = Number(delta[k]);
    if (!Number.isFinite(v)) continue;
    const cur = Number(out[k] ?? 0);
    out[k] = cur + sign * v;
  }
  return out;
}

function extractItemStats(raw: Record<string, any>): Record<string, number> {
  const out: Record<string, number> = {};
  for (const [k, v] of Object.entries(raw || {})) {
    if (k.startsWith("itemstat_")) {
      const statKey = k.replace("itemstat_", "");
      out[statKey] = Number(v) || 0;
    }
  }
  return out;
}
// ---- Consumables lazy cleanup helper ----
async function cleanupExpiredConsumablesInTx(
  tx: FirebaseFirestore.Transaction,
  userRef: FirebaseFirestore.DocumentReference,
  now: Timestamp
) {
  const activeCol = userRef.collection("activeConsumables");

  // READS first: all reads before writes
  const expiredSnap = await tx.get(
    activeCol.where("expiresAt", "<=", now)
  );
  if (expiredSnap.empty) return;

  let totalDelta: Record<string, number> = {};
  const itemRefs: FirebaseFirestore.DocumentReference[] = [];
  const toDelete: FirebaseFirestore.DocumentReference[] = [];

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
    if (!s.exists) return;
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
      updatedAt: FieldValue.serverTimestamp(),
    });
  }

  // WRITE: delete expired docs
  toDelete.forEach((ref) => tx.delete(ref));
}

// ---------------- Leaderboard Sync ----------------
export const syncLeaderboard = onDocumentWritten(
  { document: "users/{uid}", database: DB_ID },
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
      { merge: true }
    );
    // NOTE: Removed materialization of topN and background rank recomputation.
  }
);

// ---------------- getLeaderboardsSnapshot (callable) ----------------
// Returns a snapshot of the leaderboard from leaderboards/{SEASON}/entries.
// Client should page using startAfterScore if needed. No ranks are written/returned.
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
    const s = Number(startAfterScoreRaw);
    if (Number.isFinite(s)) q = q.startAfter(s);
  }

  const snap = await q.get();

  const items = snap.docs.map((d) => {
    const data = (d.data() || {}) as any;
    const username = (typeof data.username === "string" ? data.username : "").trim() || "Guest";
    const score = typeof data.score === "number" ? data.score : 0;
    const updatedAt = (data.updatedAt as Timestamp | undefined)?.toDate()?.toISOString() ?? null;
    const eliteTs = (data.elitePassExpiresAt as Timestamp | undefined) || undefined;
    const elitePassExpiresAtMillis = eliteTs ? eliteTs.toMillis() : null;
    return { uid: d.id, username, score, updatedAt, elitePassExpiresAtMillis };
  });

  const hasMore = snap.size >= limit;
  const next = hasMore && snap.docs.length > 0
    ? { startAfterScore: (typeof snap.docs[snap.docs.length - 1].get("score") === 'number' ? snap.docs[snap.docs.length - 1].get("score") : 0) }
    : null;

  let me: { uid: string; username: string; score: number } | null = null;
  if (includeSelf) {
    // Prefer leaderboard entry; fallback to users doc
    const [myEntrySnap, userSnap] = await Promise.all([
      seasonRef.collection("entries").doc(uid).get(),
      db.collection("users").doc(uid).get()
    ]);
    if (myEntrySnap.exists) {
      const md = myEntrySnap.data() || {} as any;
      me = {
        uid,
        username: (typeof md.username === "string" ? md.username : "").trim() || "Guest",
        score: typeof md.score === "number" ? md.score : 0,
      };
    } else if (userSnap.exists) {
      const ud = userSnap.data() || {} as any;
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
export const purchaseElitePass = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const purchaseId = (req.data?.purchaseId ?? "").toString().trim();
  const now = Timestamp.now();
  const userRef = db.collection("users").doc(uid);

  // idempotency
  if (purchaseId) {
    const pRef = userRef.collection("elitePassPurchases").doc(purchaseId);
    const pSnap = await pRef.get();
    if (pSnap.exists) {
      const uSnap = await userRef.get();
      const exp = uSnap.exists
        ? (uSnap.get("elitePassExpiresAt") as Timestamp | null)
        : null;
      const active = !!exp && exp.toMillis() > now.toMillis();
      return {
        active,
        expiresAt: exp?.toDate().toISOString() ?? null,
      };
    }
  }

  await db.runTransaction(async (tx) => {
    const uSnap = await tx.get(userRef);
    const existing: Timestamp | null = uSnap.exists
      ? (uSnap.get("elitePassExpiresAt") as Timestamp | null)
      : null;

    const baseMillis =
      existing && existing.toMillis() > now.toMillis()
        ? existing.toMillis()
        : now.toMillis();

    const thirtyMs = 30 * 24 * 60 * 60 * 1000;
    const newExpiry = Timestamp.fromMillis(baseMillis + thirtyMs);

    tx.set(
      userRef,
      {
        hasElitePass: true,
        elitePassExpiresAt: newExpiry,
        updatedAt: FieldValue.serverTimestamp()
      },
      { merge: true }
    );

    if (purchaseId) {
      tx.set(
        userRef.collection("elitePassPurchases").doc(purchaseId),
        { processedAt: now, newExpiry },
        { merge: true }
      );
    }
  });

  const final = await userRef.get();
  const exp = final.get("elitePassExpiresAt") as Timestamp | null;
  const active = !!exp && exp.toMillis() > now.toMillis();
  return {
    active,
    expiresAt: exp?.toDate().toISOString() ?? null,
  };
});

export const checkElitePass = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const now = Timestamp.now();
  const snap = await db.collection("users").doc(uid).get();
  if (!snap.exists) return {
    active: false,
    expiresAt: null,
  };

  const exp = snap.get("elitePassExpiresAt") as Timestamp | null;
  const active = !!exp && exp.toMillis() > now.toMillis();
  return {
    active,
    expiresAt: exp?.toDate().toISOString() ?? null,
  };
});

// ---------------- Referral ----------------
// benzer karakterler yok
const ALPHABET =
  "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

function randomReferralKey(len = 12): string {
  let s = "";
  for (let i = 0; i < len; i++) {
    s += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
  }
  return s;
}

async function reserveUniqueReferralKey(uid: string): Promise<string> {
  for (let i = 0; i < 6; i++) {
    const k = randomReferralKey(12);
    const ref = db.collection("referralKeys").doc(k);
    const snap = await ref.get();
    if (!snap.exists) {
      await ref.set({ ownerUid: uid, createdAt: Timestamp.now() });
      return k;
    }
  }
  throw new Error("Could not allocate unique referral key");
}


async function ensureReferralKeyFor(uid: string): Promise<string> {
  const userRef = db.collection("users").doc(uid);
  return await db.runTransaction(async (tx) => {
    const snap = await tx.get(userRef);
    if (!snap.exists) throw new Error("user doc missing");

    const current = (snap.get("referralKey") as string) || "";
    if (current) return current;

    const key = await reserveUniqueReferralKey(uid);
    tx.set(
      userRef,
      { referralKey: key, updatedAt: FieldValue.serverTimestamp() },
      { merge: true }
    );
    return key;
  });
}

// Grant referral reward items to ownerUid if their referrals reach new thresholds
async function grantReferralThresholdItems(ownerUid: string, referrals: number): Promise<void> {
  if (!ownerUid || !Number.isFinite(referrals) || referrals <= 0) return;
  console.log("[referralItems] START", { ownerUid, referrals });

  // Root for appdata/items/...
  const itemsRoot = db.collection("appdata").doc("items");

  // Collect candidate referral-only items whose threshold is <= current referrals
  const subCols = await itemsRoot.listCollections();
  console.log("[referralItems] COLS", subCols.map((c) => c.id));
  type ReferralItemDef = { itemId: string; threshold: number; isConsumable: boolean };
  const candidates: ReferralItemDef[] = [];

  for (const subCol of subCols) {
    const docSnap = await subCol.doc("itemdata").get();
    if (!docSnap.exists) continue;
    const data = (docSnap.data() || {}) as any;
    console.log("[referralItems] ITEMDATA", { itemId: subCol.id, data });

    const threshold = Number(data.itemReferralThreshold ?? 0) || 0;
    if (threshold <= 0) continue;
    if (referrals < threshold) continue; // not yet eligible
    console.log("[referralItems] CANDIDATE", { itemId: subCol.id, threshold });

    const isConsumable = !!data.itemIsConsumable;
    const itemId = normId(subCol.id);
    candidates.push({ itemId, threshold, isConsumable });
  }

  console.log("[referralItems] CANDIDATES_FINAL", candidates);
  if (candidates.length === 0) return;

  const userRef = db.collection("users").doc(ownerUid);
  const invCol = userRef.collection("inventory");
  const now = Timestamp.now();

  await db.runTransaction(async (tx) => {
    // === READ PHASE: all tx.get calls first ===
    const invRefs = candidates.map((c) => invCol.doc(c.itemId));
    const invSnaps = await Promise.all(invRefs.map((r) => tx.get(r)));

    // Precompute existing data so we don't read after writes
    const invStates = invSnaps.map((snap) => {
      const exists = snap.exists;
      const data = exists ? (snap.data() || {}) : {};
      return { exists, data: data as any };
    });

    // === WRITE PHASE: only tx.set calls, no more tx.get ===
    candidates.forEach((c, index) => {
      const invRef = invRefs[index];
      const invState = invStates[index];
      const invData = invState.data;

      const prevThresh = Number(invData.referralGrantedThreshold ?? 0) || 0;
      console.log("[referralItems] TX_CHECK", {
        itemId: c.itemId,
        prevThresh,
        cThreshold: c.threshold,
      });

      // Prevent double-grant per item by tracking the highest referral threshold already granted
      if (prevThresh >= c.threshold) {
        return; // already granted for this (or a higher) threshold
      }

      const base: Record<string, any> = {
        grantedByReferral: true,
        referralGrantedThreshold: c.threshold,
        updatedAt: FieldValue.serverTimestamp(),
      };

      if (!invState.exists) {
        base.createdAt = now;
      }

      const prevQty = Number(invData.quantity ?? 0) || 0;
      base.owned = true;

      if (c.isConsumable) {
        // consumables should still count as owned for UI, and stack quantity
        base.quantity = prevQty + 1;
      } else {
        // non-consumables: ensure at least 1
        base.quantity = prevQty > 0 ? prevQty : 1;
      }

      console.log("[referralItems] TX_SET", { itemId: c.itemId, base });
      tx.set(invRef, base, { merge: true });
    });
  });
}

async function applyReferralCodeToUser(
  uid: string,
  codeRaw: string
): Promise<{ applied: boolean; referredByUid?: string }> {
  const code = (codeRaw || "").toUpperCase().trim();
  if (!/^[A-Z0-9]{12}$/.test(code)) {
    throw new HttpsError("invalid-argument", "Invalid referral code");
  }

  const keyRef = db.collection("referralKeys").doc(code);
  const keySnap = await keyRef.get();
  if (!keySnap.exists) {
    throw new HttpsError("not-found", "Referral code not found");
  }

  const ownerUid = (keySnap.get("ownerUid") as string) || "";
  if (!ownerUid || ownerUid === uid) {
    throw new HttpsError("failed-precondition", "Cannot use this code");
  }

  const userRef = db.collection("users").doc(uid);
  const ownerRef = db.collection("users").doc(ownerUid);

  await db.runTransaction(async (tx) => {
    const u = await tx.get(userRef);
    if (!u.exists) {
      throw new HttpsError("failed-precondition", "User doc missing");
    }

    const already = (u.get("referredByKey") as string) || "";
    if (already) return; // idempotent

    tx.set(
      userRef,
      {
        referredByKey: code,
        referredByUid: ownerUid,
        referralAppliedAt: Timestamp.now(),
        updatedAt: FieldValue.serverTimestamp()
      },
      { merge: true }
    );

    tx.set(
      ownerRef,
      {
        referrals: FieldValue.increment(1),
        updatedAt: FieldValue.serverTimestamp()
      },
      { merge: true }
    );
  });

  try {
    const ownerSnap = await db.collection("users").doc(ownerUid).get();
    const referralsCount = Number(ownerSnap.get("referrals") ?? 0) || 0;

    await upsertUserAch(ownerUid, ACH_TYPES.SIGNAL_BOOST, referralsCount);
    await grantReferralThresholdItems(ownerUid, referralsCount);
  } catch (e) {
    console.warn("[ach/referral] post-referral processing failed", e);
  }

  return { applied: true, referredByUid: ownerUid };
}

// -------- profile create (Auth trigger) --------
export const createUserProfile = functionsV1
  .region("us-central1")
  .auth.user()
  .onCreate(async (user) => {
    const uid = user.uid;
    const email = user.email ?? "";
    const userRef = db.collection("users").doc(uid);

    await db.runTransaction(async (tx) => {
      const snap = await tx.get(userRef);
      if (snap.exists) return; // idempotent

      tx.set(userRef, {
        mail: email,
        username: "",
        currency: 0,
        premiumCurrency: 0,
        energyCurrent: 6,
        energyMax: 6,
        energyRegenPeriodSec: 14400,
        energyUpdatedAt: Timestamp.now(),
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
        elitePassExpiresAt: Timestamp.fromMillis(Date.now() - 24 * 60 * 60 * 1000),
        lastLogin: Timestamp.now(),
        createdAt: FieldValue.serverTimestamp(),
        updatedAt: FieldValue.serverTimestamp(),
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
      const nowTs = Timestamp.now();
      tx.set(
        sRef,
        {
          totalDays: 0,
          unclaimedDays: 0,
          lastLoginDate: "",
          createdAt: nowTs,
          updatedAt: FieldValue.serverTimestamp(),
        },
        { merge: true }
      );
    });

    await ensureReferralKeyFor(uid);
  });

// -------- profile ensure + optional referral apply --------
export const ensureUserProfile = onCall(async (req) => {
  const uid = req.auth?.uid;
  const email = req.auth?.token?.email ?? "";
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

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
        energyUpdatedAt: Timestamp.now(),
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
        elitePassExpiresAt: Timestamp.fromMillis(
          Date.now() - 24 * 60 * 60 * 1000
        ),
        lastLogin: Timestamp.now(),
        createdAt: FieldValue.serverTimestamp(),
        updatedAt: FieldValue.serverTimestamp(),
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
      const nowTs = Timestamp.now();
      tx.set(
        sRef,
        {
          totalDays: 0,
          unclaimedDays: 0,
          lastLoginDate: "",
          createdAt: nowTs,
          updatedAt: FieldValue.serverTimestamp(),
        },
        { merge: true }
      );
    } else {
      const needInit = !snap.get("elitePassExpiresAt");
      const patch: any = {
        lastLogin: Timestamp.now(),
        updatedAt: FieldValue.serverTimestamp(),
      };
      if (needInit) {
        // backfill as expired if missing/null
        patch.elitePassExpiresAt =
          Timestamp.fromMillis(Date.now() - 24 * 60 * 60 * 1000);
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
        if (snap.get(k) === undefined) patch[k] = 0;
      }
      if (snap.get("premiumCurrency") === undefined) patch.premiumCurrency = 0;
      // Ensure streak subdoc exists without clobbering existing values
      const sRef = userStreakRef(uid);
      const sSnap2 = await tx.get(sRef);
      if (!sSnap2.exists) {
        tx.set(
          sRef,
          {
            totalDays: 0,
            unclaimedDays: 0,
            lastLoginDate: "",
            createdAt: Timestamp.now(),
            updatedAt: FieldValue.serverTimestamp(),
          },
          { merge: true }
        );
      } else {
        // just touch updatedAt to keep a heartbeat; don't reset counters
        tx.set(
          sRef,
          { updatedAt: FieldValue.serverTimestamp() },
          { merge: true }
        );
      }
      tx.set(userRef, patch, { merge: true });
    }
  });

  const key = await ensureReferralKeyFor(uid);

  const code = (req.data?.referralCode ?? "").toString();
  if (code) await applyReferralCodeToUser(uid, code);

  return {
    ok: true,
    referralKey: key,
  };
});

// Shared core for streak increment logic. Idempotent per UTC day.
async function applyDailyStreakIncrement(uid: string): Promise<{
  serverNowMillis: number;
  nextUtcMidnightMillis: number;
  totalDays: number;
  unclaimedDays: number;
  rewardPerDay: number;
  pendingTotalReward: number;
  claimAvailable: boolean;
  todayCounted: boolean;
}> {
  const now = Timestamp.now();
  const today = utcDateString(now); // YYYY-MM-DD in UTC
  const userRef = db.collection("users").doc(uid);
  const sRef = userStreakRef(uid);

  const { totalDays, unclaimedDays, rewardPerDay, todayCounted } = await db.runTransaction(async (tx) => {
    const [uSnap, sSnap, cfgSnap] = await Promise.all([
      tx.get(userRef),
      tx.get(sRef),
      tx.get(streakConfigRef),
    ]);

    if (!uSnap.exists) {
      throw new HttpsError("failed-precondition", "User doc missing");
    }

    const rewardPerDay = cfgSnap.exists ? Number(cfgSnap.get("reward") ?? 0) || 0 : 0;

    const lastDate: string = sSnap.exists ? (sSnap.get("lastLoginDate") as string) || "" : "";
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

    tx.set(
      sRef,
      {
        totalDays,
        unclaimedDays,
        lastLoginDate: today,
        createdAt: sSnap.exists ? (sSnap.get("createdAt") as Timestamp) ?? now : now,
        updatedAt: FieldValue.serverTimestamp(),
      },
      { merge: true }
    );

    tx.set(userRef, { updatedAt: FieldValue.serverTimestamp() }, { merge: true });

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

export const updateDailyStreak = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const res = await applyDailyStreakIncrement(uid);
  return { ok: true, ...res };
});

// -------- apply referral later (optional) --------
export const applyReferralCode = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const code = (req.data?.referralCode ?? "").toString();
  const result = await applyReferralCodeToUser(uid, code);
  return result;
});

// -------- session end: submit results (idempotent) --------
export const submitSessionResult = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const p = (req.data as Record<string, any>) || {};
  const sessionId = (p.sessionId || "").toString().trim();
  const earnedCurrency = Number(p.earnedCurrency) || 0;
  const earnedScore = Number(p.earnedScore) || 0;

  // NEW: Game Mode & Result (for energy refund logic)
  const mode = (p.mode || "endless").toString().trim().toLowerCase();
  const success = !!p.success;

  if (!sessionId) throw new HttpsError("invalid-argument", "sessionId required");

  // Session doc ref
  const userRef = db.collection("users").doc(uid);
  const sessRef = userRef.collection("sessions").doc(sessionId);

  // Transaction for safe write
  const res = await db.runTransaction(async (tx) => {
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
    const prevMaxCombo = Number(uSnap.get("maxCombo") ?? 0) || 0;

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
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });

    // mark session as processed
    tx.set(sessRef, { state: "completed", earnedCurrency, earnedScore, processedAt: Timestamp.now() }, { merge: true });

    return { alreadyProcessed: false, currency: newCurrency, maxScore: newBest };
  });
  try {
    const u = await db.collection("users").doc(uid).get();
    await Promise.all([
      upsertUserAch(uid, ACH_TYPES.ENDLESS_ROLLER, Number(u.get("sessionsPlayed") ?? 0) || 0),
      upsertUserAch(uid, ACH_TYPES.SCORE_CHAMPION, Number(u.get("maxScore") ?? 0) || 0),
      upsertUserAch(uid, ACH_TYPES.TOKEN_HUNTER, Number(u.get("cumulativeCurrencyEarned") ?? 0) || 0),
      upsertUserAch(uid, ACH_TYPES.COMBO_GOD, Number(u.get("maxCombo") ?? 0) || 0),
      upsertUserAch(
        uid,
        ACH_TYPES.TIME_DRIFTER,
        Number(u.get("totalPlaytimeMinutesFloat") ?? u.get("totalPlaytimeMinutes") ?? 0) || 0
      ),
      upsertUserAch(uid, ACH_TYPES.POWERUP_EXP, Number(u.get("powerUpsCollected") ?? 0) || 0),
    ]);
  } catch (e) {
    console.warn("[ach] post-session evaluate failed", e);
  }
  return result;
});

// -------- Energy (lazy regen) helpers --------
async function lazyRegenInTx(
  tx: FirebaseFirestore.Transaction,
  userRef: FirebaseFirestore.DocumentReference,
  now: Timestamp
): Promise<{ cur: number; max: number; period: number; nextAt: Timestamp | null }> {
  const snap = await tx.get(userRef);
  if (!snap.exists) throw new HttpsError("failed-precondition", "User doc missing");

  const max = Number(snap.get("energyMax") ?? 6) || 6;
  const period = Number(snap.get("energyRegenPeriodSec") ?? 14400) || 14400; // seconds
  let cur = Number(snap.get("energyCurrent") ?? 0) || 0;
  let updatedAt = (snap.get("energyUpdatedAt") as Timestamp | undefined) || now;

  if (cur < max) {
    const elapsedMs = Math.max(0, now.toMillis() - updatedAt.toMillis());
    const ticks = Math.floor(elapsedMs / (period * 1000));
    if (ticks > 0) {
      const newCur = Math.min(max, cur + ticks);
      const newUpdated =
        Timestamp.fromMillis(updatedAt.toMillis() + ticks * period * 1000);
      tx.set(userRef, {
        energyCurrent: newCur, energyUpdatedAt: newUpdated,
        updatedAt: FieldValue.serverTimestamp()
      }, { merge: true });
      cur = newCur;
      updatedAt = newUpdated;
    }
  }

  const nextAt = cur < max
    ? Timestamp.fromMillis(updatedAt.toMillis() + (period * 1000))
    : null;

  return { cur, max, period, nextAt };
}

// -------- getEnergySnapshot (callable, preferred by clients) --------
export const getEnergySnapshot = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();
  // Pre-clean expired consumables in a separate transaction
  await db.runTransaction(async (tx) => {
    await cleanupExpiredConsumablesInTx(tx, userRef, now);
  });

  const st = await db.runTransaction(async (tx) => {
    return await lazyRegenInTx(tx, userRef, now);
  });

  const nextMs = st.cur < st.max
    ? st.nextAt ? st.nextAt.toMillis() : (Timestamp.fromMillis(
      now.toMillis() + st.period * 1000
    ).toMillis())
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
export const getEnergyStatus = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();
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
export const spendEnergy = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const sessionId = String(req.data?.sessionId || "").trim();
  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();
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
      throw new HttpsError("failed-precondition", "Not enough energy");
    }

    const newCur = st.cur - 1;
    // spending resets the timer to now for a clean 4h window
    tx.set(userRef, {
      energyCurrent: newCur, energyUpdatedAt: now,
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });

    if (sessionId) {
      tx.set(userRef.collection("energySpends").doc(sessionId), { spentAt: now }, { merge: true });
    }

    const nextAt = Timestamp.fromMillis(now.toMillis() + st.period * 1000);
    return {
      alreadyProcessed: false, cur: newCur, max: st.max,
      period: st.period, nextAt
    };
  });

  return {
    ok: true,
    alreadyProcessed: !!(res as any).alreadyProcessed,
    energyCurrent: (res as any).cur,
    energyMax: (res as any).max,
    regenPeriodSec: (res as any).period,
    nextEnergyAt: (res as any).nextAt ? (res as any).nextAt.toDate()
      .toISOString() : null,
  };
});

// -------- grantBonusEnergy (callable, +1 life via ad / reward) --------
export const grantBonusEnergy = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  // İstersen burada ileride adToken / source vs. doğrulayabilirsin:
  // const adToken = String(req.data?.adToken || "");

  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();

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
    tx.set(
      userRef,
      {
        energyCurrent: newCur,
        updatedAt: FieldValue.serverTimestamp(),
      },
      { merge: true }
    ); // WRITE

    // nextAt: lazyRegenInTx zaten hesapladı; full olduysa null olabilir
    const nextAt =
      newCur < st.max
        ? st.nextAt ||
        Timestamp.fromMillis(now.toMillis() + st.period * 1000)
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
const userAutopilotRef = (uid: string) => db.collection("users").doc(uid).collection("autopilot").doc("state");

function clampNum(n: any, def = 0): number {
  const v = Number(n);
  return Number.isFinite(v) ? v : def;
}


function eliteActive(userSnap: FirebaseFirestore.DocumentSnapshot, nowMs: number): boolean {
  const ts = userSnap.get("elitePassExpiresAt") as Timestamp | null;
  return !!ts && ts.toMillis() > nowMs;
}

/**
 * Lazy settlement for Autopilot.
 * - Works for both Normal and Elite (both accrue into autopilotWallet)
 * - Normal is capped by maxHours; Elite has no cap
 * - Uses windowStart = max(autopilotLastClaimedAt, autopilotActivationDate) when ON
 */
async function settleAutopilotInTx(
  tx: FirebaseFirestore.Transaction,
  uid: string,
  now: Timestamp
): Promise<{
  userRef: FirebaseFirestore.DocumentReference,
  autoRef: FirebaseFirestore.DocumentReference,
  userData: Record<string, any>,
  auto: Record<string, any>,
  config: { normalRate: number; eliteRate: number; maxHours: number },
  isElite: boolean,
}> {
  const userRef = db.collection("users").doc(uid);
  const autoRef = userAutopilotRef(uid);
  const [cfgSnap, userSnap, autoSnap] = await Promise.all([
    tx.get(autopilotConfigRef),
    tx.get(userRef),
    tx.get(autoRef),
  ]);

  if (!userSnap.exists) throw new HttpsError("failed-precondition", "User doc missing");
  if (!cfgSnap.exists) throw new HttpsError("failed-precondition", "autopilotconfig missing");

  const config = {
    normalRate: clampNum(cfgSnap.get("normalUserEarningPerHour"), 0),
    eliteRate: clampNum(cfgSnap.get("eliteUserEarningPerHour"), 0),
    maxHours: Math.max(0, clampNum(cfgSnap.get("normalUserMaxAutopilotDurationInHours"), 12)),
  };

  const nowMs = now.toMillis();
  const isElite = eliteActive(userSnap, nowMs);

  const userData = userSnap.data() || {};
  const curCurrency = clampNum(userData.currency, 0);

  let auto: Record<string, any> = autoSnap.exists ? (autoSnap.data() || {}) : {};
  if (!autoSnap.exists) {
    auto = {
      autopilotWallet: 0,
      isAutopilotOn: false,
      autopilotActivationDate: null,
      autopilotLastClaimedAt: nowMs,
      totalEarnedViaAutopilot: 0,
      updatedAt: FieldValue.serverTimestamp(),
    };
    tx.set(autoRef, auto, { merge: true });
  }

  let gained = 0;
  if (isElite || auto.isAutopilotOn === true) {
    const lastClaim = clampNum(auto.autopilotLastClaimedAt, nowMs);
    let activation = null;
    if (isElite && auto.autopilotActivationDate === null) {
      activation = lastClaim;
    } else {
      activation = auto.autopilotActivationDate === null ? lastClaim : clampNum(auto.autopilotActivationDate,
        lastClaim);
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
      } else {
        // Cap for normal
        const capGainRaw = Math.max(0, config.normalRate * config.maxHours);
        const capGain = Math.floor(capGainRaw * 100) / 100; // cap with 2-decimal precision
        const current = clampNum(auto.autopilotWallet, 0);
        const newWallet = Math.min(current + potential, capGain);
        gained = Math.max(0, newWallet - current);
        auto.autopilotWallet = newWallet;
      }
      auto.totalEarnedViaAutopilot = clampNum(auto.totalEarnedViaAutopilot, 0) + gained;
      auto.updatedAt = FieldValue.serverTimestamp();
      // We do NOT advance autopilotLastClaimedAt here; it only moves on claim.
      tx.set(autoRef, auto, { merge: true });
    }
  }

  return { userRef, autoRef, userData: { ...userData, currency: curCurrency }, auto, config, isElite };
}

// -------- getAutopilotStatus (callable) --------
export const getAutopilotStatus = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const now = Timestamp.now();
  // Run settlement in a tx to keep reads/writes ordered
  const out = await db.runTransaction(async (tx) => {
    const s = await settleAutopilotInTx(tx, uid, now);
    // Compute helpers for payload
    const wallet = clampNum(s.auto.autopilotWallet, 0);

    const capSec = Math.floor(s.config.normalRate > 0 ? s.config.maxHours * 3600 : 0);

    const lastClaimMs = clampNum(s.auto.autopilotLastClaimedAt, now.toMillis());
    const activationMs = typeof s.auto.autopilotActivationDate === 'number' ? s.auto.autopilotActivationDate : null;
    const windowStartMs = activationMs !== null ? Math.max(lastClaimMs, activationMs) : lastClaimMs;

    let timeToCapSeconds: number | null = null;
    let isClaimReady = false;

    if (s.isElite) {
      timeToCapSeconds = null;     // Elite has no time cap
      isClaimReady = true;         // Elite can claim anytime
    } else {
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

  return { ok: true, serverNowMillis: now.toMillis(), ...out };
});

// -------- toggleAutopilot (callable) --------
export const toggleAutopilot = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const on = !!req.data?.on;

  const now = Timestamp.now();
  const userRef = db.collection("users").doc(uid);
  const autoRef = userAutopilotRef(uid);

  await db.runTransaction(async (tx) => {
    // First settle current window so we don't lose any elapsed time
    await settleAutopilotInTx(tx, uid, now);

    if (on) {
      tx.set(autoRef, {
        isAutopilotOn: true,
        autopilotActivationDate: now.toMillis(),
        updatedAt: FieldValue.serverTimestamp(),
      }, { merge: true });
    } else {
      // Turning OFF: close the window by simply disabling and clearing activation
      tx.set(autoRef, {
        isAutopilotOn: false,
        autopilotActivationDate: null,
        updatedAt: FieldValue.serverTimestamp(),
      }, { merge: true });
    }

    // Touch user updatedAt for visibility
    tx.set(userRef, { updatedAt: FieldValue.serverTimestamp() }, { merge: true });
  });

  return { ok: true, isAutopilotOn: on };
});

// -------- claimAutopilot (callable) --------
export const claimAutopilot = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const now = Timestamp.now();
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
      throw new HttpsError("failed-precondition", "Missing user/autopilot state");
    }

    const curCurrency = clampNum(uSnap.get("currency"), 0);
    const wallet = clampNum(aSnap.get("autopilotWallet"), 0);

    const nowMs = now.toMillis();
    const isElite = eliteActive(uSnap, nowMs);

    if (!isElite) {
      if (!configSnap.exists) {
        throw new HttpsError("failed-precondition", "autopilotconfig missing");
      }
      const normalRate = clampNum(configSnap.get("normalUserEarningPerHour"), 0);
      const maxHours = Math.max(
        0,
        clampNum(configSnap.get("normalUserMaxAutopilotDurationInHours"), 12)
      );

      // time-based readiness check for normal users
      const capSec = Math.floor((normalRate > 0 ? maxHours : 0) * 3600);
      const lastClaimMs = clampNum(aSnap.get("autopilotLastClaimedAt"), now.toMillis());
      const activationMs =
        typeof aSnap.get("autopilotActivationDate") === "number"
          ? (aSnap.get("autopilotActivationDate") as number)
          : null;
      const windowStartMs =
        activationMs !== null ? Math.max(lastClaimMs, activationMs) : lastClaimMs;
      const elapsedSec = Math.max(0, Math.floor((now.toMillis() - windowStartMs) / 1000));

      if (capSec > 0 && elapsedSec < capSec) {
        throw new HttpsError("failed-precondition", "Not ready to claim");
      }
    }

    // === WRITES (after reads) ===
    if (wallet > 0) {
      tx.set(
        userRef,
        { currency: curCurrency + wallet, updatedAt: FieldValue.serverTimestamp() },
        { merge: true }
      );

      const baseUpdate: any = {
        autopilotWallet: 0,
        autopilotLastClaimedAt: now.toMillis(),
        updatedAt: FieldValue.serverTimestamp(),
      };

      if (!isElite) {
        baseUpdate.isAutopilotOn = false;
        baseUpdate.autopilotActivationDate = null;
      }

      tx.set(autoRef, baseUpdate, { merge: true });
    } else {
      const baseUpdate: any = {
        autopilotLastClaimedAt: now.toMillis(),
        updatedAt: FieldValue.serverTimestamp(),
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

export const listReferredUsers = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const limit = Math.max(1, Math.min(Number(req.data?.limit ?? 100), 500));
  const includeEarnings = !!req.data?.includeEarnings;

  const usersCol = db.collection("users");

  const mapDoc = async (doc: FirebaseFirestore.DocumentSnapshot) => {
    const d = doc.data() || {};
    const childUid = doc.id;
    const username =
      (typeof d.username === "string" ? d.username : "").trim() || "Guest";
    const currency = typeof d.currency === "number" ? d.currency : 0;

    const createdAt = (
      d.createdAt as Timestamp | undefined
    )?.toDate()?.toISOString() ?? null;
    const referralAppliedAt = (
      d.referralAppliedAt as Timestamp | undefined
    )?.toDate()?.toISOString() ?? null;

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
  } catch (err: any) {
    const msg = String(err?.message ?? "");
    const needsIndex = err?.code === 9 || /index/i.test(msg);
    if (!needsIndex) {
      console.error("listReferredUsers error:", err);
      throw new HttpsError("internal", "Query failed.");
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
        note:
          "Add composite index on users: referredByUid ASC, createdAt DESC (database 'getfi') to enable sorted results.",
        items,
      };
    } catch (e2: any) {
      console.error("listReferredUsers fallback error:", e2);
      throw new HttpsError("internal", "Query failed (fallback).");
    }
  }
});

export const getGalleryItems = onCall(async (req) => {
  const collectionPath =
    (req.data?.collectionPath as string) ||
    "appdata/galleryitems/itemdata";

  let ids: string[] = Array.isArray(req.data?.ids)
    ? req.data.ids.slice(0, 10).map((x: any) => String(x))
    : ["galleryview_1", "galleryview_2", "galleryview_3"];

  if (!collectionPath.startsWith("appdata/")) {
    throw new HttpsError("permission-denied", "Invalid collectionPath");
  }

  const pickString = (obj: Record<string, any>, keys: string[]): string => {
    for (const k of keys) {
      const v = obj?.[k];
      if (typeof v === "string" && v.trim().length > 0) return v;
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
        console.log(
          `[getGalleryItems] ${s.id} pngUrl='${pngUrl}' guidance='${guidanceKey}' descLen=${descriptionText.length}`
        );

        return {
          id: s.id,
          pngUrl,
          descriptionText,
          guidanceKey,
        };
      });

    return { ok: true, items };

  } catch (e) {
    console.error("getGalleryItems error:", e);
    throw new HttpsError("internal", "Failed to fetch gallery items.");
  }
});

export const indexMapMeta = onDocumentWritten(
  { document: "appdata/maps/{mapId}/raw", database: DB_ID },
  async (event) => {
    const mapId = event.params.mapId as string;

    // If the raw doc was deleted -> remove from pools and meta
    if (!event.data?.after?.exists) {
      const metaDoc = db.collection("appdata").doc("maps_meta");
      const byMapRef = metaDoc.collection("by_map").doc(mapId);
      const poolsCol = metaDoc.collection("pools");

      await db.runTransaction(async (tx) => {
        tx.delete(byMapRef);
        for (const d of [1, 2, 3]) {
          const pRef = poolsCol.doc(String(d));
          tx.set(
            pRef,
            {
              ids: FieldValue.arrayRemove(mapId),
              updatedAt: FieldValue.serverTimestamp(),
            },
            { merge: true }
          );
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
    } else {
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
      const d = Number(parsed?.difficultyTag ?? 0);
      if ([1, 2, 3].includes(d)) difficultyTag = d;
    } catch (e) {
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
    const now = Timestamp.now();

    await db.runTransaction(async (tx) => {
      // Upsert by_map/{mapId}
      const prev = await tx.get(byMapRef);
      const prevDiff = prev.exists
        ? Number(prev.get("difficultyTag") ?? 0)
        : 0;

      tx.set(
        byMapRef,
        {
          difficultyTag,
          updatedAt: FieldValue.serverTimestamp(),
          ...(prev.exists ? {} : { createdAt: now }),
        },
        { merge: true }
      );

      // Maintain pools/{1|2|3}.ids arrays
      for (const d of [1, 2, 3]) {
        const pRef = poolsCol.doc(String(d));
        if (d === difficultyTag) {
          tx.set(
            pRef,
            {
              ids: FieldValue.arrayUnion(mapId),
              updatedAt: FieldValue.serverTimestamp(),
            },
            { merge: true }
          );
        } else if (prevDiff && d === prevDiff) {
          tx.set(
            pRef,
            {
              ids: FieldValue.arrayRemove(mapId),
              updatedAt: FieldValue.serverTimestamp(),
            },
            { merge: true }
          );
        } else {
          // no-op
        }
      }
    });

    console.log(
      `[indexMapMeta] indexed '${mapId}' diff=${difficultyTag}`
    );
  }
);
// ---------------- Sequenced Random Maps (callable) ----------------
export const getSequencedMaps = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const clamp = (n: number, a: number, b: number) => Math.max(a, Math.min(b, n));
  const count = clamp(Number(req.data?.count ?? 24), 1, 50);
  const seedIn = String(req.data?.seed ?? "");

  // tiny deterministic RNG (xorshift32-like) from a seed string
  const makeRng = (s: string) => {
    let h = 2166136261 >>> 0;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619) >>> 0;
    }
    return () => {
      h ^= h << 13; h >>>= 0;
      h ^= h >>> 17; h >>>= 0;
      h ^= h << 5; h >>>= 0;
      return (h >>> 0) / 4294967296;
    };
  };
  const rng = seedIn ? makeRng(seedIn) : Math.random;

  // 1) read recurring difficulty curve
  const curveRef = db.collection("appdata").doc("recurring_difficulty_curve");
  const curveSnap = await curveRef.get();
  const curve = curveSnap.exists ? curveSnap.data() ?? {} : {};
  const easy = clamp(Number(curve.easy ?? 3), 0, 24);
  const medium = clamp(Number(curve.medium ?? 2), 0, 24);
  const hard = clamp(Number(curve.hard ?? 1), 0, 24);

  const cycle: number[] = [
    ...Array(easy).fill(1),
    ...Array(medium).fill(2),
    ...Array(hard).fill(3),
  ];
  if (cycle.length === 0) {
    throw new HttpsError("failed-precondition", "Empty pattern");
  }

  const pattern: number[] = [];
  while (pattern.length < count) {
    for (const d of cycle) {
      if (pattern.length >= count) break;
      pattern.push(d);
    }
  }

  // 2) load pools
  const poolsCol = db.collection("appdata")
    .doc("maps_meta").collection("pools");

  const [p1, p2, p3] = await Promise.all(
    [1, 2, 3].map(async (d) => {
      const s = await poolsCol.doc(String(d)).get();
      const ids = (s.exists ? (s.get("ids") as string[] | undefined) : []) || [];
      // copy and shallow-shuffle for variety
      const arr = ids.slice();
      for (let i = arr.length - 1; i > 0; i--) {
        const j = Math.floor((rng() || Math.random()) * (i + 1));
        const t = arr[i]; arr[i] = arr[j]; arr[j] = t;
      }
      return { d, ids, bag: arr };
    })
  );

  const poolByDiff: Record<number, { d: number; ids: string[]; bag: string[] }> = {
    1: p1, 2: p2, 3: p3
  };

  // helper to take one id from pool; allows repeats if exhausted
  const takeFrom = (d: number): string => {
    const p = poolByDiff[d];
    if (!p) throw new HttpsError("not-found", `Pool ${d} missing`);
    if (p.bag.length === 0) {
      if (p.ids.length === 0) {
        throw new HttpsError("not-found", `Pool ${d} is empty`);
      }
      // refill (allow repeats after exhaustion)
      p.bag = p.ids.slice();
    }
    return p.bag.pop() as string;
  };

  // 3) pick ids by pattern
  const chosen: { mapId: string; difficultyTag: number }[] = [];
  for (const d of pattern) {
    const id = takeFrom(d);
    chosen.push({ mapId: id, difficultyTag: d });
  }

  // 4) fetch raw jsons in parallel (dedup reads)
  const uniqueIds = Array.from(new Set(chosen.map(x => x.mapId)));
  const rawCol = db.collection("appdata").doc("maps");
  const snaps = await Promise.all(
    uniqueIds.map(id => rawCol.collection(id).doc("raw").get())
  );
  const byId: Record<string, string> = {};
  for (let i = 0; i < uniqueIds.length; i++) {
    const id = uniqueIds[i];
    const s = snaps[i];
    if (!s.exists) {
      throw new HttpsError("not-found", `raw missing: ${id}`);
    }
    const d = s.data() ?? {};
    let jsonStr = "";
    if (typeof (d as any).json === "string") {
      jsonStr = (d as any).json as string;
    } else {
      const ks = Object.keys(d);
      if (ks.length === 1 && typeof (d as any)[ks[0]] === "string") {
        jsonStr = (d as any)[ks[0]] as string;
      }
    }
    if (typeof jsonStr !== "string" || jsonStr.length === 0) {
      throw new HttpsError("data-loss", `raw.json not string for ${id}`);
    }
    byId[id] = jsonStr;
  }

  // 5) assemble entries
  const entries = chosen.map(x => {
    const js = byId[x.mapId];
    if (typeof js !== "string") {
      throw new HttpsError("data-loss", `json not string for ${x.mapId}`);
    }
    return { mapId: x.mapId, difficultyTag: x.difficultyTag, json: js };
  });

  console.log(
    `[getSequencedMaps] uid=${uid} count=${count} pat=${pattern.join("")}`
  );

  return {
    ok: true,
    count,
    pattern,
    entries
  };
});
// -------- requestSession (callable) --------
export const requestSession = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();
  // Pre-clean expired consumables in a separate transaction
  await db.runTransaction(async (tx) => {
    await cleanupExpiredConsumablesInTx(tx, userRef, now);
  });

  const out = await db.runTransaction(async (tx) => {
    // 1) lazy regen inside tx
    const st = await lazyRegenInTx(tx, userRef, now);
    if (st.cur <= 0) {
      throw new HttpsError("failed-precondition", "Not enough energy");
    }

    // 2) spend 1 energy and reset timer window to now
    const newCur = st.cur - 1;
    tx.set(userRef, {
      energyCurrent: newCur, energyUpdatedAt: now,
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });

    // 3) create server-owned session doc
    const sessionId = `${now.toMillis()}_${Math.random().toString(36).slice(2, 10)}`;
    const sessRef = userRef.collection("sessions").doc(sessionId);
    tx.set(sessRef, { state: "granted", startedAt: now }, { merge: true });

    const nextAt = Timestamp.fromMillis(now.toMillis() + st.period * 1000);
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
export const getAllItems = onCall(async (request) => {
  try {
    console.log(`[getAllItems:start] db=${DB_ID}`);
    const itemsCol = db.collection("appdata").doc("items");
    const itemsSnap = await itemsCol.listCollections();

    const out: Record<string, any> = {};

    for (const subCol of itemsSnap) {
      const docSnap = await subCol.doc("itemdata").get();
      if (!docSnap.exists) continue;
      out[subCol.id] = docSnap.data();
    }

    const sample = Object.keys(out).slice(0, 5);
    console.log(`[getAllItems:done] count=${Object.keys(out).length} sample=[${sample.join(', ')}]`);
    return {
      ok: true,
      items: out,
      count: Object.keys(out).length,
    };
  } catch (err) {
    console.error("[getAllItems] error", err);
    return {
      ok: false,
      error: (err as Error).message || "unknown",
    };
  }
});

// ========================= createItem =========================
// Unity Editor'dan (Odin butonuyla) çağırmak için:
// Callable name: createItem
// Path: appdata/items/{itemId}/itemdata  (itemId = "item_" + slug(itemName))
export const createItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const p = (req.data as Record<string, any>) || {};

  // ---- helper'lar
  const num = (v: any, def = 0) =>
    Number.isFinite(Number(v)) ? Number(v) : def;
  const str = (v: any, def = "") =>
    typeof v === "string" ? v : def;
  const bool = (v: any, def = false) =>
    typeof v === "boolean" ? v : !!def;

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
    itemIconUrl: str(
      p.itemIconUrl,
      "https://cdn-icons-png.freepik.com/256/4957/4957671.png"
    ),
    itemIsConsumable: bool(p.itemIsConsumable, false),
    itemIsRewardedAd: bool(p.itemIsRewardedAd, false),
    itemName,
    itemReferralThreshold: num(p.itemReferralThreshold, 0),

    itemstat_coinMultiplierPercent: num(p.itemstat_coinMultiplierPercent, 0),
    itemstat_comboPower: num(p.itemstat_comboPower, 0),
    itemstat_gameplaySpeedMultiplierPercent: num(
      p.itemstat_gameplaySpeedMultiplierPercent,
      0
    ),
    itemstat_magnetPowerPercent: num(p.itemstat_magnetPowerPercent, 0),
    itemstat_playerAcceleration: num(p.itemstat_playerAcceleration, 0),
    itemstat_playerSizePercent: num(p.itemstat_playerSizePercent, 0),
    itemstat_playerSpeed: num(p.itemstat_playerSpeed, 0),

    createdAt: Timestamp.now(),
    updatedAt: FieldValue.serverTimestamp(),
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

  throw new HttpsError(
    "aborted",
    "Could not allocate a unique itemId after several attempts."
  );
});

// ========================= checkOwnership =========================
// Kullanıcının sahip olduğu item ID'lerini döndürür (normalize edilerek).
// Sahiplik kriteri: owned == true  **veya** quantity > 0  (consumable destekli)
export const checkOwnership = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  console.log(`[checkOwnership:start] uid=${uid} db=${DB_ID}`);

  const userRef = db.collection("users").doc(uid);
  const invCol = userRef.collection("inventory");

  // 1) owned=true olanlar
  const ownedQ = await invCol.where("owned", "==", true).get();

  // 2) quantity>0 olanlar (consumable'lar için)
  // Not: Firestore 'OR' desteklemediği için iki sorguyu birleştiriyoruz.
  let qtyQDocs: FirebaseFirestore.QueryDocumentSnapshot[] = [];
  try {
    const qtyQ = await invCol.where("quantity", ">", 0).get();
    qtyQDocs = qtyQ.docs;
  } catch (e) {
    // quantity alanı yoksa da sorun değil; sadece owned=true setine güveniriz.
    qtyQDocs = [];
  }

  // 3) Birleştir + normalize et + tekilleştir
  const set = new Set<string>();
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
export const getInventorySnapshot = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const userRef = db.collection("users").doc(uid);
  const invCol = userRef.collection("inventory");
  const loadRef = userRef.collection("loadout").doc("current");
  const [invSnap, loadSnap] = await Promise.all([
    invCol.get(),
    loadRef.get()
  ]);
  console.log(`[getInventorySnapshot] uid=${uid} invDocs=${invSnap.size} loadoutDoc=${loadSnap.exists} db=${DB_ID}`);
  const inventory: Record<string, any> = {};
  invSnap.forEach((d) => {
    const id = normId(d.id);
    // include normalized id in payload for clients if needed
    inventory[id] = { id, ...d.data() };
  });
  const equippedItemIds: string[] = loadSnap.exists
    ? (loadSnap.get("equippedItemIds") || []).map((x: string) => normId(x))
    : [];
  return { ok: true, inventory, equippedItemIds };
});

// ---------------- purchaseItem ----------------
export const purchaseItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  // method: "GET" | "IAP" | "AD" | "PREMIUM"
  const {
    itemId: rawItemId,
    method,
    platform,
    receipt,
    orderId,
    adToken,
  } = (req.data || {}) as {
    itemId?: string;
    method?: string;
    platform?: string;
    receipt?: string;
    orderId?: string;
    adToken?: string;
  };

  const itemId = normId(rawItemId);
  if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");
  const m = String(method || "").toUpperCase();
  if (!["GET", "IAP", "AD", "PREMIUM"].includes(m)) {
    throw new HttpsError("invalid-argument", "Invalid method. Use GET | IAP | AD | PREMIUM.");
  }

  const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
  const userRef = db.collection("users").doc(uid);
  const invRef = userRef.collection("inventory").doc(itemId);
  const acRef = userRef.collection("activeConsumables").doc(itemId);
  const now = Timestamp.now();

  // ---- Pre-verify tokens outside transaction to avoid granting on failed verification ----
  const verifyIapReceipt = async (platform?: string, receipt?: string, orderId?: string) => {
    if (!platform || !receipt || !orderId) {
      throw new HttpsError("invalid-argument", "platform, receipt and orderId are required for IAP.");
    }
    const lockRef = userRef.collection("iapReceipts").doc(orderId);
    const existing = await lockRef.get();
    if (existing.exists) {
      throw new HttpsError("already-exists", "This purchase receipt was already processed.");
    }
    await lockRef.set({ usedAt: now, platform, previewHash: String(receipt).slice(0, 32) }, { merge: true });
  };
  const verifyAdGrant = async (adToken?: string) => {
    if (!adToken) throw new HttpsError("invalid-argument", "adToken required for AD method.");
    const grantRef = userRef.collection("adGrants").doc(adToken);
    const g = await grantRef.get();
    if (g.exists) {
      throw new HttpsError("already-exists", "This ad grant token was already used.");
    }
    await grantRef.set({ usedAt: now }, { merge: true });
  };

  if (m === "IAP") {
    await verifyIapReceipt(platform, receipt, orderId);
  } else if (m === "AD") {
    await verifyAdGrant(adToken);
  }

  const res = await db.runTransaction(async (tx) => {
    // ---- READS FIRST (all of them) ----
    const [itemSnap, userSnap, invSnap, acSnap] = await Promise.all([
      tx.get(itemRef),
      tx.get(userRef),
      tx.get(invRef),
      tx.get(acRef),
    ]);

    if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found.");
    const item = itemSnap.data() || {};

    const isReferralOnly = Number(item.itemReferralThreshold ?? 0) > 0;
    if (isReferralOnly) {
      throw new HttpsError("failed-precondition", "Referral-only item cannot be purchased.");
    }

    const isConsumable = !!item.itemIsConsumable;
    const priceGet = Number(item.itemGetPrice ?? 0) || 0;      // in-game currency
    const pricePremium = Number(item.itemPremiumPrice ?? 0) || 0;      // premium currency
    const priceUsd = Number(item.itemDollarPrice ?? 0) || 0;   // real money price hint
    const isAd = !!item.itemIsRewardedAd;

    // Validate method vs item flags
    if (m === "GET" && priceGet <= 0) {
      throw new HttpsError("failed-precondition", "This item is not purchasable with game currency.");
    }
    if (m === "IAP" && priceUsd <= 0) {
      throw new HttpsError("failed-precondition", "This item is not an IAP item.");
    }
    if (m === "AD" && !isAd) {
      throw new HttpsError("failed-precondition", "This item is not ad-reward purchasable.");
    }
    if (m === "PREMIUM" && pricePremium <= 0) {
      throw new HttpsError("failed-precondition", "This item is not purchasable with premium currency.");
    }

    // Ownership check (non-consumables cannot be purchased twice)
    const alreadyOwned = invSnap.exists && !!invSnap.get("owned");
    if (alreadyOwned && !isConsumable) {
      throw new HttpsError("failed-precondition", "Already owned.");
    }

    // ---- WRITES AFTER READS ----
    // Charge / touch user depending on method
    if (m === "GET") {
      const curBalance = Number(userSnap.get("currency") ?? 0) || 0;
      if (curBalance < priceGet) {
        throw new HttpsError("failed-precondition", "Not enough currency.");
      }
      tx.update(userRef, { currency: curBalance - priceGet, updatedAt: FieldValue.serverTimestamp() });
    } else if (m === "PREMIUM") {
      const curPremium = Number(userSnap.get("premiumCurrency") ?? 0) || 0;
      if (curPremium < pricePremium) {
        throw new HttpsError("failed-precondition", "Not enough premium currency.");
      }
      tx.update(userRef, { premiumCurrency: curPremium - pricePremium, updatedAt: FieldValue.serverTimestamp() });
    } else {
      // touch the user doc for bookkeeping
      tx.set(userRef, { updatedAt: FieldValue.serverTimestamp() }, { merge: true });
    }

    // Prepare audit expiry holder
    let newExpiry: Timestamp | null = null;

    // Upsert inventory / Activate consumable
    // Upsert inventory / Activate consumable
    if (isConsumable) {
      // Determine previous active state from existing activeConsumables doc
      const prevExpiry: Timestamp | null = acSnap.exists
        ? (acSnap.get("expiresAt") as Timestamp | null)
        : null;

      const wasActive = !!prevExpiry && prevExpiry.toMillis() > now.toMillis();
      const baseMillis = (wasActive && prevExpiry) ? prevExpiry.toMillis() : now.toMillis();
      const durationMs = 24 * 60 * 60 * 1000; // 24h per purchase
      newExpiry = Timestamp.fromMillis(baseMillis + durationMs);

      // Persist/extend active window (no inventory write for consumables)
      tx.set(
        acRef,
        {
          itemId,
          active: true,
          expiresAt: newExpiry,
          lastActivatedAt: now,
          updatedAt: FieldValue.serverTimestamp(),
        },
        { merge: true }
      );

      // On first activation only, add stats to user
      if (!wasActive) {
        const itemStats = extractItemStats(item);
        if (Object.keys(itemStats).length > 0) {
          const baseStats = parseStatsJson(userSnap.get("statsJson"));
          const merged = mergeStats(baseStats, itemStats, 1);
          tx.update(userRef, {
            statsJson: JSON.stringify(merged),
            updatedAt: FieldValue.serverTimestamp(),
          });
        }
      }

      // (No inventory doc creation for consumables)
    } else {
      // Non-consumables: mark owned as before
      const invData: any = {
        owned: true,
        equipped: invSnap.get("equipped") === true, // preserve equip
        quantity: 0,
        itemIsConsumable: false,
        lastChangedAt: FieldValue.serverTimestamp(),
        acquiredAt: invSnap.exists ? invSnap.get("acquiredAt") ?? now : now,
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
      itemsPurchasedCount: FieldValue.increment(1),
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });

    // Build response snapshot
    const currencyLeft = m === "GET"
      ? Math.max(0, (Number(userSnap.get("currency") ?? 0) || 0) - priceGet)
      : Number(userSnap.get("currency") ?? 0) || 0;

    const premiumCurrencyLeft = m === "PREMIUM"
      ? Math.max(0, (Number(userSnap.get("premiumCurrency") ?? 0) || 0) - pricePremium)
      : Number(userSnap.get("premiumCurrency") ?? 0) || 0;

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
    await upsertUserAch(uid, ACH_TYPES.MARKET_WHISPER, Number(u.get("itemsPurchasedCount") ?? 0) || 0);
  } catch (e) { console.warn("[ach] purchase evaluate failed", e); }

  return res;
});

// ---------------- getActiveConsumables (callable) ----------------
// Returns active consumables with future expiry; expired ones are omitted on read.
export const getActiveConsumables = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();
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
    const exp = (data.expiresAt as Timestamp | undefined) || null;
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
export const equipItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const rawItemId = (req.data?.itemId as string) || "";
  const itemId = normId(rawItemId);
  if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");

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
      throw new HttpsError("failed-precondition", "Item not owned.");
    }
    if (!itemSnap.exists) {
      throw new HttpsError("not-found", "Item not found.");
    }

    const isConsumable = !!itemSnap.get("itemIsConsumable");
    if (isConsumable) {
      throw new HttpsError("failed-precondition", "Consumables cannot be equipped.");
    }

    // Normalize current equipped list
    let equipped: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
    equipped = equipped.map((x: string) => normId(x));

    const wasEquipped = equipped.includes(itemId);
    if (!wasEquipped) {
      equipped.push(itemId);
    }

    // ---- WRITES AFTER ALL READS ----
    tx.set(
      loadRef,
      { equippedItemIds: equipped, updatedAt: FieldValue.serverTimestamp() },
      { merge: true }
    );
    tx.set(
      invRef,
      { equipped: true, lastChangedAt: FieldValue.serverTimestamp() },
      { merge: true }
    );

    // Merge item stats into user's stats only on first-time equip
    if (!wasEquipped) {
      const baseStats = parseStatsJson(userSnap.get("statsJson"));
      const itemStats = extractItemStats(itemSnap.data() || {});
      const merged = mergeStats(baseStats, itemStats, 1);
      tx.update(userRef, {
        statsJson: JSON.stringify(merged),
        updatedAt: FieldValue.serverTimestamp(),
      });
    }
  });

  return { ok: true, itemId };
});

// ---------------- unequipItem ----------------
export const unequipItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const rawItemId = (req.data?.itemId as string) || "";
  const itemId = normId(rawItemId);
  if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");

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

    let before: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
    const beforeNorm = before.map((x: string) => normId(x));
    const wasEquipped = beforeNorm.includes(itemId);

    const afterEquipped = beforeNorm.filter((x) => x !== itemId);

    // ---- WRITES AFTER ALL READS ----
    tx.set(
      loadRef,
      { equippedItemIds: afterEquipped, updatedAt: FieldValue.serverTimestamp() },
      { merge: true }
    );
    tx.set(
      invRef,
      { equipped: false, lastChangedAt: FieldValue.serverTimestamp() },
      { merge: true }
    );

    // Subtract item stats from user's stats only if it was previously equipped
    if (wasEquipped) {
      if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found.");
      const baseStats = parseStatsJson(userSnap.get("statsJson"));
      const itemStats = extractItemStats(itemSnap.data() || {});
      const merged = mergeStats(baseStats, itemStats, -1);
      tx.update(userRef, {
        statsJson: JSON.stringify(merged),
        updatedAt: FieldValue.serverTimestamp(),
      });
    }
  });

  return { ok: true, itemId };
});

// -------- recomputeRanks (callable) --------
export const recomputeRanks = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const res = await recomputeAllRanks();
  return { ok: true, updated: res.count };
});

// ========================= Change Username =========================

export const changeUsername = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const newNameRaw = String(req.data?.newName ?? "").trim();
  const now = Timestamp.now();
  const newNameLower = newNameRaw.toLowerCase();

  // --- Basic format validation ---
  if (newNameRaw.length < 3 || newNameRaw.length > 20) {
    throw new HttpsError("invalid-argument", "USERNAME_INVALID_LENGTH");
  }

  if (!/^[a-zA-Z0-9._-]+$/.test(newNameRaw)) {
    throw new HttpsError("invalid-argument", "USERNAME_INVALID_CHARS");
  }

  // --- Banned keyword rules from Firestore (config driven) ---
  // Path: appdata/usernamerules  (doc)
  // Field: bannedKeywords (array)  veya bannedkeywords (array)
  let bannedList: string[] = [
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
      const d = (rulesSnap.data() || {}) as Record<string, any>;
      let fromField =
        d.bannedKeywords ??
        d.bannedkeywords;

      // CASE 1: Already an array
      if (Array.isArray(fromField)) {
        bannedList = fromField
          .map((x) => String(x || "").toLowerCase().trim())
          .filter((s) => s.length > 0);

        // CASE 2: A raw string → attempt JSON parse first
      } else if (typeof fromField === "string" && fromField.trim().length > 0) {
        const raw = fromField.trim();
        let parsedOk = false;

        // Try JSON parse
        try {
          const parsed = JSON.parse(raw);
          if (parsed && Array.isArray(parsed.bannedKeywords)) {
            bannedList = parsed.bannedKeywords
              .map((x: any) => String(x || "").toLowerCase().trim())
              .filter((s: string) => s.length > 0);
            parsedOk = true;
          }
        } catch (_) { }

        // Fallback: split by comma/whitespace
        if (!parsedOk) {
          bannedList = raw
            .split(/[,\s]+/)
            .map((x) => x.toLowerCase().trim())
            .filter((s) => s.length > 0);
        }
      }
    }
  } catch (e) {
    console.warn("[changeUsername] could not load usernamerules, using fallback list:", e);
  }

  const lower = newNameLower;
  for (const bad of bannedList) {
    if (!bad) continue;
    if (lower.includes(bad)) {
      throw new HttpsError("invalid-argument", "USERNAME_BAD_WORD");
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
      throw new HttpsError("failed-precondition", "USER_DOC_MISSING");
    }

    const data = uSnap.data() || {};
    const lastChangedAt = data.usernameLastChangedAt as Timestamp | null;

    // --- Haftada 1 değişim limiti ---
    if (lastChangedAt) {
      const diff = now.toMillis() - lastChangedAt.toMillis();
      const WEEK_MS = 7 * 24 * 60 * 60 * 1000;
      if (diff < WEEK_MS) {
        throw new HttpsError("failed-precondition", "USERNAME_CHANGE_TOO_SOON");
      }
    }

    // --- Benzersiz username kontrolü ---
    if (nameSnap.exists) {
      const owner = nameSnap.get("uid");
      if (owner !== uid) {
        throw new HttpsError("already-exists", "USERNAME_TAKEN");
      }
    }

    // Eski username rezervasyonunu sil
    const oldName = (data.username || "").toLowerCase();
    if (oldName) {
      const oldRef = db.collection("usernames").doc(oldName);
      tx.delete(oldRef);
    }

    // Yeni username'i rezerve et
    tx.set(
      nameRef,
      { uid, updatedAt: now },
      { merge: true }
    );

    // User doc'u güncelle
    tx.set(
      userRef,
      {
        username: newNameRaw,
        usernameLastChangedAt: now,
        updatedAt: FieldValue.serverTimestamp(),
      },
      { merge: true }
    );
  });

  return { ok: true, newName: newNameRaw };
});