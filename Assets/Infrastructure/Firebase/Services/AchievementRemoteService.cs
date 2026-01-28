using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Achievement işlemleri için Firebase Cloud Functions implementasyonu.
    /// </summary>
    public class AchievementRemoteService : IAchievementRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public AchievementRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<ClaimAchievementResponse> ClaimAchievementAsync(string achievementId)
        {
            var result = await _firebase.CallFunctionAsync<ClaimAchievementResponse>(
                "claimAchievement",
                new { achievementId }
            );
            return result;
        }

        public async UniTask<List<Achievement>> FetchAchievementsAsync()
        {
            var result = await _firebase.CallFunctionAsync<List<Achievement>>(
                "getAchievements",
                null
            );
            return result;
        }

        public async UniTask<ClaimAchievementResponse> ClaimAllEligibleLevelsAsync(string achievementId)
        {
            var result = await _firebase.CallFunctionAsync<ClaimAchievementResponse>(
                "claimAllEligibleLevels",
                new { achievementId }
            );
            return result;
        }
    }
}
