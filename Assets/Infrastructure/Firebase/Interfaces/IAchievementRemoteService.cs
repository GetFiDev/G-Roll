using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Achievement işlemleri için remote service interface.
    /// Firebase Cloud Functions çağrılarını soyutlar.
    /// </summary>
    public interface IAchievementRemoteService
    {
        /// <summary>
        /// Achievement reward'ını claim eder.
        /// </summary>
        UniTask<ClaimAchievementResponse> ClaimAchievementAsync(string achievementId);

        /// <summary>
        /// Server'dan tüm achievement'ları alır.
        /// </summary>
        UniTask<List<Achievement>> FetchAchievementsAsync();
    }

    /// <summary>
    /// Claim achievement response
    /// </summary>
    public struct ClaimAchievementResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static ClaimAchievementResponse Successful()
        {
            return new ClaimAchievementResponse { Success = true };
        }

        public static ClaimAchievementResponse Failed(string error)
        {
            return new ClaimAchievementResponse { Success = false, ErrorMessage = error };
        }
    }
}
