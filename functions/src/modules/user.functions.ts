import * as functionsV1 from "firebase-functions/v1";
import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {randomReferralKey} from "../utils/helpers"; // Removed normId
import {ALPHABET} from "../utils/constants"; // Removed ACH_TYPES
// Removed upsertUserAch

// -------- Helper Functions --------
async function ensureReferralKeyFor(uid: string, tx: FirebaseFirestore.Transaction): Promise<string> {
    const userRef = db.collection("users").doc(uid);
    const userSnap = await tx.get(userRef);
    if (userSnap.exists) {
        const k = userSnap.get("referralKey");
        if (k) return k;
    }

    // Generate unique
    for (let i = 0; i < 5; i++) {
        const k = randomReferralKey(12, ALPHABET); // Fixed args
        const refKey = db.collection("referralKeys").doc(k);
        const s = await tx.get(refKey);
        if (!s.exists) {
            tx.set(refKey, {uid, createdAt: FieldValue.serverTimestamp()});
            tx.set(userRef, {referralKey: k}, {merge: true});
            return k;
        }
    }
    return "FAIL";
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

        const snap = await tx.get(q);
        if (snap.empty) return;

        for (const d of snap.docs) {
            const data = d.data();
            const thresh = Number(data.itemReferralThreshold);
            // Logic: Each item is granted exactly once when threshold met?
            // Or is it "if (newCount == thresh)"? usually ONCE.
            if (newCount === thresh) {
                const itemId = d.ref.parent.id; // item id is parent collection name
                const invRef = db.collection("users").doc(referrerUid).collection("inventory").doc(itemId);
                tx.set(invRef, {
                    owned: true,
                    acquiredAt: FieldValue.serverTimestamp(),
                    source: "referral_bonus"
                }, {merge: true});
            }
        }
    } catch (e) {
        console.warn("grantReferralThresholdItems failed (index missing?)", e);
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

    await db.runTransaction(async (tx) => {
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

        // 3. Mark user
        tx.set(userRef, {
            referredByUid: referrerUid,
            referralAppliedAt: FieldValue.serverTimestamp(),
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        // 4. Update Referrer
        const refUserRef = db.collection("users").doc(referrerUid);
        const rSnap = await tx.get(refUserRef);
        if (!rSnap.exists) return; // Weird

        const prevCount = Number(rSnap.get("referralCount") ?? 0) || 0;
        const newCount = prevCount + 1;
        tx.set(refUserRef, {
            referralCount: newCount,
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        // 5. Add to referralsChildren subcollection (for listing)
        const childRef = refUserRef.collection("referralsChildren").doc(uid);
        tx.set(childRef, {
            appliedAt: FieldValue.serverTimestamp(),
            earnedTotal: 0
        });

        // 6. Grant Reward (to new user) -> 100 currency default
        const cur = Number(uSnap.get("currency") ?? 0) || 0;
        tx.set(userRef, {currency: cur + 100}, {merge: true});

        // 7. Check Thresholds for Referrer
        await grantReferralThresholdItems(tx, referrerUid, newCount);
    });
    return {ok: true, applied: upper};
}


// -------- Cloud Functions --------

export const applyReferralCode = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const code = String(req.data?.code || "");
    return await applyReferralCodeToUser(uid, code);
});


// Trigger: Create basic profile on first Auth
export const createUserProfile = functionsV1.auth.user().onCreate(async (user) => {
    const {uid, email, displayName, photoURL} = user;
    const now = FieldValue.serverTimestamp();
    const ref = db.collection("users").doc(uid);
    const snap = await ref.get();
    if (!snap.exists) {
        await ref.set({
            uid,
            email,
            username: displayName || "Guest",
            photoUrl: photoURL || "",
            createdAt: now,
            updatedAt: now,
            currency: 0,
            premiumCurrency: 0,
            maxScore: 0,
            level: 1,
        });
        console.log(`[createUserProfile] created for ${uid}`);
    }
});


// Callable: Ensure profile exists + get referral key
export const ensureUserProfile = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    let key = "";
    await db.runTransaction(async (tx) => {
        const ref = db.collection("users").doc(uid);
        const s = await tx.get(ref);
        if (!s.exists) {
            tx.set(ref, {
                uid,
                createdAt: FieldValue.serverTimestamp(),
                updatedAt: FieldValue.serverTimestamp(),
                currency: 0,
                maxScore: 0
            });
        }
        key = await ensureReferralKeyFor(uid, tx);
    });

    return {
        ok: true,
        referralKey: key,
    };
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
                .collection("referralsChildren")
                .doc(childUid);
            const rcSnap = await rcRef.get();
            if (rcSnap.exists) {
                const et = rcSnap.get("earnedTotal");
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
