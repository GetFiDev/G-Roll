import {onCall, HttpsError} from "firebase-functions/v2/https";
import {onSchedule} from "firebase-functions/v2/scheduler";
import {db} from "../firebase";
import {FieldValue} from "@google-cloud/firestore";

// Helper: Find Ad Config by ID
// Structure: /appdata/adDatas/{anyCollectionName}/data
// We iterate all subcollections of appdata/adDatas and check the 'data' document in each.
async function findAdConfig(adId: string): Promise<{ data: any, ref: FirebaseFirestore.DocumentReference } | null> {
    const adDatasRef = db.collection("appdata").doc("adDatas");
    const collections = await adDatasRef.listCollections();

    for (const col of collections) {
        const docRef = col.doc("data");
        const snap = await docRef.get();
        if (snap.exists) {
            const data = snap.data();
            if (data && data.id === adId) {
                return {data, ref: docRef};
            }
        }
    }
    return null;
}

// Get Ad Details: Daily Limit & User's Usage Today
export const getAdProductDetails = onCall(async (request) => {
    if (!request.auth) {
        throw new HttpsError("unauthenticated", "User must be logged in.");
    }

    const uid = request.auth.uid;
    const adId = request.data.adId;

    if (!adId) {
        throw new HttpsError("invalid-argument", "Missing adId.");
    }

    try {
        // 1. Find Config
        const config = await findAdConfig(adId);
        if (!config) {
            throw new HttpsError("not-found", `Ad Product '${adId}' not found.`);
        }

        const dailyLimit = Number(config.data.daily_limit || 0);

        // 2. Get User Usage
        // Path: users/{uid}/adusagedata/{ad_id}
        const usageRef = db.collection("users").doc(uid).collection("adusagedata").doc(adId);
        const usageSnap = await usageRef.get();
        const usedToday = usageSnap.exists ? Number(usageSnap.get("used_today") || 0) : 0;

        return {
            adId,
            dailyLimit,
            usedToday
        };

    } catch (e) {
        console.error("getAdProductDetails error:", e);
        if (e instanceof HttpsError) throw e;
        throw new HttpsError("internal", "Failed to get ad details.");
    }
});

// Increment Ad Usage
export const incrementAdUsage = onCall(async (request) => {
    if (!request.auth) {
        throw new HttpsError("unauthenticated", "User must be logged in.");
    }

    const uid = request.auth.uid;
    const adId = request.data.adId;

    if (!adId) {
        throw new HttpsError("invalid-argument", "Missing adId.");
    }

    try {
        const usageRef = db.collection("users").doc(uid).collection("adusagedata").doc(adId);

        await db.runTransaction(async (tx) => {
            // 1. Re-fetch config to be safe (or rely on client logic? Better safe: check limit)
            const config = await findAdConfig(adId);
            if (!config) {
                throw new HttpsError("not-found", "Ad config not found.");
            }
            const dailyLimit = Number(config.data.daily_limit || 0);

            // 2. Check current usage
            const usageSnap = await tx.get(usageRef);
            const currentUsage = usageSnap.exists ? Number(usageSnap.get("used_today") || 0) : 0;

            if (currentUsage >= dailyLimit) {
                throw new HttpsError("failed-precondition", "Daily limit reached.");
            }

            // 3. Increment
            tx.set(usageRef, {
                used_today: currentUsage + 1,
                updatedAt: FieldValue.serverTimestamp()
            }, {merge: true});
        });

        return {success: true};

    } catch (e) {
        console.error("incrementAdUsage error:", e);
        if (e instanceof HttpsError) throw e;
        throw new HttpsError("internal", "Failed to increment usage.");
    }
});

// Scheduled Function: Reset Daily Ad Usage
// Runs every day at midnight (UTC assumed, or specific timezone if needed. Usually UTC is default)
// Clears all "adusagedata" documents.
export const resetDailyAdUsage = onSchedule("every day 00:00", async (event) => {
    console.log("Starting resetDailyAdUsage...");
    // const now = Timestamp.now(); // data not needed for bulk delete

    // Use Collection Group Query to find ALL 'adusagedata' collections
    const query = db.collectionGroup("adusagedata");

    // We want to delete all documents in these collections.
    // NOTE: This can be a lot of documents. For scale, use batched writes or recursive delete tools.
    // For this implementation, we'll traverse and delete in batches.

    // Using firestore-admin recursive delete is better if available, but here we manually iterate.
    // 500 is batch limit.

    const snapshot = await query.get();
    if (snapshot.empty) {
        console.log("No ad usage data to reset.");
        return;
    }

    const batchSize = 500;
    let batch = db.batch();
    let count = 0;
    let totalDeleted = 0;

    for (const doc of snapshot.docs) {
        batch.delete(doc.ref);
        count++;

        if (count >= batchSize) {
            await batch.commit();
            totalDeleted += count;
            console.log(`Deleted ${totalDeleted} ad usage records...`);
            batch = db.batch();
            count = 0;
        }
    }

    if (count > 0) {
        await batch.commit();
        totalDeleted += count;
    }

    console.log(`Finished resetDailyAdUsage. Total deleted: ${totalDeleted}`);
});

// Grant Reward (Generic)
// Designed to be called by AdRewardGranter.cs
export const grantReward = onCall(async (request) => {
    if (!request.auth) {
        throw new HttpsError("unauthenticated", "User must be logged in.");
    }

    const uid = request.auth.uid;
    const {currency, premium, energy} = request.data;

    // Simple validation
    const c = Number(currency || 0);
    const p = Number(premium || 0);
    const e = Number(energy || 0);

    // Prevent negative?
    if (c < 0 || p < 0 || e < 0) {
        throw new HttpsError("invalid-argument", "Negative rewards not allowed.");
    }

    if (c === 0 && p === 0 && e === 0) {
        return {success: true, message: "Nothing to grant."};
    }

    try {
        const userRef = db.collection("users").doc(uid);

        const updateData: any = {
            updatedAt: FieldValue.serverTimestamp()
        };

        if (c > 0) updateData.currency = FieldValue.increment(c);
        if (p > 0) updateData.premiumCurrency = FieldValue.increment(p);
        if (e > 0) updateData.energyCurrent = FieldValue.increment(e); // Assuming 'energyCurrent' is the field

        await userRef.set(updateData, {merge: true});

        return {success: true};
    } catch (err) {
        console.error("grantReward error:", err);
        throw new HttpsError("internal", "Failed to grant reward.");
    }
});
