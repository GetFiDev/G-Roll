import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {snapNum, utcDateString} from "../utils/helpers";
import {upsertUserAch} from "./achievements.functions";
import {ACH_TYPES} from "../utils/constants";

const streakConfigRef = db.collection("appdata").doc("streakdata");
const userStreakRef = (uid: string) => db.collection("users").doc(uid).collection("meta").doc("streak");

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

    const {totalDays, unclaimedDays, rewardPerDay, todayCounted} = await db.runTransaction(async (tx) => {
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
            if (!sSnap.exists) {
                // FIX #5: First time ever: Start streak at 1, NO reward (unclaimed=0)
                // This shows "Day 1" to user but no reward claim available
                totalDays = 1;
                unclaimedDays = 0;
            } else {
                // Ongoing streak or broken streak logic could go here if we tracked breaks
                // For now, simple increment logic from existing code:
                totalDays = prevTotal + 1;
                unclaimedDays = prevUnclaimed + 1;
            }
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
            {merge: true}
        );

        tx.set(userRef, {updatedAt: FieldValue.serverTimestamp()}, {merge: true});

        return {totalDays, unclaimedDays, rewardPerDay, todayCounted};
    });

    // Update HABIT_MAKER achievement if a new day was counted
    if (todayCounted && totalDays > 0) {
        try {
            await upsertUserAch(uid, ACH_TYPES.HABIT_MAKER, totalDays);
        } catch (e) {
            console.warn("[streak] Failed to update HABIT_MAKER achievement:", e);
        }
    }

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

// recordLogin: DEPRECATED. Kept as a thin wrapper to the new updateDailyStreak core.
export const recordLogin = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const res = await applyDailyStreakIncrement(uid);
    return {ok: true, ...res, deprecated: true};
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
            return {granted: 0, rewardPerDay: Math.max(0, rewardPerDay), unclaimedDays: 0, newCurrency: curCurrency};
        }

        const rewardPerDay = cfgSnap.exists ? Number(cfgSnap.get("reward") ?? 0) || 0 : 0;
        const grant = Math.max(0, rewardPerDay) * unclaimed;

        const curCurrency = Number(uSnap.get("currency") ?? 0) || 0;
        const newCurrency = curCurrency + grant;

        tx.set(uRef, {currency: newCurrency, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        tx.set(sRef, {unclaimedDays: 0, lastClaimAt: now, updatedAt: FieldValue.serverTimestamp()}, {merge: true});

        // optional: audit log
        const logRef = uRef.collection("streakClaims").doc();
        tx.set(logRef, {
            at: now,
            daysClaimed: unclaimed,
            rewardPerDay,
            granted: grant,
            currencyAfter: newCurrency,
        });

        return {granted: grant, rewardPerDay, unclaimedDays: 0, newCurrency};
    });

    return {ok: true, ...res};
});

// getStreakStatus: For UI — shows whether a claim is available and the next UTC midnight countdown.
export const getStreakStatus = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const res = await applyDailyStreakIncrement(uid);
    return {ok: true, ...res};
});

export const updateDailyStreak = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const res = await applyDailyStreakIncrement(uid);
    return {ok: true, ...res};
});
