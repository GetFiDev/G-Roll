import * as admin from "firebase-admin";
import {onDocumentWritten} from "firebase-functions/v2/firestore";
import {setGlobalOptions} from "firebase-functions/v2/options";
import {Firestore, FieldValue} from "@google-cloud/firestore";

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
