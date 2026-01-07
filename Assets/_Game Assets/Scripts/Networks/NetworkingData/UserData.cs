using Firebase.Firestore;

namespace NetworkingData
{
    [FirestoreData]
    public class UserData
    {
        [FirestoreProperty] public string mail { get; set; } = "";
        [FirestoreProperty] public string username { get; set; } = "";
        [FirestoreProperty] public double currency { get; set; } = 0;
        [FirestoreProperty] public double premiumCurrency { get; set; } = 0; // New premium currency

        // Yeni eklenenler:
        [FirestoreProperty] public string statsJson { get; set; } = "";   // string
        [FirestoreProperty] public string adClaimsJson { get; set; } = ""; // JSON for daily ad limits: { "productId": { "lastClaimDate": "2023-10-27", "count": 2 } }
        [FirestoreProperty] public int streak { get; set; } = 0;          // int
        [FirestoreProperty] public int trustFactor { get; set; } = 100;   // int
        [FirestoreProperty] public int rank { get; set; } = 0;            // int
        [FirestoreProperty] public double maxScore { get; set; } = 0;     // number
        [FirestoreProperty] public string referralKey { get; set; } = ""; // string
        [FirestoreProperty("referralCount")] public int referrals { get; set; } = 0;  //  int
        [FirestoreProperty] public int chapterProgress { get; set; } = 1;

        // Energy system (added to match Firestore document)
        [FirestoreProperty] public int energyCurrent { get; set; } = 0;          // current energy
        [FirestoreProperty] public int energyMax { get; set; } = 6;              // max energy
        [FirestoreProperty] public int energyRegenPeriodSec { get; set; } = 14400; // 4 hours in seconds
        [FirestoreProperty, ServerTimestamp] public Timestamp energyUpdatedAt { get; set; } // last server update


        [FirestoreProperty] public bool hasElitePass { get; set; } = false;
        [FirestoreProperty("removeAds")] public bool removeAds { get; set; } = false;

        // Timestamp alanları:
        [FirestoreProperty] public Timestamp elitePassExpiresAt { get; set; }
        [FirestoreProperty] public Timestamp lastLogin { get; set; }
        [FirestoreProperty] public Timestamp usernameLastChangedAt { get; set; }

        // ServerTimestamp alanları (sunucu dolduruyor)
        [FirestoreProperty, ServerTimestamp] public Timestamp createdAt { get; set; }
        [FirestoreProperty, ServerTimestamp] public Timestamp updatedAt { get; set; }

        // (Varsa referral alanları)
        [FirestoreProperty] public string referredByUid { get; set; }
        [FirestoreProperty] public string referredByKey { get; set; }

        [FirestoreProperty] public object referralAppliedAt { get; set; } // object to allow nulls
        public Timestamp ReferralAppliedAtTimestamp => referralAppliedAt is Timestamp t ? t : Timestamp.FromDateTime(System.DateTime.MinValue);

        [FirestoreProperty] public double totalPlaytimeMinutesFloat { get; set; }
        [FirestoreProperty] public string lastLoginLocalDate { get; set; }
        [FirestoreProperty] public int sessionsPlayed { get; set; }
        [FirestoreProperty] public double cumulativeCurrencyEarned { get; set; }
        [FirestoreProperty] public double maxCombo { get; set; }
        [FirestoreProperty] public int bestStreak { get; set; }
        [FirestoreProperty] public int powerUpsCollected { get; set; }
        [FirestoreProperty] public int itemsPurchasedCount { get; set; }
        [FirestoreProperty] public int totalPlaytimeMinutes { get; set; }
        [FirestoreProperty] public double totalPlaytimeSec { get; set; }
        [FirestoreProperty] public int tzOffsetMinutes { get; set; }

        // Missing fields added to suppress warnings and ensure data layout
        [FirestoreProperty] public string uid { get; set; } = "";
        [FirestoreProperty] public string email { get; set; } = ""; // Often same as mail, but sometimes SDK requests explicit field
        [FirestoreProperty] public string photoUrl { get; set; } = "";
        [FirestoreProperty] public int level { get; set; } = 1;
        [FirestoreProperty] public bool isProfileComplete { get; set; } = false;
        [FirestoreProperty] public double referralEarnings { get; set; } = 0; // The pool of unclaimed referral earnings
        [FirestoreProperty] public System.Collections.Generic.Dictionary<string, int> seasonalMaxScores { get; set; } = new System.Collections.Generic.Dictionary<string, int>(); // seasonId -> maxScore
    }
}