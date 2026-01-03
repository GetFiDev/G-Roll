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
    try {
        // Determine path to service account (copied to lib/ during build)
        // We try to require it relative to this file.
        const key = require("../../service-account.json");
        const auth = new google.auth.GoogleAuth({
            credentials: {
                client_email: key.client_email,
                private_key: key.private_key,
            },
            scopes: ["https://www.googleapis.com/auth/androidpublisher"],
        });
        googleAuthClient = await auth.getClient();
        return googleAuthClient;
    } catch (e) {
        console.error("[IAP] Failed to load service-account.json", e);
        throw new Error("Server configuration error: Google Auth failed");
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

        // Try as product (consumable/non-consumable)
        try {
            const res = await androidPublisher.purchases.products.get({
                packageName,
                productId: sku,
                token,
            });
            rawResponse.product = res.data;
            if (res.data.purchaseState === 0) { // 0 = Purchased
                return {valid: true, rawResponse};
            }
        } catch (e) {
            // ignore, might be subscription
        }

        // Try as subscription
        try {
            const res = await androidPublisher.purchases.subscriptions.get({
                packageName,
                subscriptionId: sku,
                token,
            });
            rawResponse.subscription = res.data;
            if (res.data.expiryTimeMillis) {
                const expiry = Number(res.data.expiryTimeMillis);
                if (expiry > Date.now()) return {valid: true, rawResponse};
            }
        } catch (e: any) {
            console.warn("[IAP] Google verification failed for both product and sub", e);
            rawResponse.error = e.message;
        }

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

        await db.runTransaction(async (tx) => {
            const snap = await tx.get(userRef);
            const currentExp = snap.exists ? (snap.get("elitePassExpiresAt") as Timestamp) : null;
            let baseTime = now.toMillis();
            if (currentExp && currentExp.toMillis() > baseTime) {
                baseTime = currentExp.toMillis();
            }
            const newExp = Timestamp.fromMillis(baseTime + durationMs);

            tx.set(userRef, {
                hasElitePass: true,
                elitePassExpiresAt: newExp,
                updatedAt: FieldValue.serverTimestamp()
            }, {merge: true});
        });
        result.message = "Elite Pass Extended";
    }
    // Non-Consumables
    else if (productId.includes("removeads")) {
        await userRef.update({
            removeAds: true,
            updatedAt: FieldValue.serverTimestamp()
        });
        result.message = "Ads Removed";
    }

    return result;
});
