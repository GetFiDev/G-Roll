import {onSchedule} from "firebase-functions/v2/scheduler";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";

export const checkElitePassExpirations = onSchedule("every 12 hours", async (event) => {
    const now = Timestamp.now();
    console.log("[Scheduler] Starting checkElitePassExpirations at", now.toDate().toISOString());

    // Find expired subscriptions
    // Requires composite index: hasElitePass (ASC/DESC) + elitePassExpiresAt (ASC)
    const usersRef = db.collection("users");
    const q = usersRef
        .where("hasElitePass", "==", true)
        .where("elitePassExpiresAt", "<", now);

    const snap = await q.get();
    console.log(`[Scheduler] Found ${snap.size} expired users.`);

    // Process in parallel (batches would be better for huge datasets, but this is fine for now)
    const updates = snap.docs.map(async (doc) => {
        const uid = doc.id;
        try {
            await db.runTransaction(async (tx) => {
                const userRef = usersRef.doc(uid);
                const uSnap = await tx.get(userRef);

                if (!uSnap.exists) return;

                const currentEnergy = Number(uSnap.get("energyCurrent") ?? 0);
                const newMax = 5;

                // Cap current energy if it exceeds new max
                let newCurrent = currentEnergy;
                if (newCurrent > newMax) {
                    newCurrent = newMax;
                }

                tx.set(userRef, {
                    hasElitePass: false,
                    energyMax: newMax,
                    energyCurrent: newCurrent,
                    updatedAt: FieldValue.serverTimestamp()
                }, {merge: true});

                // Revoke Elite Pass Items
                const invRef = userRef.collection("inventory");
                const itemsQ = invRef.where("source", "==", "elite_pass");

                // Transactional read of items to be removed
                const itemsTxSnap = await tx.get(itemsQ);

                itemsTxSnap.forEach((itemDoc) => {
                    tx.delete(itemDoc.ref);
                });
            });
            console.log(`[Scheduler] Expired Elite Pass for ${uid}`);
        } catch (e) {
            console.error(`[Scheduler] Failed to expire ${uid}:`, e);
        }
    });

    await Promise.all(updates);
    console.log("[Scheduler] Finished checkElitePassExpirations.");
});
