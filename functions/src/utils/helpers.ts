import {Timestamp} from "@google-cloud/firestore";
import {db} from "../firebase";

// Helper: Get Active Season ID
export async function getActiveSeasonId(): Promise<string | null> {
    const now = Timestamp.now();
    try {
        console.log(`[getActiveSeasonId] DB_ID=${db.databaseId}, Now=${now.toDate().toISOString()}`);

        // Strategy: Filter by endDate ONLY to avoid composite index requirement.
        // Then filter startDate in memory.
        const snap = await db.collection("seasons")
            .where("endDate", ">=", now)
            .orderBy("endDate")
            .limit(5) // Fetch a few candidates (future seasons)
            .get();

        console.log(`[getActiveSeasonId] Found ${snap.size} candidates (not ended)`);

        for (const doc of snap.docs) {
            const data = doc.data();
            const start = data.startDate as Timestamp;
            // Check if it has started
            if (start && start.toMillis() <= now.toMillis()) {
                console.log(`[getActiveSeasonId] Active Season Found: ${doc.id}`);
                return doc.id;
            }
        }
        console.log("[getActiveSeasonId] No active season found in candidates.");
    } catch (e) {
        console.error("Error fetching active season:", e);
    }
    return null;
}

// --- ID Normalization Helper (use everywhere for itemId) ---
export const normId = (s?: string) => (s || "").trim().toLowerCase();

// UTC date helper (YYYY-MM-DD)
export function utcDateString(ts: Timestamp): string {
    return new Date(ts.toMillis()).toISOString().slice(0, 10);
}

// small numeric helper for safe casting
export function snapNum(v: any): number {
    const n = Number(v);
    return Number.isFinite(n) ? n : 0;
}

// ---- Stats helpers for equip/unequip merging ----
export function parseStatsJson(s: any): Record<string, number> {
    if (typeof s !== "string") return {};
    try {
        const o = JSON.parse(s);
        if (o && typeof o === "object") return o as Record<string, number>;
        return {};
    } catch {
        return {};
    }
}

export function mergeStats(base: Record<string, number>, delta: Record<string, number>, sign: 1 | -1) {
    const out: Record<string, number> = {...base};
    for (const k of Object.keys(delta)) {
        const v = Number(delta[k]);
        if (!Number.isFinite(v)) continue;
        const cur = Number(out[k] ?? 0);
        out[k] = cur + sign * v;
    }
    return out;
}

export function extractItemStats(raw: Record<string, any>): Record<string, number> {
    const out: Record<string, number> = {};
    for (const [k, v] of Object.entries(raw || {})) {
        if (k.startsWith("itemstat_")) {
            const statKey = k.replace("itemstat_", "");
            out[statKey] = Number(v) || 0;
        }
    }
    return out;
}

export function randomReferralKey(len = 12, alphabet: string): string {
    let s = "";
    for (let i = 0; i < len; i++) {
        s += alphabet[Math.floor(Math.random() * alphabet.length)];
    }
    return s;
}
