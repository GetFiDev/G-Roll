using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Leaderboard yonetimi icin service interface.
    /// Skor gonderme, siralama sorgulama ve season desteigi saglar.
    /// Eski LeaderboardManager'in yerini alir.
    /// </summary>
    public interface ILeaderboardService
    {
        #region Properties

        /// <summary>Secili leaderboard tipi</summary>
        LeaderboardType CurrentType { get; }

        /// <summary>Cache yuklu mu?</summary>
        bool IsCacheLoaded { get; }

        /// <summary>Cached top entries</summary>
        IReadOnlyList<LeaderboardEntry> TopCached { get; }

        #endregion

        #region Season Properties

        /// <summary>Season aktif mi?</summary>
        bool IsSeasonActive { get; }

        /// <summary>Aktif season adi</summary>
        string ActiveSeasonName { get; }

        /// <summary>Aktif season aciklamasi</summary>
        string ActiveSeasonDescription { get; }

        /// <summary>Sonraki season baslangic tarihi</summary>
        DateTime? NextSeasonStartDate { get; }

        #endregion

        #region Current User Properties

        /// <summary>Kullanicinin sirasi</summary>
        int MyRank { get; }

        /// <summary>Kullanicinin skoru</summary>
        int MyScore { get; }

        /// <summary>Kullanici elite mi?</summary>
        bool MyIsElite { get; }

        #endregion

        #region Events

        /// <summary>Leaderboard guncellendikten sonra</summary>
        event Action OnLeaderboardUpdated;

        /// <summary>Cache guncellendikten sonra</summary>
        event Action OnCacheUpdated;

        #endregion

        #region Methods

        /// <summary>
        /// Leaderboard tipini degistir
        /// </summary>
        void SetLeaderboardType(LeaderboardType type);

        /// <summary>
        /// Cache'i yenile
        /// </summary>
        UniTask<OperationResult> RefreshAsync();

        /// <summary>
        /// En yuksek skorlu kullanicilari dondurur.
        /// </summary>
        UniTask<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(int count);

        /// <summary>
        /// Kullanicinin etrafindaki siralamayi dondurur.
        /// </summary>
        UniTask<IReadOnlyList<LeaderboardEntry>> GetNearbyEntriesAsync(string userId, int range = 5);

        /// <summary>
        /// Belirtilen kullanicinin leaderboard kaydini dondurur.
        /// </summary>
        UniTask<LeaderboardEntry> GetUserEntryAsync(string userId);

        /// <summary>
        /// Skor optimistic olarak gonderir.
        /// </summary>
        UniTask<OperationResult> SubmitScoreOptimisticAsync(int score);

        #endregion
    }

    /// <summary>
    /// Leaderboard kaydi
    /// </summary>
    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public int Score { get; set; }
        public long Timestamp { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool HasElitePass { get; set; }
    }

    /// <summary>
    /// Season bilgisi
    /// </summary>
    public class SeasonInfo
    {
        public bool IsActive { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? NextSeasonStartDate { get; set; }
    }
}
