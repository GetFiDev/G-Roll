using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Player istatistikleri servisi.
    /// Oyun istatistiklerini yonetir ve server ile sync eder.
    /// Eski LegacyPlayerStatsService'in yerini alir.
    /// </summary>
    public interface IPlayerStatsService
    {
        #region State Properties

        /// <summary>Mevcut player stats</summary>
        PlayerStats CurrentStats { get; }

        /// <summary>Stats yuklu mu?</summary>
        bool IsLoaded { get; }

        #endregion

        #region Events

        /// <summary>Stats guncellendikten sonra</summary>
        event Action<PlayerStats> OnStatsUpdated;

        /// <summary>Yeni high score kazanildiginda</summary>
        event Action<int, int> OnNewHighScore; // previous, new

        #endregion

        #region Methods

        /// <summary>
        /// Stats'i server'dan yenile
        /// </summary>
        UniTask<OperationResult<PlayerStats>> RefreshAsync();

        /// <summary>
        /// Tek bir stat'i guncelle
        /// </summary>
        UniTask<OperationResult> UpdateStatAsync(string statKey, int value);

        /// <summary>
        /// Oyun sonunda topluca stats guncelle
        /// </summary>
        UniTask<OperationResult> RecordGameEndAsync(GameEndStats gameStats);

        /// <summary>
        /// Local cache'i temizle
        /// </summary>
        void ClearCache();

        #endregion
    }

    /// <summary>
    /// Player istatistikleri
    /// </summary>
    public class PlayerStats
    {
        /// <summary>Toplam oynanan oyun sayisi</summary>
        public int TotalGamesPlayed { get; set; }

        /// <summary>Toplam skor</summary>
        public int TotalScore { get; set; }

        /// <summary>En yuksek skor</summary>
        public int HighScore { get; set; }

        /// <summary>Toplam kazanilan coin</summary>
        public int TotalCoinsEarned { get; set; }

        /// <summary>Toplam kat edilen mesafe</summary>
        public int TotalDistance { get; set; }

        /// <summary>Toplam olum sayisi</summary>
        public int TotalDeaths { get; set; }

        /// <summary>Toplam oynama suresi (saniye)</summary>
        public int TotalPlayTimeSeconds { get; set; }

        /// <summary>Son oynama zamani (Unix timestamp)</summary>
        public long LastPlayedAt { get; set; }

        /// <summary>Toplam oynama suresi (TimeSpan)</summary>
        public TimeSpan TotalPlayTime => TimeSpan.FromSeconds(TotalPlayTimeSeconds);

        /// <summary>Ortalama skor</summary>
        public float AverageScore => TotalGamesPlayed > 0 ? (float)TotalScore / TotalGamesPlayed : 0;

        /// <summary>Son oynama tarihi</summary>
        public DateTime LastPlayedDate => DateTimeOffset.FromUnixTimeSeconds(LastPlayedAt).LocalDateTime;
    }

    /// <summary>
    /// Oyun sonu istatistikleri
    /// </summary>
    public class GameEndStats
    {
        /// <summary>Elde edilen skor</summary>
        public int Score { get; set; }

        /// <summary>Toplanan coinler</summary>
        public int CoinsCollected { get; set; }

        /// <summary>Kat edilen mesafe</summary>
        public int Distance { get; set; }

        /// <summary>Oyun suresi (saniye)</summary>
        public int DurationSeconds { get; set; }

        /// <summary>Basarili mi bitti?</summary>
        public bool WasSuccessful { get; set; }

        /// <summary>Olum sayisi (bu oyunda)</summary>
        public int DeathCount { get; set; }
    }
}
