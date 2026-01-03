export const DB_ID = "getfi";
export const SEASON = "current";
export const RANK_BATCH = 500; // how many users to rank per page
export const RANK_LOCK_DOC = `leaderboards/${SEASON}/meta/rank_job`;

// ========================= Achievements =========================
// Types: map to server-side progress sources on users/{uid}
export const ACH_TYPES = {
    ENDLESS_ROLLER: "endless_roller",        // sessionsPlayed
    SCORE_CHAMPION: "score_champion",         // maxScore
    TOKEN_HUNTER: "token_hunter",           // cumulativeCurrencyEarned
    COMBO_GOD: "combo_god",              // maxCombo
    MARKET_WHISPER: "market_whisperer",       // itemsPurchasedCount
    TIME_DRIFTER: "time_drifter",           // totalPlaytimeMinutes
    HABIT_MAKER: "habit_maker",            // streak (daily login)
    POWERUP_EXP: "powerup_explorer",       // powerUpsCollected
    SIGNAL_BOOST: "signal_booster",         // referrals
} as const;

export type AchType = typeof ACH_TYPES[keyof typeof ACH_TYPES];

export type AchLevel = { threshold: number; rewardGet: number };

export type AchDoc = {
    levels: AchLevel[]; // length=5 expected
    displayName?: string;
    description?: string;
    iconUrl?: string;
    order?: number;
};

// ---------------- Referral ----------------
export const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
