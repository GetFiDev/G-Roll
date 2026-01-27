using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Leaderboard yönetimi için service interface.
    /// Skor gönderme ve sıralama sorgulama işlemlerini yönetir.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// En yüksek skorlu kullanıcıları döndürür.
        /// </summary>
        /// <param name="count">Kaç sonuç döndürülsün</param>
        UniTask<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(int count);

        /// <summary>
        /// Kullanıcının etrafındaki sıralamayı döndürür.
        /// </summary>
        /// <param name="userId">Kullanıcı ID'si</param>
        /// <param name="range">Üst ve alt kaç kullanıcı gösterilsin</param>
        UniTask<IReadOnlyList<LeaderboardEntry>> GetNearbyEntriesAsync(string userId, int range = 5);

        /// <summary>
        /// Belirtilen kullanıcının leaderboard kaydını döndürür.
        /// </summary>
        UniTask<LeaderboardEntry> GetUserEntryAsync(string userId);

        /// <summary>
        /// Skor optimistic olarak gönderir.
        /// </summary>
        UniTask<OperationResult> SubmitScoreOptimisticAsync(int score);

        /// <summary>
        /// Leaderboard güncellendiğinde tetiklenen event.
        /// </summary>
        event Action OnLeaderboardUpdated;
    }

    /// <summary>
    /// Leaderboard kaydı
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
    }
}
