using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Referral sistemi servisi.
    /// Referral kodlari, kazanclar ve referral listesi yonetimini saglar.
    /// Eski ReferralManager'in yerini alir.
    /// </summary>
    public interface IReferralService
    {
        #region State Properties

        /// <summary>Cache yuklu mu?</summary>
        bool IsCacheLoaded { get; }

        /// <summary>Kullanicinin kendi referral kodu</summary>
        string MyReferralKey { get; }

        /// <summary>Toplam referral sayisi</summary>
        int GlobalReferralCount { get; }

        /// <summary>Bekleyen kazanc miktari</summary>
        decimal PendingEarnings { get; }

        /// <summary>Cached referral listesi</summary>
        IReadOnlyList<ReferralEntry> CachedReferrals { get; }

        #endregion

        #region Events

        /// <summary>Cache guncellendikten sonra</summary>
        event Action OnCacheUpdated;

        /// <summary>Kazanc claim edildikten sonra</summary>
        event Action<decimal> OnEarningsClaimed;

        #endregion

        #region Methods

        /// <summary>
        /// Referral cache'ini yenile
        /// </summary>
        UniTask<OperationResult<ReferralCacheResult>> RefreshCacheAsync(int limit = 100);

        /// <summary>
        /// Bekleyen kazanclari claim et
        /// </summary>
        UniTask<OperationResult<decimal>> ClaimEarningsAsync();

        /// <summary>
        /// Referral key olustur veya mevcut olanı getir
        /// </summary>
        UniTask<OperationResult<string>> GetOrCreateReferralKeyAsync();

        /// <summary>
        /// Referral kodunu uygula (yeni kullanici kaydi sirasinda)
        /// </summary>
        UniTask<OperationResult> ApplyReferralCodeAsync(string referralCode);

        #endregion
    }

    /// <summary>
    /// Referral girisi (referral eden kullanici bilgisi)
    /// </summary>
    public class ReferralEntry
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public decimal EarnedTotal { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>
    /// Referral cache sonucu
    /// </summary>
    public class ReferralCacheResult
    {
        public IReadOnlyList<ReferralEntry> Referrals { get; set; }
        public int TotalCount { get; set; }
        public decimal PendingTotal { get; set; }
        public string MyReferralKey { get; set; }
    }
}
