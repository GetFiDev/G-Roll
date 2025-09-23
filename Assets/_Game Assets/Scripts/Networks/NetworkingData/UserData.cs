using Firebase.Firestore;

namespace NetworkingData
{
    [FirestoreData]
    public class UserData
    {
        [FirestoreProperty] public string mail { get; set; } = "";
        [FirestoreProperty] public string username { get; set; } = "";
        [FirestoreProperty] public float currency { get; set; } = 0f;
        [FirestoreProperty] public Timestamp lastLogin { get; set; } = Timestamp.GetCurrentTimestamp();
        // Elite Pass
        [FirestoreProperty] public bool hasElitePass { get; set; } = false;
        [FirestoreProperty] public Timestamp elitePassExpiresAt { get; set; }
        // Diğerleri
        [FirestoreProperty] public int streak { get; set; } = 0;
        [FirestoreProperty] public int referrals { get; set; } = 0;
        [FirestoreProperty] public int rank { get; set; } = 0;
        [FirestoreProperty] public int score { get; set; } = 0;
    }
}
