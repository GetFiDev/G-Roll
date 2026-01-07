import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as functionsV1 from "firebase-functions/v1";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";

// ========================= IAP Verification =========================

// Load Google Service Account
let googleAuthClient: any = null;
async function getGoogleAuth() {
    if (googleAuthClient) return googleAuthClient;
    const {google} = require("googleapis");
    const path = require("path");
    const fs = require("fs");

    // Strategy: Try multiple potential locations for service-account.json
    // 1. ../service-account.json (relative to lib/modules/iap.functions.js -> lib/service-account.json)
    // 2. ../../service-account.json (relative to lib/modules -> root workspace)
    // 3. ./service-account.json (relative to CWD)

    // Cloud Functions structure can vary. We'll check existence before requiring to avoid massive error dumps.
    const candidates = [
        path.resolve(__dirname, "../service-account.json"),
        path.resolve(__dirname, "../../service-account.json"),
        path.resolve(process.cwd(), "service-account.json"),
        path.resolve(process.cwd(), "lib/service-account.json"),
    ];

    let foundPath: string | null = null;

    console.log(`[IAP] Looking for service-account.json. __dirname: ${__dirname}, CWD: ${process.cwd()}`);

    for (const p of candidates) {
        if (fs.existsSync(p)) {
            console.log(`[IAP] Found key at: ${p}`);
            foundPath = p;
            break;
        }
    }

    if (!foundPath) {
        console.error("[IAP] service-account.json NOT FOUND in common locations.");
        // Debugging: List contents of key directories
        try {
            console.log(`[IAP DEBUG] ls __dirname (${__dirname}):`, fs.readdirSync(__dirname));
            console.log(`[IAP DEBUG] ls ../ (${path.resolve(__dirname, "..")}):`, fs.readdirSync(path.resolve(__dirname, "..")));
            try { console.log(`[IAP DEBUG] ls ../../ (${path.resolve(__dirname, "../..")}):`, fs.readdirSync(path.resolve(__dirname, "../.."))); } catch { }
            try { console.log(`[IAP DEBUG] ls CWD (${process.cwd()}):`, fs.readdirSync(process.cwd())); } catch { }
            try { console.log(`[IAP DEBUG] ls /workspace:`, fs.readdirSync("/workspace")); } catch { }
        } catch (e) {
            console.error("[IAP DEBUG] LS failed", e);
        }
        throw new Error("Server configuration error: Google Auth key missing.");
    }

    try {
        const key = require(foundPath);
        const auth = new google.auth.GoogleAuth({
            credentials: {
                client_email: key.client_email,
                private_key: key.private_key,
            },
            scopes: ["https://www.googleapis.com/auth/androidpublisher"],
        });
        googleAuthClient = await auth.getClient();
        return googleAuthClient;
    } catch (e: any) {
        console.error(`[IAP] Failed to load key from ${foundPath}`, e);
        throw new Error("Server configuration error: Google Auth failed loading key.");
    }
}

// Helper: Fetch all premium non-consumable items
async function getPremiumItems() {
    try {
        // Correct structure: appdata/items/{itemId}/itemdata
        // We must iterate collections under appdata/items because collectionGroup("itemdata")
        // would work IF itemdata was a collection, but here itemdata is a DOC inside {itemId} collection.
        // Wait, NO. If {itemId} is a collection, and itemdata is a DOC inside it.
        // collectionGroup queries COLLECTIONS.
        // There is no collection named "itemdata". The collection name is dynamic {itemId}.
        // So we cannot use collectionGroup.

        // We must do listCollections on appdata/items (which is a DOC 'items' in 'appdata' collection?
        // Let's check shop.functions.ts: db.collection("appdata").doc("items").listCollections()

        const itemsRef = db.collection("appdata").doc("items");
        const collections = await itemsRef.listCollections();

        console.log(`[IAP] getPremiumItems scanning ${collections.length} item collections.`);

        const items: { id: string, name: string }[] = [];

        // Optimization: We could use Promise.all but be careful with memory if 1000s of items.
        // Assuming < 100 items for now.
        for (const col of collections) {
            const docSnap = await col.doc("itemdata").get();
            if (!docSnap.exists) continue;

            const data = docSnap.data() || {};
            const premiumPrice = Number(data.itemPremiumPrice ?? 0);
            const isConsumable = !!data.itemIsConsumable; // Check both standard name

            // Criteria: Premium Price > 0 AND Not Consumable
            if (premiumPrice > 0 && !isConsumable) {
                items.push({
                    id: col.id, // The collection ID is the itemId 
                    name: data.itemName || "Unknown Item"
                });
            }
        }

        console.log(`[IAP] getPremiumItems found ${items.length} matches.`);
        return items;
    } catch (e) {
        console.error("getPremiumItems failed", e);
        return [];
    }
}

// Helper to verify Apple Receipt
// Helper to verify Apple Receipt
async function verifyAppleStore(receipt: string): Promise<{
    valid: boolean,
    environment: string,
    status?: number,
    error?: string,
    rawResponse?: any
}> {
    const APPLE_SHARED_SECRET = String(((functionsV1 as any).config?.() || {}).iap?.apple_secret || "");
    const excludeOldTransactions = true;
    let environment = "Production";
    let rawResponse: any = null;

    try {
        // Receipt from Unity is often a base64 encoded string OR a JSON with "Payload" being base64.
        let base64Receipt = receipt;
        try {
            const json = JSON.parse(receipt);
            if (json.Payload) base64Receipt = json.Payload;
            else if (json.payload) base64Receipt = json.payload;
        } catch { }

        // Remove any pure whitespace
        base64Receipt = base64Receipt.trim();

        const body = {
            "receipt-data": base64Receipt,
            "password": APPLE_SHARED_SECRET,
            "exclude-old-transactions": excludeOldTransactions
        };

        // Try Production first
        let response = await fetch("https://buy.itunes.apple.com/verifyReceipt", {
            method: "POST",
            headers: {"Content-Type": "application/json"},
            body: JSON.stringify(body)
        });
        let data: any = await response.json();
        rawResponse = data;

        // If status 21007, try Sandbox
        if (data.status === 21007) {
            environment = "Sandbox";
            response = await fetch("https://sandbox.itunes.apple.com/verifyReceipt", {
                method: "POST",
                headers: {"Content-Type": "application/json"},
                body: JSON.stringify(body)
            });
            data = await response.json();
            rawResponse = data;
        }

        if (data.status === 0) {
            return {valid: true, environment, status: 0, rawResponse};
        }

        console.warn("[IAP] Apple verification failed. Status:", data.status);
        return {valid: false, environment, status: data.status, error: "Apple Status " + data.status, rawResponse};

    } catch (e: any) {
        console.error("[IAP] Apple verification exception", e);
        return {valid: false, environment: "Unknown", error: e.message, rawResponse};
    }
}

// Helper to verify Google Play receipt
// Helper to verify Google Play receipt
async function verifyGooglePlay(productId: string, receiptJson: string): Promise<{
    valid: boolean,
    error?: string,
    rawResponse?: any
}> {
    let rawResponse: any = {};
    try {
        const {google} = require("googleapis");
        // Parse Unity receipt
        const receiptObj = JSON.parse(receiptJson);

        let payloadStr = receiptObj.Payload || receiptObj.payload;
        if (!payloadStr) {
            console.warn("[IAP] Google receipt missing Payload field", receiptObj);
            return {valid: false, error: "Missing Payload field"};
        }

        const payload = JSON.parse(payloadStr);
        const innerJson = JSON.parse(payload.json);
        const token = innerJson.purchaseToken;
        const packageName = innerJson.packageName;
        const sku = innerJson.productId;

        if (!token || !packageName || !sku) {
            console.error("[IAP] Malformed Google receipt payload", innerJson);
            return {valid: false, error: "Malformed payload"};
        }

        // Call Google API
        const auth = await getGoogleAuth();
        const androidPublisher = google.androidpublisher({version: "v3", auth});

        // ---------------- Helpers ----------------
        const checkProduct = async () => {
            try {
                const res = await androidPublisher.purchases.products.get({
                    packageName,
                    productId: sku,
                    token,
                });
                rawResponse.product = res.data;
                if (res.data.purchaseState === 0) return true;
                console.warn(`[IAP] Product found but purchaseState is ${res.data.purchaseState}`);
            } catch (e: any) {
                // Not a product or error
            }
            return false;
        };

        const checkSubscription = async () => {
            // Try v1
            try {
                const res = await androidPublisher.purchases.subscriptions.get({
                    packageName,
                    subscriptionId: sku,
                    token,
                });
                rawResponse.subscription = res.data;
                if (res.data.expiryTimeMillis) {
                    const expiry = Number(res.data.expiryTimeMillis);
                    if (expiry > Date.now()) return true;
                }
            } catch (e: any) {
                // Try v2 fallback
                try {
                    const res = await androidPublisher.purchases.subscriptionsv2.get({
                        packageName,
                        token,
                    });
                    rawResponse.subscriptionV2 = res.data;
                    if (res.data.subscriptionState === "SUBSCRIPTION_STATE_ACTIVE" || res.data.subscriptionState === 1) {
                        return true;
                    }
                } catch (e2: any) { }
            }
            return false;
        };
        // ------------------------------------------

        const isLikelySubscription = sku.toLowerCase().includes("pass") || sku.toLowerCase().includes("sub");

        if (isLikelySubscription) {
            console.log(`[IAP] Heuristic: '${sku}' looks like a subscription. Checking Subs API first.`);
            if (await checkSubscription()) return {valid: true, rawResponse};
            // Fallback
            if (await checkProduct()) return {valid: true, rawResponse};
        } else {
            if (await checkProduct()) return {valid: true, rawResponse};
            // Fallback
            if (await checkSubscription()) return {valid: true, rawResponse};
        }

        console.warn("[IAP] Google verification failed (checked both Product and Subscription APIs).");
        return {valid: false, error: "Google validation failed", rawResponse};

    } catch (e: any) {
        console.error("[IAP] Google verification exception", e);
        return {valid: false, error: e.message, rawResponse: {exception: e.message}};
    }
}

// ---------------- Elite Pass ----------------
export const purchaseElitePass = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const purchaseId = (req.data?.purchaseId ?? "").toString().trim();
    const now = Timestamp.now();
    const userRef = db.collection("users").doc(uid);

    // idempotency
    if (purchaseId) {
        const pRef = userRef.collection("elitePassPurchases").doc(purchaseId);
        const pSnap = await pRef.get();
        if (pSnap.exists) {
            const uSnap = await userRef.get();
            const exp = uSnap.exists
                ? (uSnap.get("elitePassExpiresAt") as Timestamp | null)
                : null;
            const active = !!exp && exp.toMillis() > now.toMillis();
            return {
                active,
                expiresAt: exp?.toDate().toISOString() ?? null,
            };
        }
    }

    await db.runTransaction(async (tx) => {
        const uSnap = await tx.get(userRef);
        const existing: Timestamp | null = uSnap.exists
            ? (uSnap.get("elitePassExpiresAt") as Timestamp | null)
            : null;

        const baseMillis =
            existing && existing.toMillis() > now.toMillis()
                ? existing.toMillis()
                : now.toMillis();

        const thirtyMs = 30 * 24 * 60 * 60 * 1000;
        const newExpiry = Timestamp.fromMillis(baseMillis + thirtyMs);

        tx.set(
            userRef,
            {
                hasElitePass: true,
                elitePassExpiresAt: newExpiry,
                updatedAt: FieldValue.serverTimestamp()
            },
            {merge: true}
        );

        if (purchaseId) {
            tx.set(
                userRef.collection("elitePassPurchases").doc(purchaseId),
                {processedAt: now, newExpiry},
                {merge: true}
            );
        }
    });

    const final = await userRef.get();
    const exp = final.get("elitePassExpiresAt") as Timestamp | null;
    const active = !!exp && exp.toMillis() > now.toMillis();
    return {
        active,
        expiresAt: exp?.toDate().toISOString() ?? null,
    };
});

export const checkElitePass = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const now = Timestamp.now();
    const snap = await db.collection("users").doc(uid).get();
    if (!snap.exists) return {
        active: false,
        expiresAt: null,
    };

    const exp = snap.get("elitePassExpiresAt") as Timestamp | null;
    const active = !!exp && exp.toMillis() > now.toMillis();
    return {
        active,
        expiresAt: exp?.toDate().toISOString() ?? null,
    };
});

export const verifyPurchase = onCall(async (req) => {
    const uid = req.auth?.uid;
    const email = req.auth?.token?.email || null;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const productId = req.data?.productId;
    const receipt = req.data?.receipt; // This is the full receipt string from Unity
    const deviceId = req.data?.deviceId || "unknown";
    const platformRaw = req.data?.platform || "unknown";

    if (!productId || !receipt) {
        throw new HttpsError("invalid-argument", "Missing productId or receipt");
    }

    console.log(`[verifyPurchase] Start for ${uid} - ${productId}`);

    // Parse Receipt basics for logging
    let transactionId = "unknown";
    let store = "unknown";
    try {
        const r = JSON.parse(receipt);
        store = r.Store || "unknown";
        transactionId = r.TransactionID || "unknown";
        // Try to dig deeper if specific store
        if (r.Payload) {
            const p = JSON.parse(r.Payload);
            if (store === "GooglePlay") {
                const json = JSON.parse(p.json);
                if (json.orderId) transactionId = json.orderId;
            }
        }
    } catch { }

    // 1. Detect Store
    let isGoogle = false;
    let isApple = false;

    // Simple heuristic or parsing
    if (receipt.includes("GooglePlay") || store === "GooglePlay") isGoogle = true;
    else if (receipt.includes("AppleAppStore") || store === "AppleAppStore" || store === "MacAppStore") isApple = true;

    // 2. Verify
    let valid = false;
    let verificationError = null;
    let verificationDetails: any = null;

    if (isGoogle) {
        const res = await verifyGooglePlay(productId, receipt);
        valid = res.valid;
        verificationError = res.error;
        verificationDetails = res.rawResponse;
    } else if (isApple) {
        const res = await verifyAppleStore(receipt);
        valid = res.valid;
        verificationError = res.error;
        verificationDetails = res.rawResponse;
    } else {
        // Unknown store (Editor? FakeStore?)
        console.warn("[IAP] Unknown store in receipt", receipt);
        // If testing in Editor (fake store)
        try {
            const r = JSON.parse(receipt);
            if (r.Store === "fake" || r.Store === "FakeStore") {
                console.log("[IAP] FakeStore detected, auto-verifying for testing.");
                valid = true;
                store = "FakeStore";
            }
        } catch { }

        if (!valid) {
            verificationError = "Unknown store";
        }
    }

    // 3. Log to Firestore (users/{uid}/iaptransactions)
    const logData = {
        uid,
        email,
        deviceId,
        platform: platformRaw,
        productId, // invoice name/sku
        transactionId,
        store,
        receipt,
        timestamp: FieldValue.serverTimestamp(),
        verified: valid,
        verificationError: verificationError || null,
        verificationDetails: verificationDetails || null,
        clientTime: Timestamp.now(),
        method: "verifyPurchase"
    };

    try {
        await db.collection("users").doc(uid).collection("iaptransactions").add(logData);
    } catch (e) {
        console.error("[IAP] Failed to log transaction", e);
    }

    if (!valid) {
        throw new HttpsError("permission-denied", "Receipt verification failed: " + (verificationError || "unknown"));
    }

    // 3. Grant Rewards (Idempotent)
    const result = {success: true, message: "Verified", rewards: {} as any};
    const userRef = db.collection("users").doc(uid);

    // MAPPING
    // Consumables
    if (productId.includes("diamond")) {
        let amount = 0;
        if (productId.endsWith("diamond5")) amount = 5;
        else if (productId.endsWith("diamond10")) amount = 10;
        else if (productId.endsWith("diamond25")) amount = 25;
        else if (productId.endsWith("diamond60")) amount = 60;
        else if (productId.endsWith("diamond150")) amount = 150;
        else if (productId.endsWith("diamond400")) amount = 400;
        else if (productId.endsWith("diamond1000")) amount = 1000;

        if (amount > 0) {
            await userRef.update({
                premiumCurrency: FieldValue.increment(amount),
                updatedAt: FieldValue.serverTimestamp()
            });
            result.rewards["diamonds"] = amount;
        }
    }
    // Subscriptions
    else if (productId.includes("elitepass")) {
        // Logic similar to purchaseElitePass but just refreshing expiry
        const now = Timestamp.now();

        let days = 30; // Default monthly
        if (productId.includes("annual")) {
            days = 365;
        }
        const durationMs = days * 24 * 60 * 60 * 1000;

        // Subscription: Grant Benefits
        // 1. Fetch Premium Items to Grant
        const premiumItems = await getPremiumItems();

        await db.runTransaction(async (tx) => {
            // 1. READS (All reads must happen before any writes)
            const snap = await tx.get(userRef);

            // Read status of all premium items to see if we need to grant them
            const invChecks: { ref: any, exists: boolean, owned: boolean, item: any }[] = [];
            for (const item of premiumItems) {
                const invRef = userRef.collection("inventory").doc(item.id);
                const invSnap = await tx.get(invRef);
                invChecks.push({
                    ref: invRef,
                    exists: invSnap.exists,
                    owned: invSnap.exists && invSnap.get("owned") === true,
                    item: item
                });
            }

            // 2. LOGIC
            const currentExp = snap.exists ? (snap.get("elitePassExpiresAt") as Timestamp) : null;
            let baseTime = now.toMillis();
            if (currentExp && currentExp.toMillis() > baseTime) {
                baseTime = currentExp.toMillis();
            }
            const newExp = Timestamp.fromMillis(baseTime + durationMs);
            const curPremium = Number(snap.get("premiumCurrency") ?? 0) || 0;
            const currentEnergy = Number(snap.get("energyCurrent") ?? 0);

            let newCurrent = currentEnergy + 5;
            if (newCurrent > 10) newCurrent = 10;

            // 3. WRITES
            // Update User
            tx.set(userRef, {
                hasElitePass: true,
                elitePassExpiresAt: newExp,
                premiumCurrency: curPremium + 400, // Grant 400
                energyMax: 10, // Set Max to 10
                energyCurrent: newCurrent,
                updatedAt: FieldValue.serverTimestamp()
            }, {merge: true});

            // Grant Items
            console.log(`[IAP] Granting ${premiumItems.length} premium items.`);
            for (const check of invChecks) {
                if (!check.owned) {
                    console.log(`[IAP] Granting item: ${check.item.id} (${check.item.name})`);
                    tx.set(check.ref, {
                        owned: true,
                        source: "elite_pass",
                        acquiredAt: FieldValue.serverTimestamp(),
                        itemIsConsumable: false
                    }, {merge: true});
                } else {
                    console.log(`[IAP] Item ${check.item.id} already owned, skipping grant.`);
                }
            }
        });
        result.message = "Elite Pass Extended: 400 Gems Added, Energy Max 10, Items Unlocked";
        result.rewards["premiumCurrency"] = 400;
    }
    // Non-Consumables
    else if (productId.includes("removeads")) {
        console.log(`[verifyPurchase] Granting Remove Ads for ${uid} (Product: ${productId})`);

        // Use set({merge:true}) instead of update() to be safe against missing docs
        // Also explicitly set removeAds to true
        await userRef.set({
            removeAds: true,
            updatedAt: FieldValue.serverTimestamp()
        }, {merge: true});

        result.message = "Ads Removed";
    }

    return result;
});
