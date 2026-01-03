import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {cleanupExpiredConsumablesInTx} from "./shop.functions";

// -------- Energy (lazy regen) helpers (Exported for Session) --------
export async function lazyRegenInTx(
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
            }, {merge: true});
            cur = newCur;
            updatedAt = newUpdated;
        }
    }

    const nextAt = cur < max
        ? Timestamp.fromMillis(updatedAt.toMillis() + (period * 1000))
        : null;

    return {cur, max, period, nextAt};
}

// -------- getEnergySnapshot --------
export const getEnergySnapshot = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const userRef = db.collection("users").doc(uid);
    const now = Timestamp.now();
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

// -------- getEnergyStatus --------
export const getEnergyStatus = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const userRef = db.collection("users").doc(uid);
    const now = Timestamp.now();
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

// -------- spendEnergy --------
export const spendEnergy = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const sessionId = String(req.data?.sessionId || "").trim();
    const userRef = db.collection("users").doc(uid);
    const now = Timestamp.now();
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });

    const res = await db.runTransaction(async (tx) => {
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
        if (st.cur <= 0) throw new HttpsError("failed-precondition", "Not enough energy");

        const newCur = st.cur - 1;
        tx.set(userRef, {
            energyCurrent: newCur, energyUpdatedAt: now,
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        if (sessionId) {
            tx.set(userRef.collection("energySpends").doc(sessionId), {spentAt: now}, {merge: true});
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
        nextEnergyAt: (res as any).nextAt ? (res as any).nextAt.toDate().toISOString() : null,
    };
});

// -------- grantBonusEnergy --------
export const grantBonusEnergy = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const now = Timestamp.now();

    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });

    const res = await db.runTransaction(async (tx) => {
        const st = await lazyRegenInTx(tx, userRef, now);
        if (st.cur >= st.max) {
            return {granted: 0, cur: st.cur, max: st.max, period: st.period, nextAt: st.nextAt};
        }
        const newCur = Math.min(st.max, st.cur + 1);
        const granted = newCur - st.cur;
        tx.set(userRef, {energyCurrent: newCur, updatedAt: FieldValue.serverTimestamp()}, {merge: true});

        const nextAt = newCur < st.max
            ? st.nextAt || Timestamp.fromMillis(now.toMillis() + st.period * 1000)
            : null;
        return {granted, cur: newCur, max: st.max, period: st.period, nextAt};
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
