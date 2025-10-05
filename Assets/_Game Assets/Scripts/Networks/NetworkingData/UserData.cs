using Firebase.Firestore;

namespace NetworkingData
{
    [FirestoreData]
    public class UserData
    {
        [FirestoreProperty] public string mail { get; set; } = "";
        [FirestoreProperty] public string username { get; set; } = "";
        [FirestoreProperty] public double currency { get; set; } = 0;

        // Yeni eklenenler:
        [FirestoreProperty] public string statsJson { get; set; } = "";   // string
        [FirestoreProperty] public int streak { get; set; } = 0;          // int
        [FirestoreProperty] public int trustFactor { get; set; } = 100;   // int
        [FirestoreProperty] public int rank { get; set; } = 0;            // int
        [FirestoreProperty] public double maxScore { get; set; } = 0;     // number
        [FirestoreProperty] public string referralKey { get; set; } = ""; // string
        [FirestoreProperty] public int referrals { get; set; } = 0;  //  int


        [FirestoreProperty] public bool hasElitePass { get; set; } = false;

        // Timestamp alanları:
        [FirestoreProperty] public Timestamp elitePassExpiresAt { get; set; }
        [FirestoreProperty] public Timestamp lastLogin { get; set; }

        // ServerTimestamp alanları (sunucu dolduruyor)
        [FirestoreProperty, ServerTimestamp] public Timestamp createdAt { get; set; }
        [FirestoreProperty, ServerTimestamp] public Timestamp updatedAt { get; set; }

        // (Varsa referral alanları)
        [FirestoreProperty] public string referredByUid { get; set; }
        [FirestoreProperty] public string referredByKey { get; set; }
        [FirestoreProperty] public Timestamp referralAppliedAt { get; set; }
    }
}