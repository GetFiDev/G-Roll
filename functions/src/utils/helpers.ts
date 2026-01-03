import {Timestamp} from "@google-cloud/firestore";

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
