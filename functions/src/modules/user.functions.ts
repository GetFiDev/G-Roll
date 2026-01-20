import * as functionsV1 from "firebase-functions/v1";
import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {randomReferralKey} from "../utils/helpers"; // Removed normId
import {ALPHABET, ACH_TYPES} from "../utils/constants"; // Removed ACH_TYPES -> re-added
import {upsertUserAch} from "./achievements.functions";

// -------- Helper Functions --------
// Helper: Pure Read Check for available referral key
async function findAvailableReferralKey(tx: FirebaseFirestore.Transaction): Promise<string> {
    for (let i = 0; i < 5; i++) {
        const k = randomReferralKey(12, ALPHABET);
        const refKey = db.collection("referralKeys").doc(k);
        const s = await tx.get(refKey);
        if (!s.exists) {
            return k;
        }
    }
    return "FAIL";
}

// Helper: Get next user rank (simple counter)
async function getNextUserRank(): Promise<number> {
    const statsRef = db.collection("appdata").doc("stats");
    try {
        return await db.runTransaction(async (tx) => {
            const snap = await tx.get(statsRef);
            let current = 0;
            if (snap.exists) {
                current = Number(snap.get("userCount") ?? 0) || 0;
            }
            const next = current + 1;
            tx.set(statsRef, {userCount: next}, {merge: true});
            return next;
        });
    } catch (e) {
        console.warn("getNextUserRank failed, defaulting to 0:", e);
        return 0; // Fallback
    }
}

async function grantReferralThresholdItems(
    tx: FirebaseFirestore.Transaction,
    referrerUid: string,
    newCount: number
) {
    // Find valid thresholds <= newCount
    // OPTIMIZATION: Query collectionGroup "itemdata"
    try {
        const q = db.collectionGroup("itemdata")
            .where("itemReferralThreshold", ">", 0)
            .where("itemReferralThreshold", "<=", newCount);

        // Queries cannot be run on the transaction object directly (tx.get(query) is invalid).
        // We perform a non-transactional read for the config data.
        const snap = await q.get();
        if (snap.empty) return;

        for (const d of snap.docs) {
            const data = d.data();
            const thresh = Number(data.itemReferralThreshold);
            // Logic: Each item is granted exactly once when threshold met?
            // Or is it "if (newCount == thresh)"? usually ONCE.
            if (newCount === thresh) {
                const itemId = d.ref.parent.parent?.id;
                if (!itemId) continue;

                const invRef = db.collection("users").doc(referrerUid).collection("inventory").doc(itemId);
                tx.set(invRef, {
                    owned: true,
                    acquiredAt: FieldValue.serverTimestamp(),
                    source: "referral_bonus"
                }, {merge: true});
            }
        }
    } catch (e) {
        console.warn("grantReferralThresholdItems failed:", e);
    }
}

export async function applyReferralCodeToUser(uid: string, code: string) {
    if (!code || code.length < 5) throw new HttpsError("invalid-argument", "Invalid code");
    const upper = code.toUpperCase().trim();

    // Validate format
    const validChars = new Set(ALPHABET.split(""));
    for (const c of upper) {
        if (!validChars.has(c)) throw new HttpsError("invalid-argument", "Bad chars");
    }

    const result = await db.runTransaction(async (tx) => {
        // 1. Resolve code -> referrerUid
        const kRef = db.collection("referralKeys").doc(upper);
        const kSnap = await tx.get(kRef);
        if (!kSnap.exists) throw new HttpsError("not-found", "Code not found");
        const referrerUid = kSnap.get("uid");

        if (referrerUid === uid) throw new HttpsError("failed-precondition", "Cannot refer self");

        // 2. Check if user already applied
        const userRef = db.collection("users").doc(uid);
        const uSnap = await tx.get(userRef);
        if (uSnap.exists && uSnap.get("referredByUid")) {
            throw new HttpsError("already-exists", "Already applied a referral code");
        }

        // 3. Get Referrer Data (READ BEFORE WRITE)
        const refUserRef = db.collection("users").doc(referrerUid);
        const rSnap = await tx.get(refUserRef);
        if (!rSnap.exists) return {referrerUid: null, newCount: 0}; // Weird, but stop if referrer invalid

        // --- ALL READS DONE (mostly) ---

        // 4. Mark user (New User Update)
        tx.set(userRef, {
            referredByUid: referrerUid,
            referredByKey: upper,
            referralAppliedAt: FieldValue.serverTimestamp(),
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        // 5. Update Referrer (Referral Count)
        const prevCount = Number(rSnap.get("referralCount") ?? 0) || 0;
        const newCount = prevCount + 1;
        tx.set(refUserRef, {
            referralCount: newCount,
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        // 6. Initialize Lifetime Earnings Record (New Structure)
        // Path: users/{referrer}/referralData/currentReferralEarnings/records/{newUid}
        const lifetimeRef = refUserRef
            .collection("referralData")
            .doc("currentReferralEarnings")
            .collection("records")
            .doc(uid);

        const childName = (uSnap.get("username") as string) || "Guest";

        tx.set(lifetimeRef, {
            childName: childName,
            totalEarned: 0,
            joinedAt: FieldValue.serverTimestamp()
        });

        // 7. Grant Reward (to new user) -> REMOVED per request
        // const cur = Number(uSnap.get("currency") ?? 0) || 0;
        // tx.set(userRef, {
        //    currency: cur + 100
        // }, {merge: true});

        // 8. Check Thresholds for Referrer
        await grantReferralThresholdItems(tx, referrerUid, newCount);

        return {referrerUid, newCount};
    });

    // Update SIGNAL_BOOST achievement for the referrer
    if (result?.referrerUid && result.newCount > 0) {
        try {
            await upsertUserAch(result.referrerUid, ACH_TYPES.SIGNAL_BOOST, result.newCount);
        } catch (e) {
            console.warn("[referral] Failed to update SIGNAL_BOOST achievement:", e);
        }
    }
    return {ok: true, applied: upper};
}


// -------- Cloud Functions --------

export const applyReferralCode = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const code = String(req.data?.code || "");
    return await applyReferralCodeToUser(uid, code);
});


// Trigger: Create minimal profile on first Auth (Auth -> Firestore)
export const createUserProfile = functionsV1.auth.user().onCreate(async (user) => {
    const {uid, email, photoURL} = user;
    const now = FieldValue.serverTimestamp();
    const ref = db.collection("users").doc(uid);

    // RACE CONDITION FIX:
    // Some flows (like referral application) might create the doc BEFORE this trigger runs.
    // Instead of exiting if doc exists, we check if it is "initialized" (has createdAt).
    const snap = await ref.get();

    // If it has 'createdAt', assume it is already fully initialized.
    if (snap.exists && snap.data()?.createdAt) {
        console.log(`[createUserProfile] Profile for ${uid} already fully initialized. Skipping.`);
        return;
    }

    // Get Rank (User Count) - Only fetching if we are truly setting up the user
    // (If it was a partial doc, it likely doesn't have rank/stats/currency yet)
    // Note: If partial doc exists, we merge into it.
    const rank = await getNextUserRank();

    // Default JSONs
    // Use same BASE_STATS logic as shop.functions 
    const statsJson = JSON.stringify({
        "comboPower": 25,
        "playerSpeed": 20,
        "coinMultiplierPercent": 0,
        "gameplaySpeedMultiplierPercent": 0,
        "magnetPowerPercent": 0,
        "playerAcceleration": 0,
        "playerSizePercent": 0,
    });

    // 1 day ago for elitePassExpiresAt
    const OneDayAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);
    const elitePassExpiresAt = Timestamp.fromDate(OneDayAgo);

    console.log(`[createUserProfile] Creating/Merging profile for ${uid} with Rank ${rank}`);

    // Use MERGE to preserve any existing fields (like referralKey, referredByUid)
    await ref.set({
        // Identity (Merge safe: will overwrite with sync'd auth data which is good)
        uid,
        email: email || "",
        mail: email || "",
        photoUrl: photoURL || "",
        // username: "", 
        // DONT overwrite username if it exists (e.g. from early set flow) - handled by merge if we omit it? 
        // actually username is usually empty string initially. If partial doc has it, we shouldn't wipe it.
        // We will explicitly set it ONLY if it is missing in the data we write? No, set with merge merges fields.
        // To be safe, let's conditionally add username only if we want to force empty. 
        // Standard flow: User has no username yet.

        // Timestamps
        createdAt: now, // Critical marker for initialization
        updatedAt: now,
        lastLogin: now,
        lastLoginLocalDate: "",
        tzOffsetMinutes: 0,

        // Subscriptions
        hasElitePass: false,
        elitePassExpiresAt: elitePassExpiresAt,

        // Economy & Score
        currency: 0,
        premiumCurrency: 0,
        maxScore: 0,
        cumulativeCurrencyEarned: 0,

        // Progress / Stats
        level: 1,
        rank: rank,
        trustFactor: 100,
        streak: 1, // FIX #5: Streak starts at 1
        bestStreak: 1, // Match initial streak
        chapterProgress: 1,

        // Game Stats
        statsJson: statsJson,
        adClaimsJson: "",
        totalPlaytimeMinutes: 0,
        totalPlaytimeMinutesFloat: 0,
        totalPlaytimeSec: 0,
        sessionsPlayed: 0,
        maxCombo: 0,
        powerUpsCollected: 0,
        itemsPurchasedCount: 0,

        // Social / Referral
        // referralKey: "", // Danger: Don't overwrite if partial doc has it
        referralCount: 0,
        referralEarnings: 0,
        // referredByUid: "", // Danger: Don't overwrite
        // referredByKey: "", // Danger: Don't overwrite

        // Energy System
        energyCurrent: 5,
        energyMax: 5,
        energyRegenPeriodSec: 14400,
        energyUpdatedAt: now,

        // Internal
        isProfileComplete: false,
    }, {merge: true});
});

// Callable: Complete Profile (Set Username + Apply Referral)
// This replaces ensureUserProfile and acts as the "Finish Registration" step.
export const completeUserProfile = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const usernameRaw = String(req.data?.username || "").trim();
    const referralCode = String(req.data?.referralCode || "").trim();

    // 1. Validate Username
    if (usernameRaw.length < 3 || usernameRaw.length > 20) {
        throw new HttpsError("invalid-argument", "USERNAME_INVALID_LENGTH");
    }
    if (!/^[a-zA-Z0-9._-]+$/.test(usernameRaw)) {
        throw new HttpsError("invalid-argument", "USERNAME_INVALID_CHARS");
    }

    // 1.5 Banned keyword check (same logic as changeUsername)
    let bannedList: string[] = [
        "fuck", "amk", "siktir", "orospu", "piç", "aq", "porno",
    ];

    try {
        const rulesSnap = await db.collection("appdata").doc("usernamerules").get();
        if (rulesSnap.exists) {
            const d = (rulesSnap.data() || {}) as Record<string, any>;
            let fromField = d.bannedKeywords ?? d.bannedkeywords;

            if (Array.isArray(fromField)) {
                bannedList = fromField
                    .map((x) => String(x || "").toLowerCase().trim())
                    .filter((s) => s.length > 0);
            } else if (typeof fromField === "string" && fromField.trim().length > 0) {
                const raw = fromField.trim();
                try {
                    const parsed = JSON.parse(raw);
                    if (parsed && Array.isArray(parsed.bannedKeywords)) {
                        bannedList = parsed.bannedKeywords
                            .map((x: any) => String(x || "").toLowerCase().trim())
                            .filter((s: string) => s.length > 0);
                    }
                } catch (_) {
                    bannedList = raw
                        .split(/[,\s]+/)
                        .map((x) => x.toLowerCase().trim())
                        .filter((s) => s.length > 0);
                }
            }
        }
    } catch (e) {
        console.warn("[completeUserProfile] could not load usernamerules, using fallback list:", e);
    }

    const lower = usernameRaw.toLowerCase();
    for (const bad of bannedList) {
        if (!bad) continue;
        if (lower.includes(bad)) {
            throw new HttpsError("invalid-argument", "USERNAME_BAD_WORD");
        }
    }

    const usernameLower = usernameRaw.toLowerCase();
    const userRef = db.collection("users").doc(uid);
    const nameRef = db.collection("usernames").doc(usernameLower);

    // 2. Transaction: Set Username + Valid Unique Check
    await db.runTransaction(async (tx) => {
        // --- READ PHASE ---
        const [uSnap, nSnap] = await Promise.all([
            tx.get(userRef),
            tx.get(nameRef)
        ]);

        if (!uSnap.exists) throw new HttpsError("not-found", "User profile missing (Auth trigger failed?)");

        // Check if already completed
        if (uSnap.get("isProfileComplete") === true) {
            // If already has this username, just return success
            if (uSnap.get("username") === usernameRaw) return;
            throw new HttpsError("failed-precondition", "Profile already completed.");
        }

        // Check availability
        if (nSnap.exists && nSnap.get("uid") !== uid) {
            throw new HttpsError("already-exists", "USERNAME_TAKEN");
        }

        // Check specific referral key generation needed?
        let myKey = uSnap.get("referralKey");
        let keyToCreate = null;

        if (!myKey) {
            // Perform READ inside logic to find available key
            const k = await findAvailableReferralKey(tx);
            if (k === "FAIL") throw new HttpsError("internal", "Failed to generate referral key");
            myKey = k;
            keyToCreate = k;
        }

        // --- WRITE PHASE ---
        const now = FieldValue.serverTimestamp();

        // 1. Reserve Username
        tx.set(nameRef, {uid, updatedAt: now});

        // 2. Create Referral Key (if needed)
        if (keyToCreate) {
            const kRef = db.collection("referralKeys").doc(keyToCreate);
            tx.set(kRef, {uid, createdAt: now});
        }

        // 3. Update User Profile
        tx.set(userRef, {
            username: usernameRaw,
            usernameLastChangedAt: now,
            isProfileComplete: true,
            updatedAt: now,
            referralKey: myKey
        }, {merge: true});
    });

    // 3. Apply Referral Code (if provided) using existing logic
    // This is separate from the main transaction to avoid complexity, 
    // but safe because isProfileComplete is already true (so user can't retry this step infinitely).
    // Actually, we should try to do it even if separate.
    let referralApplied = false;
    let referralError = "";

    if (referralCode.length > 0) {
        try {
            const res = await applyReferralCodeToUser(uid, referralCode);
            if (res.ok) referralApplied = true;
        } catch (e: any) {
            console.warn(`[completeUserProfile] Referral '${referralCode}' failed: ${e.message}`);
            referralError = e.message;
            // We do NOT fail the profile completion just because referral failed.
        }
    }

    return {success: true, username: usernameRaw, referralApplied, referralError};
});

// List Referred Users
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
                .collection("referralData")
                .doc("currentReferralEarnings")
                .collection("records")
                .doc(childUid);

            const rcSnap = await rcRef.get();
            if (rcSnap.exists) {
                const et = rcSnap.get("totalEarned"); // New field name: totalEarned
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


// Change Username
export const changeUsername = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const newNameRaw = String(req.data?.newName ?? "").trim();
    const now = Timestamp.now();
    const newNameLower = newNameRaw.toLowerCase();

    // --- Basic format validation ---
    if (newNameRaw.length < 3 || newNameRaw.length > 20) {
        throw new HttpsError("invalid-argument", "USERNAME_INVALID_LENGTH");
    }

    if (!/^[a-zA-Z0-9._-]+$/.test(newNameRaw)) {
        throw new HttpsError("invalid-argument", "USERNAME_INVALID_CHARS");
    }

    // --- Banned keyword rules from Firestore (config driven) ---
    let bannedList: string[] = [
        "fuck", "amk", "siktir", "orospu", "piç", "aq", "porno",
    ];

    try {
        const rulesSnap = await db.collection("appdata").doc("usernamerules").get();
        if (rulesSnap.exists) {
            const d = (rulesSnap.data() || {}) as Record<string, any>;
            let fromField = d.bannedKeywords ?? d.bannedkeywords;

            if (Array.isArray(fromField)) {
                bannedList = fromField
                    .map((x) => String(x || "").toLowerCase().trim())
                    .filter((s) => s.length > 0);
            } else if (typeof fromField === "string" && fromField.trim().length > 0) {
                const raw = fromField.trim();
                try {
                    const parsed = JSON.parse(raw);
                    if (parsed && Array.isArray(parsed.bannedKeywords)) {
                        bannedList = parsed.bannedKeywords
                            .map((x: any) => String(x || "").toLowerCase().trim())
                            .filter((s: string) => s.length > 0);
                    }
                } catch (_) {
                    bannedList = raw
                        .split(/[,\s]+/)
                        .map((x) => x.toLowerCase().trim())
                        .filter((s) => s.length > 0);
                }
            }
        }
    } catch (e) {
        console.warn("[changeUsername] could not load usernamerules, using fallback list:", e);
    }

    const lower = newNameLower;
    for (const bad of bannedList) {
        if (!bad) continue;
        if (lower.includes(bad)) {
            throw new HttpsError("invalid-argument", "USERNAME_BAD_WORD");
        }
    }

    const userRef = db.collection("users").doc(uid);
    const nameRef = db.collection("usernames").doc(newNameLower);

    await db.runTransaction(async (tx) => {
        const [uSnap, nameSnap] = await Promise.all([
            tx.get(userRef),
            tx.get(nameRef),
        ]);

        if (!uSnap.exists) {
            throw new HttpsError("failed-precondition", "USER_DOC_MISSING");
        }

        const data = uSnap.data() || {};
        const lastChangedAt = data.usernameLastChangedAt as Timestamp | null;

        // --- Haftada 1 değişim limiti ---
        if (lastChangedAt) {
            const diff = now.toMillis() - lastChangedAt.toMillis();
            const WEEK_MS = 7 * 24 * 60 * 60 * 1000;
            if (diff < WEEK_MS) {
                throw new HttpsError("failed-precondition", "USERNAME_CHANGE_TOO_SOON");
            }
        }

        // --- Benzersiz username kontrolü ---
        if (nameSnap.exists) {
            const owner = nameSnap.get("uid");
            if (owner !== uid) {
                throw new HttpsError("already-exists", "USERNAME_TAKEN");
            }
        }

        // Eski username rezervasyonunu sil
        const oldName = (data.username || "").toLowerCase();
        if (oldName) {
            const oldRef = db.collection("usernames").doc(oldName);
            tx.delete(oldRef);
        }

        // Yeni username'i rezerve et
        tx.set(
            nameRef,
            {uid, updatedAt: now},
            {merge: true}
        );

        // User doc'u güncelle
        tx.set(
            userRef,
            {
                username: newNameRaw,
                usernameLastChangedAt: now,
                updatedAt: FieldValue.serverTimestamp(),
            },
            {merge: true}
        );
    });

    return {ok: true, newName: newNameRaw};
});

// -------- Pending Referral Earnings --------

export const getPendingReferrals = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const pendingCol = db.collection("users")
        .doc(uid)
        .collection("referralData")
        .doc("pendingReferralEarnings")
        .collection("records");
    const snap = await pendingCol.limit(50).get();

    if (snap.empty) {
        return {hasPending: false, items: [], total: 0};
    }

    const items: any[] = [];
    let total = 0;
    snap.forEach(doc => {
        const d = doc.data();
        const amt = Number(d.amount) || 0;
        if (amt > 0) {
            total += amt;
            items.push({
                childUid: d.childUid,
                childName: d.childName || "Unknown",
                amount: amt
            });
        }
    });

    return {hasPending: total > 0, items, total};
});

export const claimReferralEarnings = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const userRef = db.collection("users").doc(uid);
    // New Path: users/{uid}/referralData/pendingReferralEarnings/records
    const pendingCol = userRef
        .collection("referralData")
        .doc("pendingReferralEarnings")
        .collection("records");

    // New Path: users/{uid}/referralData/currentReferralEarnings/records
    const lifetimeCol = userRef
        .collection("referralData")
        .doc("currentReferralEarnings")
        .collection("records");

    return await db.runTransaction(async (tx) => {
        const pSnap = await tx.get(pendingCol.limit(100));
        if (pSnap.empty) {
            return {claimed: 0, items: []};
        }

        const uSnap = await tx.get(userRef);
        if (!uSnap.exists) {
            throw new HttpsError("not-found", "User not found");
        }

        const currentCurrency = Number(uSnap.get("currency") ?? 0) || 0;
        const currentTotalEarned = Number(uSnap.get("referralEarnings") ?? 0) || 0;

        let batchTotal = 0;
        const breakdown: any[] = [];
        const lifetimeUpdates: { ref: FirebaseFirestore.DocumentReference, amt: number }[] = [];

        // 1. Process Pending Docs
        for (const doc of pSnap.docs) {
            const d = doc.data();
            const childUid = doc.id;
            const amt = Number(d.amount) || 0;
            const cName = d.childName || "Unknown";

            if (amt <= 0) {
                tx.delete(doc.ref);
                continue;
            }

            batchTotal += amt;
            breakdown.push({
                childUid,
                childName: cName,
                amount: amt
            });

            // Prepare lifetime update
            const lifetimeRef = lifetimeCol.doc(childUid);
            // We need to read lifetime doc to increment it? 
            // Yes, inside TX.
            // CAUTION: Reading inside loop. 100 items limit is okay for 500 ops.
            // Optimize: We could use FieldValue.increment if we didn't need to read 'joinedAt'.
            // Actually, we can use set({totalEarned: FieldValue.increment(amt)}, {merge:true}) without reading!
            // This saves 100 reads.

            // However, verify if logic requires reading. Plan said "Update... increment".
            // Using FieldValue.increment is safer and cheaper.
            lifetimeUpdates.push({ref: lifetimeRef, amt});

            // Delete pending
            tx.delete(doc.ref);
        }

        // Fix float precision for batchTotal
        batchTotal = Number(batchTotal.toFixed(3));

        if (batchTotal > 0) {
            // 2. Create Transaction Record
            // Path: users/{uid}/referralData/referralTransactions/records/{autoId}
            const txRef = userRef
                .collection("referralData")
                .doc("referralTransactions")
                .collection("records")
                .doc();

            tx.set(txRef, {
                claimedAt: FieldValue.serverTimestamp(),
                totalAmount: batchTotal,
                breakdown: breakdown
            });

            // 3. Update Lifetime Docs (Batch)
            for (const item of lifetimeUpdates) {
                tx.set(item.ref, {
                    totalEarned: FieldValue.increment(item.amt),
                    // We don't overwrite childName/joinedAt, just increment total
                }, {merge: true});
            }

            // 4. Update User Properties
            tx.set(userRef, {
                currency: currentCurrency + batchTotal,
                referralEarnings: currentTotalEarned + batchTotal, // Update global stat
                updatedAt: FieldValue.serverTimestamp()
            }, {merge: true});
        }

        return {claimed: batchTotal, count: breakdown.length};
    });
});


// Sync Email: Updates the user's email in Firestore from the Auth Token
// Useful for GPGS users who just granted Email scope after account creation.
export const syncUserEmail = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const email = req.auth?.token.email;
    if (!email) {
        return {updated: false, reason: "No email in auth token"};
    }

    await db.collection("users").doc(uid).set({
        email: email,
        mail: email,
        updatedAt: FieldValue.serverTimestamp()
    }, {merge: true});

    return {updated: true, email: email};
});
