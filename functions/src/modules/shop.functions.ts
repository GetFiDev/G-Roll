import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {normId, extractItemStats, mergeStats, parseStatsJson} from "../utils/helpers";
import {DB_ID, ACH_TYPES} from "../utils/constants";
import {upsertUserAch} from "./achievements.functions";

const BASE_STATS: Record<string, number> = {
    "comboPower": 25,
    "playerSpeed": 20,
    // Add others if needed with 0 defaults or specific values
    "coinMultiplierPercent": 0,
    "gameplaySpeedMultiplierPercent": 0,
    "playerAcceleration": 0,
    "maxScore": 0,
    "playerSizePercent": 0,
    "magnetPowerPercent": 0
};

// Helper: Calculate total stats from a list of item IDs (equipped + active consumables)
// This function MUST be called after all necessary reads (itemRefs) are performed.
// It returns the final stats object.
function computeTotalStats(
    itemSnaps: FirebaseFirestore.DocumentSnapshot[]
): Record<string, number> {
    let totalStats: Record<string, number> = {...BASE_STATS};
    for (const snap of itemSnaps) {
        if (!snap.exists) continue;
        const stats = extractItemStats(snap.data() || {});
        totalStats = mergeStats(totalStats, stats, 1);
    }
    return totalStats;
}

// Helper: Fetches item snapshots for a list of item IDs. READ ONLY.
async function fetchItemSnaps(
    tx: FirebaseFirestore.Transaction,
    ids: string[]
): Promise<FirebaseFirestore.DocumentSnapshot[]> {
    if (ids.length === 0) return [];

    // Deduplicate
    const uniqueIds = Array.from(new Set(ids)).map(x => normId(x));
    const itemRefs = uniqueIds.map(id => db.collection("appdata").doc("items").collection(id).doc("itemdata"));
    return await Promise.all(itemRefs.map(r => tx.get(r)));
}

// ---- Consumables lazy cleanup helper (Exported for Energy/Session modules) ----
// ---- Consumables lazy cleanup helper (Exported for Energy/Session modules) ----
export async function cleanupExpiredConsumablesInTx(
    tx: FirebaseFirestore.Transaction,
    userRef: FirebaseFirestore.DocumentReference,
    now: Timestamp
) {
    const activeCol = userRef.collection("activeConsumables");
    const loadRef = userRef.collection("loadout").doc("current");

    // READ PHASE:
    // 1. Get Expired (<now)
    // 2. Get Active (>now)
    // 3. Get Loadout (to know equipped items for full recalc)

    const [expiredSnap, activeSnap, loadSnap] = await Promise.all([
        tx.get(activeCol.where("expiresAt", "<=", now)),
        tx.get(activeCol.where("expiresAt", ">", now)),
        tx.get(loadRef)
    ]);

    if (expiredSnap.empty) return; // Nothing to clean up

    // Gather IDs for recalculation
    const equippedIds: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
    const activeIds: string[] = activeSnap.docs.map(d => d.id);
    const allIds = [...equippedIds, ...activeIds];

    // Fetch Item Data for all valid items
    const itemSnaps = await fetchItemSnaps(tx, allIds);

    // WRITE PHASE:
    // 1. Delete Expired
    expiredSnap.docs.forEach((d) => {
        tx.delete(d.ref);
    });

    // 2. Compute & Update Stats
    const totalStats = computeTotalStats(itemSnaps);

    tx.update(userRef, {
        statsJson: JSON.stringify(totalStats),
        updatedAt: FieldValue.serverTimestamp()
    });
}

// ========================= getAllItems =========================
export const getAllItems = onCall(async (request) => {
    try {
        console.log(`[getAllItems:start] db=${DB_ID}`);
        const itemsCol = db.collection("appdata").doc("items");
        const itemsSnap = await itemsCol.listCollections();

        const out: Record<string, any> = {};

        for (const subCol of itemsSnap) {
            const docSnap = await subCol.doc("itemdata").get();
            if (!docSnap.exists) continue;
            out[subCol.id] = docSnap.data();
        }

        const sample = Object.keys(out).slice(0, 5);
        console.log(`[getAllItems:done] count=${Object.keys(out).length} sample=[${sample.join(', ')}]`);
        return {
            ok: true,
            items: out,
            count: Object.keys(out).length,
        };
    } catch (err) {
        console.error("[getAllItems] error", err);
        return {
            ok: false,
            error: (err as Error).message || "unknown",
        };
    }
});

// ========================= createItem =========================
export const createItem = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const p = (req.data as Record<string, any>) || {};

    // ---- helper'lar
    const num = (v: any, def = 0) =>
        Number.isFinite(Number(v)) ? Number(v) : def;
    const str = (v: any, def = "") =>
        typeof v === "string" ? v : def;
    const bool = (v: any, def = false) =>
        typeof v === "boolean" ? v : !!def;

    const itemName = str(p.itemName, "itemname (demo)").trim();
    // "item_<slug>"
    const slug = itemName
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "_")
        .replace(/^_+|_+$/g, "");
    const baseId = `item_${slug || "noname"}`;

    // yazılacak veri (tüm alanlar)
    const docData = {
        itemDescription: str(p.itemDescription, "item description demo"),
        // REMOVED: itemDollarPrice: num(p.itemDollarPrice, 0),
        itemGetPrice: num(p.itemGetPrice, 0.05),
        itemPremiumPrice: num(p.itemPremiumPrice, 0),
        itemIconUrl: str(
            p.itemIconUrl,
            "https://cdn-icons-png.freepik.com/256/4957/4957671.png"
        ),
        itemIsConsumable: bool(p.itemIsConsumable, false),
        itemIsRewardedAd: bool(p.itemIsRewardedAd, false),
        itemName,
        itemReferralThreshold: num(p.itemReferralThreshold, 0),

        itemstat_coinMultiplierPercent: num(p.itemstat_coinMultiplierPercent, 0),
        itemstat_comboPower: num(p.itemstat_comboPower, 0),
        itemstat_gameplaySpeedMultiplierPercent: num(
            p.itemstat_gameplaySpeedMultiplierPercent,
            0
        ),
        itemstat_magnetPowerPercent: num(p.itemstat_magnetPowerPercent, 0),
        itemstat_playerAcceleration: num(p.itemstat_playerAcceleration, 0),
        itemstat_playerSizePercent: num(p.itemstat_playerSizePercent, 0),
        itemstat_playerSpeed: num(p.itemstat_playerSpeed, 0),

        createdAt: Timestamp.now(),
        updatedAt: FieldValue.serverTimestamp(),
        createdBy: uid,
    };

    // Aynı isim varsa çakışmayı çöz: baseId, sonra kısa random ekle
    let itemId = baseId;
    for (let attempt = 0; attempt < 5; attempt++) {
        const ref = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
        const snap = await ref.get();
        if (!snap.exists) {
            await ref.set(docData, {merge: false});
            return {
                ok: true,
                itemId,
                path: `appdata/items/${itemId}/itemdata`,
            };
        }
        // çakıştı; yeni bir ek kuyruk dene
        itemId = `${baseId}_${Math.random().toString(36).slice(2, 6)}`;
    }

    throw new HttpsError(
        "aborted",
        "Could not allocate a unique itemId after several attempts."
    );
});

// ========================= checkOwnership =========================
export const checkOwnership = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    console.log(`[checkOwnership:start] uid=${uid} db=${DB_ID}`);

    const userRef = db.collection("users").doc(uid);
    const invCol = userRef.collection("inventory");

    // 1) owned=true olanlar
    const ownedQ = await invCol.where("owned", "==", true).get();

    // 2) quantity>0 olanlar (consumable'lar için)
    let qtyQDocs: FirebaseFirestore.QueryDocumentSnapshot[] = [];
    try {
        const qtyQ = await invCol.where("quantity", ">", 0).get();
        qtyQDocs = qtyQ.docs;
    } catch (e) {
        qtyQDocs = [];
    }

    // 3) Birleştir + normalize et + tekilleştir
    const set = new Set<string>();
    ownedQ.forEach(d => set.add(normId(d.id)));
    qtyQDocs.forEach(d => set.add(normId(d.id)));

    const itemIds = Array.from(set.values()).sort();

    console.log(`[checkOwnership:done] uid=${uid} ownedQ=${ownedQ.size} qtyQ=${qtyQDocs.length} count=${itemIds.length}`);

    return {
        ok: true,
        count: itemIds.length,
        itemIds,
    };
});

// ========================= Inventory System =========================
export const getInventorySnapshot = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const userRef = db.collection("users").doc(uid);
    const invCol = userRef.collection("inventory");
    const loadRef = userRef.collection("loadout").doc("current");
    const [invSnap, loadSnap] = await Promise.all([
        invCol.get(),
        loadRef.get()
    ]);
    console.log(`[getInventorySnapshot] uid=${uid} invDocs=${invSnap.size} loadoutDoc=${loadSnap.exists} db=${DB_ID}`);
    const inventory: Record<string, any> = {};
    invSnap.forEach((d) => {
        const id = normId(d.id);
        inventory[id] = {id, ...d.data()};
    });
    const equippedItemIds: string[] = loadSnap.exists
        ? (loadSnap.get("equippedItemIds") || []).map((x: string) => normId(x))
        : [];
    return {ok: true, inventory, equippedItemIds};
});

// ---------------- purchaseItem ----------------
export const purchaseItem = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    // method: "GET" | "IAP" | "AD" | "PREMIUM"
    const {
        itemId: rawItemId,
        method,
        // REMOVED: platform, receipt, orderId for IAP
        adToken,
    } = (req.data || {}) as {
        itemId?: string;
        method?: string;
        adToken?: string;
    };

    const itemId = normId(rawItemId);
    if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");
    const m = String(method || "").toUpperCase();
    if (!["GET", "AD", "PREMIUM"].includes(m)) {
        throw new HttpsError("invalid-argument", "Invalid method. Use GET | AD | PREMIUM.");
    }

    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const acRef = userRef.collection("activeConsumables").doc(itemId);
    const now = Timestamp.now();

    // ---- Pre-verify tokens ----
    const verifyAdGrant = async (adToken?: string) => {
        if (!adToken) throw new HttpsError("invalid-argument", "adToken required for AD method.");
        const grantRef = userRef.collection("adGrants").doc(adToken);
        const g = await grantRef.get();
        if (g.exists) {
            throw new HttpsError("already-exists", "This ad grant token was already used.");
        }
        await grantRef.set({usedAt: now}, {merge: true});
    };

    // Verify Ad Token if needed
    if (m === "AD") {
        await verifyAdGrant(adToken);
    }

    const res = await db.runTransaction(async (tx) => {
        const [itemSnap, userSnap, invSnap, acSnap] = await Promise.all([
            tx.get(itemRef),
            tx.get(userRef),
            tx.get(invRef),
            tx.get(acRef),
        ]);

        if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found.");
        const item = itemSnap.data() || {};

        const isReferralOnly = Number(item.itemReferralThreshold ?? 0) > 0;
        if (isReferralOnly) {
            // Check if user has enough referrals
            const userRefCount = Number(userSnap.get("referralCount") ?? 0) || 0;
            const threshold = Number(item.itemReferralThreshold);
            if (userRefCount < threshold) {
                throw new HttpsError("failed-precondition", `Need ${threshold} referrals (have ${userRefCount}).`);
            }
            // If met, allow proceeding (will treat as Price=0 or special grant)
            // Continue execution... logic below handles price=0 properly for GET/PREMIUM 
            // if we set them to 0 effectively or they are 0.
        }

        const isConsumable = !!item.itemIsConsumable;
        const priceGet = Number(item.itemGetPrice ?? 0) || 0;
        const pricePremium = Number(item.itemPremiumPrice ?? 0) || 0;
        // REMOVED: const priceUsd = Number(item.itemDollarPrice ?? 0) || 0;
        const isAd = !!item.itemIsRewardedAd;

        if (m === "GET" && priceGet <= 0 && !isReferralOnly) {
            throw new HttpsError("failed-precondition", "Not purchasable with GET.");
        }
        // REMOVED: if (m === "IAP" && priceUsd <= 0) ...
        if (m === "AD" && !isAd) throw new HttpsError("failed-precondition", "Not ad-reward purchasable.");
        if (m === "PREMIUM" && pricePremium <= 0 && !isReferralOnly) {
            throw new HttpsError("failed-precondition", "Not purchasable with premium.");
        }

        const alreadyOwned = invSnap.exists && !!invSnap.get("owned");
        if (alreadyOwned && !isConsumable) {
            throw new HttpsError("failed-precondition", "Already owned.");
        }

        // Charge / Touch
        if (m === "GET") {
            const curBalance = Number(userSnap.get("currency") ?? 0) || 0;
            if (curBalance < priceGet) throw new HttpsError("failed-precondition", "Not enough currency.");
            tx.update(userRef, {currency: curBalance - priceGet, updatedAt: FieldValue.serverTimestamp()});
        } else if (m === "PREMIUM") {
            const curPremium = Number(userSnap.get("premiumCurrency") ?? 0) || 0;
            if (curPremium < pricePremium) throw new HttpsError("failed-precondition", "Not enough premium currency.");
            tx.update(userRef, {premiumCurrency: curPremium - pricePremium, updatedAt: FieldValue.serverTimestamp()});
        } else {
            tx.set(userRef, {updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        }

        let newExpiry: Timestamp | null = null;

        if (isConsumable) {
            const prevExpiry: Timestamp | null = acSnap.exists ? (acSnap.get("expiresAt") as Timestamp | null) : null;
            const wasActive = !!prevExpiry && prevExpiry.toMillis() > now.toMillis();
            const baseMillis = (wasActive && prevExpiry) ? prevExpiry.toMillis() : now.toMillis();
            const durationMs = 24 * 60 * 60 * 1000;
            newExpiry = Timestamp.fromMillis(baseMillis + durationMs);

            tx.set(acRef, {
                itemId,
                active: true,
                expiresAt: newExpiry,
                lastActivatedAt: now,
                updatedAt: FieldValue.serverTimestamp(),
            }, {merge: true});

            if (!wasActive) {
                const itemStats = extractItemStats(item);
                if (Object.keys(itemStats).length > 0) {
                    const baseStats = parseStatsJson(userSnap.get("statsJson"));
                    const merged = mergeStats(baseStats, itemStats, 1);
                    tx.update(userRef, {
                        statsJson: JSON.stringify(merged),
                        updatedAt: FieldValue.serverTimestamp(),
                    });
                }
            }
        } else {
            const invData: any = {
                owned: true,
                equipped: invSnap.get("equipped") === true,
                quantity: 0,
                itemIsConsumable: false,
                lastChangedAt: FieldValue.serverTimestamp(),
                acquiredAt: invSnap.exists ? invSnap.get("acquiredAt") ?? now : now,
            };
            tx.set(invRef, invData, {merge: true});
        }

        const logRef = userRef.collection("purchases").doc();
        tx.set(logRef, {
            itemId,
            method: m,
            priceGet: m === "GET" ? priceGet : 0,
            pricePremium: m === "PREMIUM" ? pricePremium : 0,
            priceUsd: 0, // No real money items anymore
            isConsumable,
            expiresAt: newExpiry,
            at: now,
        });

        tx.set(userRef, {
            itemsPurchasedCount: FieldValue.increment(1),
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        const currencyLeft = m === "GET"
            ? Math.max(0, (Number(userSnap.get("currency") ?? 0) || 0) - priceGet)
            : Number(userSnap.get("currency") ?? 0) || 0;

        const premiumCurrencyLeft = m === "PREMIUM"
            ? Math.max(0, (Number(userSnap.get("premiumCurrency") ?? 0) || 0) - pricePremium)
            : Number(userSnap.get("premiumCurrency") ?? 0) || 0;

        return {
            ok: true,
            itemId,
            owned: !isConsumable,
            isConsumable,
            currencyLeft,
            premiumCurrencyLeft,
            expiresAt: newExpiry ? newExpiry.toDate().toISOString() : null,
            expiresAtMillis: newExpiry ? newExpiry.toMillis() : null,
        };
    });

    try {
        const u = await db.collection("users").doc(uid).get();
        await upsertUserAch(uid, ACH_TYPES.MARKET_WHISPER, Number(u.get("itemsPurchasedCount") ?? 0) || 0);
    } catch (e) {
        console.warn("[ach] purchase evaluate failed", e);
    }

    return res;
});

// ---------------- getActiveConsumables ----------------
export const getActiveConsumables = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const userRef = db.collection("users").doc(uid);
    const now = Timestamp.now();
    await db.runTransaction(async (tx) => {
        await cleanupExpiredConsumablesInTx(tx, userRef, now);
    });

    const snap = await userRef
        .collection("activeConsumables")
        .where("expiresAt", ">", now)
        .get();

    const items = snap.docs.map((d) => {
        const data = d.data() || {};
        const exp = (data.expiresAt as Timestamp | undefined) || null;
        return {
            itemId: d.id,
            active: true,
            expiresAt: exp ? exp.toDate().toISOString() : null,
            expiresAtMillis: exp ? exp.toMillis() : null,
        };
    });

    return {ok: true, serverNowMillis: now.toMillis(), items};
});

// ---------------- equipItem ----------------
export const equipItem = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const rawItemId = (req.data?.itemId as string) || "";
    const itemId = normId(rawItemId);
    if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");

    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const loadRef = userRef.collection("loadout").doc("current");
    const activeCol = userRef.collection("activeConsumables");
    const now = Timestamp.now();

    await db.runTransaction(async (tx) => {
        // READ PHASE
        const [invSnap, itemSnap, loadSnap, activeSnap] = await Promise.all([
            tx.get(invRef),
            tx.get(itemRef),
            tx.get(loadRef),
            tx.get(activeCol.where("expiresAt", ">", now))
        ]);

        if (!invSnap.exists || !invSnap.get("owned")) {
            throw new HttpsError("failed-precondition", "Item not owned.");
        }
        if (!itemSnap.exists) {
            throw new HttpsError("not-found", "Item not found.");
        }
        const isConsumable = !!itemSnap.get("itemIsConsumable");
        if (isConsumable) throw new HttpsError("failed-precondition", "Consumables cannot be equipped.");

        // Determine new equipped list
        let equipped: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
        equipped = equipped.map((x: string) => normId(x));

        if (!equipped.includes(itemId)) {
            if (equipped.length >= 6) {
                throw new HttpsError("resource-exhausted", "MAX_EQUIPPED_REACHED");
            }
            equipped.push(itemId);
        }

        // Collect all IDs needed for stats calculation
        const activeIds: string[] = activeSnap.docs.map(d => d.id);
        const allIds = [...equipped, ...activeIds];

        // Fetch detailed item data for all stats
        const itemSnaps = await fetchItemSnaps(tx, allIds);

        // WRITE PHASE
        tx.set(loadRef, {equippedItemIds: equipped, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        tx.set(invRef, {equipped: true, lastChangedAt: FieldValue.serverTimestamp()}, {merge: true});

        // Update stats
        const totalStats = computeTotalStats(itemSnaps);
        tx.update(userRef, {
            statsJson: JSON.stringify(totalStats),
            updatedAt: FieldValue.serverTimestamp()
        });
    });

    return {ok: true, itemId};
});

// ---------------- unequipItem ----------------
export const unequipItem = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");
    const rawItemId = (req.data?.itemId as string) || "";
    const itemId = normId(rawItemId);
    if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");

    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const loadRef = userRef.collection("loadout").doc("current");
    const activeCol = userRef.collection("activeConsumables");
    const now = Timestamp.now();

    await db.runTransaction(async (tx) => {
        // READ PHASE
        const [loadSnap, activeSnap] = await Promise.all([
            tx.get(loadRef),
            tx.get(activeCol.where("expiresAt", ">", now))
        ]);

        let before: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
        const beforeNorm = before.map((x: string) => normId(x));
        const afterEquipped = beforeNorm.filter((x) => x !== itemId);

        // Collect all IDs needed for stats calculation
        const activeIds: string[] = activeSnap.docs.map(d => d.id);
        const allIds = [...afterEquipped, ...activeIds];

        // Fetch detailed item data
        const itemSnaps = await fetchItemSnaps(tx, allIds);

        // WRITE PHASE
        tx.set(loadRef, {equippedItemIds: afterEquipped, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        tx.set(invRef, {equipped: false, lastChangedAt: FieldValue.serverTimestamp()}, {merge: true});

        // Update stats
        const totalStats = computeTotalStats(itemSnaps);
        tx.update(userRef, {
            statsJson: JSON.stringify(totalStats),
            updatedAt: FieldValue.serverTimestamp()
        });
    });

    return {ok: true, itemId};
});
