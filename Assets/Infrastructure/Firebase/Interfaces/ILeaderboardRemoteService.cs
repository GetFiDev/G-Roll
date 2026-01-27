using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Leaderboard remote service interface.
    /// Firebase Functions ile haberleşir.
    /// </summary>
    public interface ILeaderboardRemoteService
    {
        /// <summary>
        /// En yüksek skorlu kullanıcıları getirir.
        /// </summary>
        UniTask<LeaderboardResponse> GetTopEntriesAsync(int count);

        /// <summary>
        /// Kullanıcının etrafındaki sıralamayı getirir.
        /// </summary>
        UniTask<LeaderboardResponse> GetNearbyEntriesAsync(string userId, int range);

        /// <summary>
        /// Belirtilen kullanıcının kaydını getirir.
        /// </summary>
        UniTask<LeaderboardEntryResponse> GetUserEntryAsync(string userId);

        /// <summary>
        /// Skor gönderir.
        /// </summary>
        UniTask<SubmitScoreResponse> SubmitScoreAsync(int score);
    }

    /// <summary>
    /// Leaderboard response
    /// </summary>
    public class LeaderboardResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<LeaderboardEntry> Entries { get; set; }
    }

    /// <summary>
    /// Single entry response
    /// </summary>
    public class LeaderboardEntryResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public LeaderboardEntry Entry { get; set; }
    }

    /// <summary>
    /// Submit score response
    /// </summary>
    public class SubmitScoreResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int NewRank { get; set; }
        public bool IsNewHighScore { get; set; }
    }
}
