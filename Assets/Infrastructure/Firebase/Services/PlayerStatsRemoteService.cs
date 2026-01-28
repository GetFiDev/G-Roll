using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Player stats remote service implementasyonu.
    /// Firebase Cloud Functions ile iletisim saglar.
    /// </summary>
    public class PlayerStatsRemoteService : IPlayerStatsRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public PlayerStatsRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<PlayerStatsResponse> GetStatsAsync()
        {
            return await _firebase.CallFunctionAsync<PlayerStatsResponse>("getPlayerStats");
        }

        public async UniTask<UpdateStatResponse> UpdateStatAsync(string statKey, int value)
        {
            return await _firebase.CallFunctionAsync<UpdateStatResponse>(
                "updatePlayerStat",
                new { statKey, value });
        }

        public async UniTask<RecordGameResponse> RecordGameEndAsync(GameEndStats stats)
        {
            return await _firebase.CallFunctionAsync<RecordGameResponse>(
                "recordGameEnd",
                new
                {
                    score = stats.Score,
                    coinsCollected = stats.CoinsCollected,
                    distance = stats.Distance,
                    durationSeconds = stats.DurationSeconds,
                    wasSuccessful = stats.WasSuccessful,
                    deathCount = stats.DeathCount
                });
        }
    }
}
