import * as admin from "firebase-admin";
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
