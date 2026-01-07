import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {ACH_TYPES} from "../utils/constants";
import {upsertUserAch} from "./achievements.functions";
import {cleanupExpiredConsumablesInTx} from "./shop.functions";
import {lazyRegenInTx} from "./energy.functions";

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
        }, {merge: true});

        // 3) create server-owned session doc
        const sessionId = `${now.toMillis()}_${Math.random().toString(36).slice(2, 10)}`;
        const sessRef = userRef.collection("sessions").doc(sessionId);
        tx.set(sessRef, {state: "granted", startedAt: now}, {merge: true});

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

// -------- session end: submit results (idempotent) --------
import {getActiveSeasonId} from "../utils/helpers";

export const submitSessionResult = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const p = (req.data as Record<string, any>) || {};
    const sessionId = (p.sessionId || "").toString().trim();
    const earnedCurrency = Number(p.earnedCurrency) || 0;
    const earnedScore = Number(p.earnedScore) || 0;

    if (!sessionId) throw new HttpsError("invalid-argument", "sessionId required");

    // Session doc ref
    const userRef = db.collection("users").doc(uid);
    const sessRef = userRef.collection("sessions").doc(sessionId);

    // Fetch active season OUTSIDE transaction (safe)
    const activeSeasonId = await getActiveSeasonId();

    // Transaction for safe write
    const res = await db.runTransaction(async (tx) => {
        // 1) Session state check
        const sSnap = await tx.get(sessRef);
        if (!sSnap.exists) {
            // Session yoksa (belki cok eski veya creation fail oldu)
            return {alreadyProcessed: false, valid: false};
        }
        const sData = sSnap.data() || {};
        if (sData.state === "completed" || sData.processedAt) {
            // Already processed
            return {alreadyProcessed: true, valid: true};
        }

        // 2) User data read (MUST BE FIRST for referral logic)
        const uSnap = await tx.get(userRef);

        // 3) Referral Data Read (Must occur before writes)
        const referrerUid = uSnap.get("referredByUid") as string | null;
        let refUserSnap: FirebaseFirestore.DocumentSnapshot | null = null;
        let pendingRef: FirebaseFirestore.DocumentReference | null = null;
        let pendingSnap: FirebaseFirestore.DocumentSnapshot | null = null;

        if (referrerUid && earnedCurrency > 0) {
            const refUserRef = db.collection("users").doc(referrerUid);
            refUserSnap = await tx.get(refUserRef); // READ

            if (refUserSnap.exists) {
                // Determine if we need to read pending doc
                const eliteExp = refUserSnap.get("elitePassExpiresAt") as Timestamp | null;
                const nowMs = Date.now();
                const hasElite = eliteExp ? (eliteExp.toMillis() > nowMs) : false;
                const rate = hasElite ? 0.10 : 0.05;
                const bonus = Number((earnedCurrency * rate).toFixed(3));

                if (bonus > 0) {
                    // Path: users/{referrer}/referralData/pendingReferralEarnings/records/{childUid}
                    pendingRef = refUserRef
                        .collection("referralData")
                        .doc("pendingReferralEarnings")
                        .collection("records")
                        .doc(uid);

                    pendingSnap = await tx.get(pendingRef); // READ
                }
            }
        }

        // --- ALL READS DONE ---

        const prevMaxCombo = Number(uSnap.get("maxCombo") ?? 0) || 0;
        const prevSessions = Number(uSnap.get("sessionsPlayed") ?? 0) || 0;
        const prevCumEarn = Number(uSnap.get("cumulativeCurrencyEarned") ?? 0) || 0;
        const prevPlaySec = Number(uSnap.get("totalPlaytimeSec") ?? 0) || 0;
        const prevPups = Number(uSnap.get("powerUpsCollected") ?? 0) || 0;
        const prevCurrency = Number(uSnap.get("currency") ?? 0) || 0;
        const prevBest = Number(uSnap.get("maxScore") ?? 0) || 0;

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

        // --- Seasonal Score Logic ---
        const safeSeasonal = (uSnap.get("seasonalMaxScores") || {}) as Record<string, number>;
        const seasonalMaxScores = {...safeSeasonal};

        if (activeSeasonId) {
            const prevSeasonBest = Number(seasonalMaxScores[activeSeasonId] || 0);
            const newSeasonBest = Math.max(prevSeasonBest, earnedScore);
            seasonalMaxScores[activeSeasonId] = newSeasonBest;
        }

        tx.set(userRef, {
            currency: newCurrency,
            maxScore: newBest,
            seasonalMaxScores,
            sessionsPlayed: newSessions,
            cumulativeCurrencyEarned: newCumEarn,
            totalPlaytimeSec: newPlaySec,
            totalPlaytimeMinutes: newPlayMin,
            totalPlaytimeMinutesFloat: newPlayMinFloat,
            powerUpsCollected: newPups,
            maxCombo: newMaxCombo,
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        // mark session as processed
        tx.set(sessRef, {state: "completed", earnedCurrency, earnedScore, processedAt: Timestamp.now()}, {merge: true});

        // --- Referral Writes ---
        if (refUserSnap && refUserSnap.exists && pendingRef) {
            const eliteExp = refUserSnap.get("elitePassExpiresAt") as Timestamp | null;
            const nowMs = Date.now();
            const hasElite = eliteExp ? (eliteExp.toMillis() > nowMs) : false;
            const rate = hasElite ? 0.10 : 0.05;
            const bonus = Number((earnedCurrency * rate).toFixed(3));

            if (bonus > 0) {
                const oldAmount = (pendingSnap && pendingSnap.exists) ? (Number(pendingSnap.get("amount")) || 0) : 0;
                const childName = (uSnap.get("username") as string) || "Guest";

                tx.set(pendingRef, {
                    amount: oldAmount + bonus,
                    childUid: uid,
                    childName: childName,
                    updatedAt: FieldValue.serverTimestamp()
                }, {merge: true});
            }
        }

        return {alreadyProcessed: false, currency: newCurrency, maxScore: newBest};
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
    return res;
});
