import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";

// ========================= Autopilot (AFK) =========================

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
        tx.set(autoRef, auto, {merge: true});
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
            tx.set(autoRef, auto, {merge: true});
        }
    }

    return {userRef, autoRef, userData: {...userData, currency: curCurrency}, auto, config, isElite};
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

    return {ok: true, serverNowMillis: now.toMillis(), ...out};
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
            }, {merge: true});
        } else {
            // Turning OFF: close the window by simply disabling and clearing activation
            tx.set(autoRef, {
                isAutopilotOn: false,
                autopilotActivationDate: null,
                updatedAt: FieldValue.serverTimestamp(),
            }, {merge: true});
        }

        // Touch user updatedAt for visibility
        tx.set(userRef, {updatedAt: FieldValue.serverTimestamp()}, {merge: true});
    });

    return {ok: true, isAutopilotOn: on};
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
                {currency: curCurrency + wallet, updatedAt: FieldValue.serverTimestamp()},
                {merge: true}
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

            tx.set(autoRef, baseUpdate, {merge: true});
        } else {
            const baseUpdate: any = {
                autopilotLastClaimedAt: now.toMillis(),
                updatedAt: FieldValue.serverTimestamp(),
            };

            if (!isElite) {
                baseUpdate.isAutopilotOn = false;
                baseUpdate.autopilotActivationDate = null;
            }

            tx.set(autoRef, baseUpdate, {merge: true});
        }

        return {claimed: wallet, currencyAfter: curCurrency + wallet};
    });

    return {ok: true, claimed: res.claimed, currencyAfter: res.currencyAfter};
});
