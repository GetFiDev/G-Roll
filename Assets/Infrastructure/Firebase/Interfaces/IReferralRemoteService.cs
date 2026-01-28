using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Referral remote service interface.
    /// Firebase Cloud Functions ile iletisim saglar.
    /// </summary>
    public interface IReferralRemoteService
    {
        /// <summary>
        /// Referral listesini getir
        /// </summary>
        UniTask<ReferralListResponse> GetReferralsAsync(int limit);

        /// <summary>
        /// Bekleyen kazanclari claim et
        /// </summary>
        UniTask<ClaimEarningsResponse> ClaimEarningsAsync();

        /// <summary>
        /// Referral key getir veya olustur
        /// </summary>
        UniTask<ReferralKeyResponse> GetOrCreateReferralKeyAsync();

        /// <summary>
        /// Referral kodunu uygula
        /// </summary>
        UniTask<ApplyReferralResponse> ApplyReferralCodeAsync(string referralCode);
    }

    #region Response Types

    public struct ReferralListResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ReferralEntryData[] Referrals { get; set; }
        public int TotalCount { get; set; }
        public decimal PendingTotal { get; set; }
        public string MyReferralKey { get; set; }
    }

    public struct ReferralEntryData
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public decimal EarnedTotal { get; set; }
        public long JoinedAtTimestamp { get; set; }
    }

    public struct ClaimEarningsResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public decimal ClaimedAmount { get; set; }
        public decimal NewBalance { get; set; }
    }

    public struct ReferralKeyResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string ReferralKey { get; set; }
        public bool IsNewlyCreated { get; set; }
    }

    public struct ApplyReferralResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    #endregion
}
