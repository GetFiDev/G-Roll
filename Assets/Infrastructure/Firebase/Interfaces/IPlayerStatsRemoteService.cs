using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Player stats remote service interface.
    /// Firebase Cloud Functions ile iletisim saglar.
    /// </summary>
    public interface IPlayerStatsRemoteService
    {
        /// <summary>
        /// Player stats'i getir
        /// </summary>
        UniTask<PlayerStatsResponse> GetStatsAsync();

        /// <summary>
        /// Tek stat guncelle
        /// </summary>
        UniTask<UpdateStatResponse> UpdateStatAsync(string statKey, int value);

        /// <summary>
        /// Oyun sonu stats kaydet
        /// </summary>
        UniTask<RecordGameResponse> RecordGameEndAsync(GameEndStats stats);
    }

    #region Response Types

    public struct PlayerStatsResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public PlayerStatsData Stats { get; set; }
    }

    public struct PlayerStatsData
    {
        public int TotalGamesPlayed { get; set; }
        public int TotalScore { get; set; }
        public int HighScore { get; set; }
        public int TotalCoinsEarned { get; set; }
        public int TotalDistance { get; set; }
        public int TotalDeaths { get; set; }
        public int TotalPlayTimeSeconds { get; set; }
        public long LastPlayedAt { get; set; }
    }

    public struct UpdateStatResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int NewValue { get; set; }
    }

    public struct RecordGameResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsNewHighScore { get; set; }
        public int NewHighScore { get; set; }
        public int PreviousHighScore { get; set; }
        public PlayerStatsData UpdatedStats { get; set; }
    }

    #endregion
}
