import * as admin from "firebase-admin";
import * as functionsV1 from "firebase-functions/v1";
import {onDocumentWritten} from "firebase-functions/v2/firestore";
import {onCall, HttpsError} from "firebase-functions/v2/https";
import {setGlobalOptions} from "firebase-functions/v2/options";
import {Firestore, Timestamp, FieldValue} from "@google-cloud/firestore";

admin.initializeApp();
// nam5 -> us-central1
setGlobalOptions({region:"us-central1"});

const DB_ID = "getfi";
const SEASON = "current";
const TOP_N = 50;

const db = new Firestore({databaseId:DB_ID});

// ---------------- Leaderboard Sync ----------------
export const syncLeaderboard = onDocumentWritten(
  {document:"users/{uid}", database:DB_ID},
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
    await seasonRef.collection("entries").doc(uid).set(
      {username, score, updatedAt:FieldValue.serverTimestamp()},
      {merge:true}
    );

    // 2) materialize topN
    const topRef = seasonRef.collection("meta").doc(`top${TOP_N}`);

    type Entry = {uid:string; username:string; score:number};

    await db.runTransaction(async (tx) => {
      const snap = await tx.get(topRef);

      let entries: Entry[] = [];
      const rawEntries = snap.exists ? snap.data()?.entries : null;

      if (Array.isArray(rawEntries)) {
        entries = rawEntries
          .map((e:any) => ({
            uid:String(e?.uid ?? ""),
            username:String(e?.username ?? "Guest"),
            score:Number(e?.score ?? 0)
          }))
          .filter((e:Entry) => e.uid.length > 0);
      }

      const i = entries.findIndex((e) => e.uid === uid);
      const updated: Entry = {uid, username, score};
      if (i >= 0) entries[i] = updated; else entries.push(updated);

      entries.sort((a,b) => b.score - a.score);
      if (entries.length > TOP_N) entries = entries.slice(0, TOP_N);

      tx.set(
        topRef,
        {entries, updatedAt:FieldValue.serverTimestamp()},
        {merge:true}
      );
    });
  }
);

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
        hasElitePass:true,
        elitePassExpiresAt:newExpiry,
        updatedAt:FieldValue.serverTimestamp()
      },
      {merge:true}
    );

    if (purchaseId) {
      tx.set(
        userRef.collection("elitePassPurchases").doc(purchaseId),
        {processedAt:now, newExpiry},
        {merge:true}
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

async function reserveUniqueReferralKey(uid:string): Promise<string> {
  for (let i = 0; i < 6; i++) {
    const k = randomReferralKey(12);
    const ref = db.collection("referralKeys").doc(k);
    const snap = await ref.get();
    if (!snap.exists) {
      await ref.set({ownerUid:uid, createdAt:Timestamp.now()});
      return k;
    }
  }
  throw new Error("Could not allocate unique referral key");
}

async function ensureReferralKeyFor(uid:string): Promise<string> {
  const userRef = db.collection("users").doc(uid);
  return await db.runTransaction(async (tx) => {
    const snap = await tx.get(userRef);
    if (!snap.exists) throw new Error("user doc missing");

    const current = (snap.get("referralKey") as string) || "";
    if (current) return current;

    const key = await reserveUniqueReferralKey(uid);
    tx.set(
      userRef,
      {referralKey:key, updatedAt:FieldValue.serverTimestamp()},
      {merge:true}
    );
    return key;
  });
}

async function applyReferralCodeToUser(
  uid:string,
  codeRaw:string
): Promise<{applied:boolean; referredByUid?:string}> {
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
        referredByKey:code,
        referredByUid:ownerUid,
        referralAppliedAt:Timestamp.now(),
        updatedAt:FieldValue.serverTimestamp()
      },
      {merge:true}
    );

    tx.set(
      ownerRef,
      {referrals:FieldValue.increment(1),
       updatedAt:FieldValue.serverTimestamp()},
      {merge:true}
    );
  });

  return {applied:true, referredByUid:ownerUid};
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
        trustFactor: 100,
        rank: 0,
        maxScore: 0,
        hasElitePass: false,
        // init as expired (avoid nulls for Unity deserialization)
        elitePassExpiresAt: Timestamp.fromMillis(Date.now() - 24*60*60*1000),
        lastLogin: Timestamp.now(),
        createdAt: FieldValue.serverTimestamp(),
        updatedAt: FieldValue.serverTimestamp(),
      });
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
        updatedAt: FieldValue.serverTimestamp()
      });
    } else {
      const needInit = !snap.get("elitePassExpiresAt");
      const patch:any = {
        lastLogin: Timestamp.now(),
        updatedAt: FieldValue.serverTimestamp(),
      };
      if (needInit) {
        // backfill as expired if missing/null
        patch.elitePassExpiresAt = 
          Timestamp.fromMillis(Date.now() - 24*60*60*1000);
      }
      tx.set(userRef, patch, {merge:true});
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

  const sessionId = String(req.data?.sessionId || "").trim();
  const earnedCurrency = Number(req.data?.earnedCurrency ?? 0);
  const earnedScore = Number(req.data?.earnedScore ?? 0);

  if (!sessionId) throw new HttpsError("invalid-argument", "sessionId required.");
  if (!isFinite(earnedCurrency) || !isFinite(earnedScore)) {
    throw new HttpsError("invalid-argument", "Numeric inputs required.");
  }
  if (earnedCurrency < 0 || earnedScore < 0) {
    throw new HttpsError("invalid-argument", "Negative values are not allowed.");
  }

  const userRef = db.collection("users").doc(uid);
  const sessRef = userRef.collection("sessions").doc(sessionId);

  const result = await db.runTransaction(async (tx) => {
    // session must exist and be granted by requestSession
    const sSnap = await tx.get(sessRef);
    if (!sSnap.exists) {
      throw new HttpsError("failed-precondition", "Unknown sessionId");
    }

    const alreadyDone = !!sSnap.get("processedAt");
    if (alreadyDone) {
      // idempotent: return current totals from user
      const uSnap = await tx.get(userRef);
      const uData = uSnap.data() || {};
      return {alreadyProcessed:true, currency:Number(uData.currency ?? 0),
        maxScore:Number(uData.maxScore ?? 0)};
    }

    // update user totals
    const uSnap = await tx.get(userRef);
    if (!uSnap.exists) {
      tx.set(userRef, {mail:"", username:"", currency:0, maxScore:0, createdAt:FieldValue.serverTimestamp(), updatedAt:FieldValue.serverTimestamp()}, {merge:true});
    }

    const currentCurrency = Number(uSnap.get("currency") ?? 0) || 0;
    const currentBest = Number(uSnap.get("maxScore") ?? 0) || 0;
    const newCurrency = currentCurrency + earnedCurrency;
    const newBest = Math.max(currentBest, earnedScore);

    tx.set(userRef, {currency:newCurrency, maxScore:newBest,
      updatedAt:FieldValue.serverTimestamp()}, {merge:true});

    // mark session as processed
    tx.set(sessRef, {state:"completed", earnedCurrency, earnedScore, processedAt:Timestamp.now()}, {merge:true});

    return {alreadyProcessed:false, currency:newCurrency, maxScore:newBest};
  });

  return result;
});

// -------- Energy (lazy regen) helpers --------
async function lazyRegenInTx(
  tx: FirebaseFirestore.Transaction,
  userRef: FirebaseFirestore.DocumentReference,
  now: Timestamp
): Promise<{cur:number; max:number; period:number; nextAt:Timestamp|null}> {
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
      tx.set(userRef, {energyCurrent:newCur, energyUpdatedAt:newUpdated, 
        updatedAt:FieldValue.serverTimestamp()}, {merge:true});
      cur = newCur;
      updatedAt = newUpdated;
    }
  }

  const nextAt = cur < max
    ? Timestamp.fromMillis(updatedAt.toMillis() + (period * 1000))
    : null;

  return {cur, max, period, nextAt};
}

// -------- getEnergySnapshot (callable, preferred by clients) --------
export const getEnergySnapshot = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();

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

  const res = await db.runTransaction(async (tx) => {
    // idempotency (optional but cheap)
    if (sessionId) {
      const sRef = userRef.collection("energySpends").doc(sessionId);
      const sSnap = await tx.get(sRef);
      if (sSnap.exists) {
        const st0 = await lazyRegenInTx(tx, userRef, now);
        return {alreadyProcessed:true, cur:st0.cur, max:st0.max,
          period:st0.period, nextAt:st0.nextAt};
      }
    }

    const st = await lazyRegenInTx(tx, userRef, now);
    if (st.cur <= 0) {
      throw new HttpsError("failed-precondition", "Not enough energy");
    }

    const newCur = st.cur - 1;
    // spending resets the timer to now for a clean 4h window
    tx.set(userRef, {energyCurrent:newCur, energyUpdatedAt:now,
      updatedAt:FieldValue.serverTimestamp()}, {merge:true});

    if (sessionId) {
      tx.set(userRef.collection("energySpends").doc(sessionId), {spentAt:now}, {merge:true});
    }

    const nextAt = Timestamp.fromMillis(now.toMillis() + st.period * 1000);
    return {alreadyProcessed:false, cur:newCur, max:st.max,
      period:st.period, nextAt};
  });

  return {
    ok:true,
    alreadyProcessed: !!(res as any).alreadyProcessed,
    energyCurrent: (res as any).cur,
    energyMax: (res as any).max,
    regenPeriodSec: (res as any).period,
    nextEnergyAt: (res as any).nextAt ? (res as any).nextAt.toDate()
      .toISOString() : null,
  };
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
    return {ok: true, sorted: true, items};
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

    return {ok: true, items};
    
  } catch (e) {
    console.error("getGalleryItems error:", e);
    throw new HttpsError("internal", "Failed to fetch gallery items.");
  }
});

export const indexMapMeta = onDocumentWritten(
  {document:"appdata/maps/{mapId}/raw", database:DB_ID},
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
            {merge:true}
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
          ...(prev.exists ? {} : {createdAt: now}),
        },
        {merge:true}
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
            {merge:true}
          );
        } else if (prevDiff && d === prevDiff) {
          tx.set(
            pRef,
            {
              ids: FieldValue.arrayRemove(mapId),
              updatedAt: FieldValue.serverTimestamp(),
            },
            {merge:true}
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

  const clamp = (n:number, a:number, b:number) => Math.max(a, Math.min(b, n));
  const count = clamp(Number(req.data?.count ?? 24), 1, 50);
  const seedIn = String(req.data?.seed ?? "");

  // tiny deterministic RNG (xorshift32-like) from a seed string
  const makeRng = (s:string) => {
    let h = 2166136261 >>> 0;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619) >>> 0;
    }
    return () => {
      h ^= h << 13; h >>>= 0;
      h ^= h >>> 17; h >>>= 0;
      h ^= h << 5;  h >>>= 0;
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

  const cycle:number[] = [
    ...Array(easy).fill(1),
    ...Array(medium).fill(2),
    ...Array(hard).fill(3),
  ];
  if (cycle.length === 0) {
    throw new HttpsError("failed-precondition", "Empty pattern");
  }

  const pattern:number[] = [];
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
    [1,2,3].map(async (d) => {
      const s = await poolsCol.doc(String(d)).get();
      const ids = (s.exists ? (s.get("ids") as string[]|undefined) : [])||[];
      // copy and shallow-shuffle for variety
      const arr = ids.slice();
      for (let i = arr.length - 1; i > 0; i--) {
        const j = Math.floor((rng() || Math.random()) * (i + 1));
        const t = arr[i]; arr[i] = arr[j]; arr[j] = t;
      }
      return {d, ids, bag:arr};
    })
  );

  const poolByDiff:Record<number,{d:number;ids:string[];bag:string[]}> = {
    1:p1, 2:p2, 3:p3
  };

  // helper to take one id from pool; allows repeats if exhausted
  const takeFrom = (d:number):string => {
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
  const chosen:{mapId:string;difficultyTag:number}[] = [];
  for (const d of pattern) {
    const id = takeFrom(d);
    chosen.push({mapId:id, difficultyTag:d});
  }

  // 4) fetch raw jsons in parallel (dedup reads)
  const uniqueIds = Array.from(new Set(chosen.map(x => x.mapId)));
  const rawCol = db.collection("appdata").doc("maps");
  const snaps = await Promise.all(
    uniqueIds.map(id => rawCol.collection(id).doc("raw").get())
  );
  const byId:Record<string,string> = {};
  for (let i = 0; i < uniqueIds.length; i++) {
    const id = uniqueIds[i];
    const s = snaps[i];
    if (!s.exists) {
      throw new HttpsError("not-found", `raw missing: ${id}`);
    }
    const d = s.data() ?? {};
    let jsonStr = "";
    if (typeof(d as any).json === "string") {
      jsonStr = (d as any).json as string;
    } else {
      const ks = Object.keys(d);
      if (ks.length === 1 && typeof(d as any)[ks[0]] === "string") {
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
    return {mapId:x.mapId, difficultyTag:x.difficultyTag, json:js};
  });

  console.log(
    `[getSequencedMaps] uid=${uid} count=${count} pat=${pattern.join("")}`
  );

  return {
    ok:true,
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

  const out = await db.runTransaction(async (tx) => {
    // 1) lazy regen inside tx
    const st = await lazyRegenInTx(tx, userRef, now);
    if (st.cur <= 0) {
      throw new HttpsError("failed-precondition", "Not enough energy");
    }

    // 2) spend 1 energy and reset timer window to now
    const newCur = st.cur - 1;
    tx.set(userRef, {energyCurrent:newCur, energyUpdatedAt:now,
      updatedAt:FieldValue.serverTimestamp()}, {merge:true});

    // 3) create server-owned session doc
    const sessionId = `${now.toMillis()}_${Math.random().toString(36).slice(2,10)}`;
    const sessRef = userRef.collection("sessions").doc(sessionId);
    tx.set(sessRef, {state:"granted", startedAt:now}, {merge:true});

    const nextAt = Timestamp.fromMillis(now.toMillis() + st.period * 1000);
    return {sessionId, energyCurrent:newCur, energyMax:st.max,
      regenPeriodSec:st.period, nextEnergyAt:nextAt};
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
    const itemsCol = db.collection("appdata").doc("items");
    const itemsSnap = await itemsCol.listCollections();

    const out: Record<string, any> = {};

    for (const subCol of itemsSnap) {
      const docSnap = await subCol.doc("itemdata").get();
      if (!docSnap.exists) continue;
      out[subCol.id] = docSnap.data();
    }

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
      await ref.set(docData, {merge: false});
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
  const inventory: Record<string, any> = {};
  invSnap.forEach((d) => (inventory[d.id] = d.data()));
  const equippedItemIds: string[] = loadSnap.exists ? loadSnap.get("equippedItemIds") || [] : [];
  return {ok: true, inventory, equippedItemIds};
});

// ---------------- purchaseItem ----------------
export const purchaseItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  // method: "GET" | "IAP" | "AD"
  const {itemId, method} = (req.data || {}) as {
    itemId?: string;
    method?: string;
    // IAP extras (optional):
    platform?: string; // "ios" | "android" | etc.
    receipt?: string;  // base64 / json
    orderId?: string;  // provider order id (anti-replay)
    // AD extras:
    adToken?: string;  // unique grant token (anti-replay)
  };

  if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");
  const m = String(method || "").toUpperCase();
  if (!["GET", "IAP", "AD"].includes(m)) {
    throw new HttpsError("invalid-argument", "Invalid method. Use GET | IAP | AD.");
  }

  const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
  const userRef = db.collection("users").doc(uid);
  const now = Timestamp.now();

  // helpers (local to this scope)
  const verifyIapReceipt = async (platform?: string, receipt?: string, orderId?: string) => {
    // TODO: Integrate with App Store / Play Billing server-side validation.
    // For now, accept non-empty payload and prevent replay by orderId locking.
    if (!platform || !receipt || !orderId) {
      throw new HttpsError("invalid-argument", "platform, receipt and orderId are required for IAP.");
    }
    const lockRef = userRef.collection("iapReceipts").doc(orderId);
    const existing = await lockRef.get();
    if (existing.exists) {
      throw new HttpsError("already-exists", "This purchase receipt was already processed.");
    }
    // Mark as consumed (will be committed in outer transaction as well if needed)
    await lockRef.set({usedAt: now, platform, previewHash: String(receipt).slice(0, 32)}, {merge: true});
    return true;
  };

  const verifyAdGrant = async (adToken?: string) => {
    if (!adToken) throw new HttpsError("invalid-argument", "adToken required for AD method.");
    const grantRef = userRef.collection("adGrants").doc(adToken);
    const g = await grantRef.get();
    if (g.exists) {
      throw new HttpsError("already-exists", "This ad grant token was already used.");
    }
    await grantRef.set({usedAt: now});
    return true;
  };

  return await db.runTransaction(async (tx) => {
    const [itemSnap, userSnap] = await Promise.all([
      tx.get(itemRef),
      tx.get(userRef),
    ]);

    if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found.");
    const item = itemSnap.data() || {};

    const isReferralOnly = Number(item.itemReferralThreshold ?? 0) > 0;
    if (isReferralOnly) {
      throw new HttpsError("failed-precondition", "Referral-only item cannot be purchased.");
    }

    const isConsumable = !!item.itemIsConsumable;
    const priceGet = Number(item.itemGetPrice ?? 0) || 0;      // in-game currency
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

    // Ownership check (non-consumables cannot be purchased twice)
    const invRef = userRef.collection("inventory").doc(itemId);
    const invSnap = await tx.get(invRef);
    const alreadyOwned = invSnap.exists && !!invSnap.get("owned");
    if (alreadyOwned && !isConsumable) {
      throw new HttpsError("failed-precondition", "Already owned.");
    }

    // Charge / verify depending on method
    let newBalance = Number(userSnap.get("currency") ?? 0) || 0;
    if (m === "GET") {
      if (newBalance < priceGet) {
        throw new HttpsError("failed-precondition", "Not enough currency.");
      }
      newBalance -= priceGet;
      tx.update(userRef, {currency: newBalance, updatedAt: FieldValue.serverTimestamp()});
    } else if (m === "IAP") {
      // Run verification outside of transaction (network calls)
      tx.set(userRef, {updatedAt: FieldValue.serverTimestamp()}, {merge: true});
    } else if (m === "AD") {
      // Mark ad token usage to prevent replay; will also exist outside txn
      tx.set(userRef, {updatedAt: FieldValue.serverTimestamp()}, {merge: true});
    }

    // Upsert inventory
    const nextQty = isConsumable ? (Number(invSnap.get("quantity") ?? 0) + 1) : 0;
    const invData: any = {
      owned: true,
      equipped: invSnap.get("equipped") === true, // preserve equip
      quantity: nextQty,
      itemIsConsumable: isConsumable,
      lastChangedAt: FieldValue.serverTimestamp(),
      acquiredAt: invSnap.exists ? invSnap.get("acquiredAt") ?? now : now,
    };
    tx.set(invRef, invData, {merge: true});

    // Audit trail
    const logRef = userRef.collection("purchases").doc();
    tx.set(logRef, {
      itemId,
      method: m,
      priceGet: m === "GET" ? priceGet : 0,
      priceUsd: m === "IAP" ? priceUsd : 0,
      isConsumable,
      at: now,
    });

    return {ok: true, itemId, owned: true, currencyLeft: newBalance};
  }).then(async (res) => {
    // Post-transaction external checks (IAP/AD anti-replay) — outside tx to avoid network in txn
    if (String(method || "").toUpperCase() === "IAP") {
      const {platform, receipt, orderId} = (req.data || {}) as any;
      await verifyIapReceipt(platform, receipt, orderId);
    } else if (String(method || "").toUpperCase() === "AD") {
      const {adToken} = (req.data || {}) as any;
      await verifyAdGrant(adToken);
    }
    return res;
  });
});

// ---------------- equipItem ----------------
export const equipItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const {itemId} = req.data || {};
  if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");
  const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
  const userRef = db.collection("users").doc(uid);
  const invRef = userRef.collection("inventory").doc(itemId);
  const loadRef = userRef.collection("loadout").doc("current");
  await db.runTransaction(async (tx) => {
    const invSnap = await tx.get(invRef);
    if (!invSnap.exists || !invSnap.get("owned")) throw new HttpsError("failed-precondition", "Item not owned.");
    const itemSnap = await tx.get(itemRef);
    if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found.");
    const isConsumable = !!itemSnap.get("itemIsConsumable");
    if (isConsumable) throw new HttpsError("failed-precondition", "Consumables cannot be equipped.");
    const loadSnap = await tx.get(loadRef);
    let equipped: string[] = loadSnap.exists ? loadSnap.get("equippedItemIds") || [] : [];
    if (!equipped.includes(itemId)) equipped.push(itemId);
    tx.set(loadRef, {equippedItemIds: equipped, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
    tx.set(invRef, {equipped: true, lastChangedAt: FieldValue.serverTimestamp()}, {merge: true});
  });
  return {ok: true, itemId};
});

// ---------------- unequipItem ----------------
export const unequipItem = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
  const {itemId} = req.data || {};
  if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");
  const userRef = db.collection("users").doc(uid);
  const invRef = userRef.collection("inventory").doc(itemId);
  const loadRef = userRef.collection("loadout").doc("current");
  await db.runTransaction(async (tx) => {
    const loadSnap = await tx.get(loadRef);
    let equipped: string[] = loadSnap.exists ? loadSnap.get("equippedItemIds") || [] : [];
    equipped = equipped.filter((x) => x !== itemId);
    tx.set(loadRef, {equippedItemIds: equipped, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
    tx.set(invRef, {equipped: false, lastChangedAt: FieldValue.serverTimestamp()}, {merge: true});
  });
  return {ok: true, itemId};
});
