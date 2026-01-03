import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {normId, extractItemStats, mergeStats, parseStatsJson} from "../utils/helpers";
import {DB_ID, ACH_TYPES} from "../utils/constants";
import {upsertUserAch} from "./achievements.functions";

// ---- Consumables lazy cleanup helper (Exported for Energy/Session modules) ----
export async function cleanupExpiredConsumablesInTx(
    tx: FirebaseFirestore.Transaction,
    userRef: FirebaseFirestore.DocumentReference,
    now: Timestamp
) {
    const activeCol = userRef.collection("activeConsumables");

    // READS first: all reads before writes
    const expiredSnap = await tx.get(
        activeCol.where("expiresAt", "<=", now)
    );
    if (expiredSnap.empty) return;

    let totalDelta: Record<string, number> = {};
    const itemRefs: FirebaseFirestore.DocumentReference[] = [];
    const toDelete: FirebaseFirestore.DocumentReference[] = [];

    expiredSnap.docs.forEach((d) => {
        const itemId = d.id;
        const itemRef = db
            .collection("appdata")
            .doc("items")
            .collection(itemId)
            .doc("itemdata");
        itemRefs.push(itemRef);
        toDelete.push(d.ref);
    });

    // READ: item defs (stats)
    const itemSnaps = await Promise.all(itemRefs.map((r) => tx.get(r)));
    itemSnaps.forEach((s) => {
        if (!s.exists) return;
        const stats = extractItemStats(s.data() || {});
        totalDelta = mergeStats(totalDelta, stats, -1); // subtract
    });

    // WRITE: apply merged subtraction to user's stats
    if (Object.keys(totalDelta).length > 0) {
        const uSnap = await tx.get(userRef);
        const baseStats = parseStatsJson(uSnap.get("statsJson"));
        const merged = mergeStats(baseStats, totalDelta, 1); // totalDelta already negative
        tx.update(userRef, {
            statsJson: JSON.stringify(merged),
            updatedAt: FieldValue.serverTimestamp(),
        });
    }

    // WRITE: delete expired docs
    toDelete.forEach((ref) => tx.delete(ref));
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
        itemDollarPrice: num(p.itemDollarPrice, 0),
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
        platform,
        receipt,
        orderId,
        adToken,
    } = (req.data || {}) as {
        itemId?: string;
        method?: string;
        platform?: string;
        receipt?: string;
        orderId?: string;
        adToken?: string;
    };

    const itemId = normId(rawItemId);
    if (!itemId) throw new HttpsError("invalid-argument", "itemId required.");
    const m = String(method || "").toUpperCase();
    if (!["GET", "IAP", "AD", "PREMIUM"].includes(m)) {
        throw new HttpsError("invalid-argument", "Invalid method. Use GET | IAP | AD | PREMIUM.");
    }

    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");
    const userRef = db.collection("users").doc(uid);
    const invRef = userRef.collection("inventory").doc(itemId);
    const acRef = userRef.collection("activeConsumables").doc(itemId);
    const now = Timestamp.now();

    // ---- Pre-verify tokens ----
    const verifyIapReceipt = async (platform?: string, receipt?: string, orderId?: string) => {
        if (!platform || !receipt || !orderId) {
            throw new HttpsError("invalid-argument", "platform, receipt and orderId are required for IAP.");
        }
        const lockRef = userRef.collection("iapReceipts").doc(orderId);
        const existing = await lockRef.get();
        if (existing.exists) {
            throw new HttpsError("already-exists", "This purchase receipt was already processed.");
        }
        await lockRef.set({usedAt: now, platform, previewHash: String(receipt).slice(0, 32)}, {merge: true});
    };
    const verifyAdGrant = async (adToken?: string) => {
        if (!adToken) throw new HttpsError("invalid-argument", "adToken required for AD method.");
        const grantRef = userRef.collection("adGrants").doc(adToken);
        const g = await grantRef.get();
        if (g.exists) {
            throw new HttpsError("already-exists", "This ad grant token was already used.");
        }
        await grantRef.set({usedAt: now}, {merge: true});
    };

    if (m === "IAP") {
        await verifyIapReceipt(platform, receipt, orderId);
    } else if (m === "AD") {
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
            throw new HttpsError("failed-precondition", "Referral-only item cannot be purchased.");
        }

        const isConsumable = !!item.itemIsConsumable;
        const priceGet = Number(item.itemGetPrice ?? 0) || 0;
        const pricePremium = Number(item.itemPremiumPrice ?? 0) || 0;
        const priceUsd = Number(item.itemDollarPrice ?? 0) || 0;
        const isAd = !!item.itemIsRewardedAd;

        if (m === "GET" && priceGet <= 0) throw new HttpsError("failed-precondition", "Not purchasable with GET.");
        if (m === "IAP" && priceUsd <= 0) throw new HttpsError("failed-precondition", "Not an IAP item.");
        if (m === "AD" && !isAd) throw new HttpsError("failed-precondition", "Not ad-reward purchasable.");
        if (m === "PREMIUM" && pricePremium <= 0) throw new HttpsError("failed-precondition", "Not purchasable with premium.");

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
            priceUsd: m === "IAP" ? priceUsd : 0,
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

    await db.runTransaction(async (tx) => {
        const [invSnap, itemSnap, loadSnap, userSnap] = await Promise.all([
            tx.get(invRef),
            tx.get(itemRef),
            tx.get(loadRef),
            tx.get(userRef),
        ]);

        if (!invSnap.exists || !invSnap.get("owned")) {
            throw new HttpsError("failed-precondition", "Item not owned.");
        }
        if (!itemSnap.exists) {
            throw new HttpsError("not-found", "Item not found.");
        }
        const isConsumable = !!itemSnap.get("itemIsConsumable");
        if (isConsumable) throw new HttpsError("failed-precondition", "Consumables cannot be equipped.");

        let equipped: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
        equipped = equipped.map((x: string) => normId(x));
        const wasEquipped = equipped.includes(itemId);
        if (!wasEquipped) equipped.push(itemId);

        tx.set(loadRef, {equippedItemIds: equipped, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        tx.set(invRef, {equipped: true, lastChangedAt: FieldValue.serverTimestamp()}, {merge: true});

        if (!wasEquipped) {
            const baseStats = parseStatsJson(userSnap.get("statsJson"));
            const itemStats = extractItemStats(itemSnap.data() || {});
            const merged = mergeStats(baseStats, itemStats, 1);
            tx.update(userRef, {statsJson: JSON.stringify(merged), updatedAt: FieldValue.serverTimestamp()});
        }
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
    const itemRef = db.collection("appdata").doc("items").collection(itemId).doc("itemdata");

    await db.runTransaction(async (tx) => {
        const [loadSnap, userSnap, itemSnap] = await Promise.all([
            tx.get(loadRef),
            tx.get(userRef),
            tx.get(itemRef),
        ]);

        let before: string[] = loadSnap.exists ? (loadSnap.get("equippedItemIds") || []) : [];
        const beforeNorm = before.map((x: string) => normId(x));
        const wasEquipped = beforeNorm.includes(itemId);
        const afterEquipped = beforeNorm.filter((x) => x !== itemId);

        tx.set(loadRef, {equippedItemIds: afterEquipped, updatedAt: FieldValue.serverTimestamp()}, {merge: true});
        tx.set(invRef, {equipped: false, lastChangedAt: FieldValue.serverTimestamp()}, {merge: true});

        if (wasEquipped) {
            if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found.");
            const baseStats = parseStatsJson(userSnap.get("statsJson"));
            const itemStats = extractItemStats(itemSnap.data() || {});
            const merged = mergeStats(baseStats, itemStats, -1);
            tx.update(userRef, {statsJson: JSON.stringify(merged), updatedAt: FieldValue.serverTimestamp()});
        }
    });

    return {ok: true, itemId};
});
