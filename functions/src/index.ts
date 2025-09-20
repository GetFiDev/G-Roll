import * as admin from "firebase-admin";
import * as functionsV1 from "firebase-functions/v1";
import {onDocumentWritten} from "firebase-functions/v2/firestore";
import {setGlobalOptions} from "firebase-functions/v2/options";
import {Firestore, Timestamp, FieldValue} from "@google-cloud/firestore";
import {onCall, HttpsError} from "firebase-functions/v2/https";

admin.initializeApp();

// Firestore lokasyonun nam5 → fonksiyon bölgesi us-central1
setGlobalOptions({region: "us-central1"});

const DB_ID = "getfi";
const SEASON = "current";
const TOP_N = 50;

// Çoklu DB client
const db = new Firestore({databaseId: DB_ID});

export const syncLeaderboard = onDocumentWritten(
  {document: "users/{uid}", database: DB_ID},
  async (event) => {
    const uid = event.params.uid;
    const after = event.data?.after?.data();
    if (!after) return;

    const username =
      (typeof after.username === "string" ? after.username : "")
        .trim() || "Guest";

    const rawScore = after.score;
    const score = typeof rawScore === "number" ? rawScore : 0;

    const seasonRef = db.collection("leaderboards").doc(SEASON);

    // 1) Kullanıcının public entry'sini güncelle
    await seasonRef
      .collection("entries")
      .doc(uid)
      .set(
        {username, score, updatedAt: FieldValue.serverTimestamp()},
        {merge: true},
      );

    // 2) TopN’i tek dokümana materialize et
    const topRef = seasonRef.collection("meta").doc(`top${TOP_N}`);

    await db.runTransaction(async (tx) => {
      const snap = await tx.get(topRef);

      type Entry = {uid: string; username: string; score: number};
      let entries: Entry[] = [];

      const rawEntries = snap.exists ? snap.data()?.entries : null;
      if (Array.isArray(rawEntries)) {
        entries = rawEntries
          .map((e) => ({
            uid: String(e?.uid ?? ""),
            username: String(e?.username ?? "Guest"),
            score: Number(e?.score ?? 0),
          }))
          .filter((e) => e.uid.length > 0);
      }

      const i = entries.findIndex((e) => e.uid === uid);
      const updated: Entry = {uid, username, score};
      if (i >= 0) entries[i] = updated;
      else entries.push(updated);

      entries.sort((a, b) => b.score - a.score);
      if (entries.length > TOP_N) entries = entries.slice(0, TOP_N);

      tx.set(
        topRef,
        {entries, updatedAt: FieldValue.serverTimestamp()},
        {merge: true},
      );
    });
  },
);
/**
 * Elite Pass satın alma:
 * - Sadece auth'lu kullanıcı
 * - Süre: aktifse kalan + 30 gün, değilse şimdi + 30 gün
 * - İsteğe bağlı: purchaseId ile idempotent (çifte yazmayı önler)
 * Döner: { active: boolean, expiresAt: ISO string | null }
 */
export const purchaseElitePass = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const purchaseId = (req.data?.purchaseId ?? "").toString().trim();
  const now = Timestamp.now();
  const userRef = db.collection("users").doc(uid);

  // Idempotency: aynı purchaseId geldiyse tekrarlama
  if (purchaseId) {
    const pRef = userRef.collection("elitePassPurchases").doc(purchaseId);
    const pSnap = await pRef.get();
    if (pSnap.exists) {
      const uSnap = await userRef.get();
      const exp = uSnap.exists ?
        (uSnap.get("elitePassExpiresAt") as Timestamp | null) :
        null;
      const active = !!exp && exp.toMillis() > now.toMillis();
      return {active, expiresAt: exp?.toDate().toISOString() ?? null};
    }
  }

  await db.runTransaction(async (tx) => {
    const uSnap = await tx.get(userRef);
    const existing: Timestamp | null = uSnap.exists ?
      (uSnap.get("elitePassExpiresAt") as Timestamp | null) :
      null;

    const baseMillis =
      existing && existing.toMillis() > now.toMillis() ?
        existing.toMillis() :
        now.toMillis();

    const thirtyDaysMs = 30 * 24 * 60 * 60 * 1000;
    const newExpiry = Timestamp.fromMillis(baseMillis + thirtyDaysMs);

    tx.set(
      userRef,
      {
        hasElitePass: true,
        elitePassExpiresAt: newExpiry,
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );

    if (purchaseId) {
      tx.set(
        userRef.collection("elitePassPurchases").doc(purchaseId),
        {processedAt: now, newExpiry},
        {merge: true},
      );
    }
  });

  const finalSnap = await userRef.get();
  const exp = finalSnap.get("elitePassExpiresAt") as Timestamp | null;
  const active = !!exp && exp.toMillis() > now.toMillis();
  return {active, expiresAt: exp?.toDate().toISOString() ?? null};
});

/**
 * Elite Pass kontrol:
 * - Sadece auth'lu kullanıcı
 * - Sunucuda "şimdi" ile expiry karşılaştırır
 * Döner: { active: boolean, expiresAt: ISO string | null }
 */
export const checkElitePass = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const now = Timestamp.now();
  const snap = await db.collection("users").doc(uid).get();
  if (!snap.exists) return {active: false, expiresAt: null};

  const exp = snap.get("elitePassExpiresAt") as Timestamp | null;
  const active = !!exp && exp.toMillis() > now.toMillis();
  return {active, expiresAt: exp?.toDate().toISOString() ?? null};
});

// ---------- helpers ----------
const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // benzer karakterleri çıkardık
function randomReferralKey(len = 12): string {
  let s = "";
  for (let i = 0; i < len; i++) {
    s += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
  }
  return s;
}

// Benzersiz KEY ayırt; referralKeys/{KEY} = {ownerUid}
async function reserveUniqueReferralKey(uid: string): Promise<string> {
  for (let i = 0; i < 6; i++) { // 6 deneme yeterli
    const k = randomReferralKey(12);
    const ref = db.collection("referralKeys").doc(k);
    const snap = await ref.get();
    if (!snap.exists) {
      await ref.set({ownerUid: uid, createdAt: Timestamp.now()});
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
      {
        referralKey: key,
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );
    return key;
  });
}

async function applyReferralCodeToUser(
  uid: string,
  codeRaw: string,
): Promise<{applied: boolean; referredByUid?: string}> {
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
    if (!u.exists) throw new HttpsError("failed-precondition", "User doc missing");

    // daha önce set edilmiş mi?
    const already = (u.get("referredByKey") as string) || "";
    if (already) return; // idempotent: ikinci kez yazma

    tx.set(
      userRef,
      {
        referredByKey: code,
        referredByUid: ownerUid,
        referralAppliedAt: Timestamp.now(),
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );

    // referansı verenin sayacını +1 (field yoksa 0'dan başlar)
    tx.set(
      ownerRef,
      {referrals: FieldValue.increment(1), updatedAt: FieldValue.serverTimestamp()},
      {merge: true},
    );
  });

  return {applied: true, referredByUid: ownerUid};
}

export const createUserProfile = functionsV1
  .region("us-central1")
  .auth.user()
  .onCreate(async (user) => {
    const { uid, email } = user;
    const userRef = db.collection("users").doc(uid);

    await db.runTransaction(async (tx) => {
      const snap = await tx.get(userRef);
      if (snap.exists) return; // idempotent

      tx.set(userRef, {
        mail: email ?? "",
        username: "",
        currency: 0,
        score: 0,
        hasElitePass: false,           // server-only
        elitePassExpiresAt: null,      // ⬅️ alanı baştan oluştur (satın alınca set edilecek)
        lastLogin: Timestamp.now(),
        createdAt: FieldValue.serverTimestamp(),
        updatedAt: FieldValue.serverTimestamp(),
      });
    });

    // profil oluşturulduktan sonra referralKey ver
    await ensureReferralKeyFor(uid);
  });

// ---------- 2) login sonrası: profili garantiye al + opsiyonel referral code uygula ----------
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
        score: 0,
        hasElitePass: false,
        lastLogin: Timestamp.now(),
        createdAt: FieldValue.serverTimestamp(),
        updatedAt: FieldValue.serverTimestamp(),
      });
    } else {
      tx.set(
        userRef,
        {lastLogin: Timestamp.now(), updatedAt: FieldValue.serverTimestamp()},
        {merge: true},
      );
    }
  });

  // referralKey garanti
  const key = await ensureReferralKeyFor(uid);

  // İsteğe bağlı referralCode varsa uygula (ilk login’de UI’dan gelebilir)
  const code = (req.data?.referralCode ?? "").toString();
  if (code) {
    await applyReferralCodeToUser(uid, code);
  }

  return {ok: true, referralKey: key};
});

// ---------- 3) (opsiyonel) sonradan kod girme ----------
export const applyReferralCode = onCall(async (req) => {
  const uid = req.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

  const code = (req.data?.referralCode ?? "").toString();
  const result = await applyReferralCodeToUser(uid, code);
  return result;
});
