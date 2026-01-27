using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Leaderboard remote service implementation.
    /// Firebase Functions ile haberleşir.
    /// </summary>
    public class LeaderboardRemoteService : ILeaderboardRemoteService
    {
        private readonly IFirebaseGateway _firebaseGateway;
        private readonly IGRollLogger _logger;

        [Inject]
        public LeaderboardRemoteService(
            IFirebaseGateway firebaseGateway,
            IGRollLogger logger)
        {
            _firebaseGateway = firebaseGateway;
            _logger = logger;
        }

        public async UniTask<LeaderboardResponse> GetTopEntriesAsync(int count)
        {
            _logger.Log($"[Leaderboard] Fetching top {count} entries");

            var response = await _firebaseGateway.CallFunctionAsync<LeaderboardResponse>(
                "getTopLeaderboard",
                new { count }
            );

            return response ?? new LeaderboardResponse
            {
                Success = false,
                ErrorMessage = "Failed to fetch leaderboard",
                Entries = new List<LeaderboardEntry>()
            };
        }

        public async UniTask<LeaderboardResponse> GetNearbyEntriesAsync(string userId, int range)
        {
            _logger.Log($"[Leaderboard] Fetching nearby entries for {userId}");

            var response = await _firebaseGateway.CallFunctionAsync<LeaderboardResponse>(
                "getNearbyLeaderboard",
                new { userId, range }
            );

            return response ?? new LeaderboardResponse
            {
                Success = false,
                ErrorMessage = "Failed to fetch nearby entries",
                Entries = new List<LeaderboardEntry>()
            };
        }

        public async UniTask<LeaderboardEntryResponse> GetUserEntryAsync(string userId)
        {
            _logger.Log($"[Leaderboard] Fetching entry for {userId}");

            var response = await _firebaseGateway.CallFunctionAsync<LeaderboardEntryResponse>(
                "getUserLeaderboardEntry",
                new { userId }
            );

            return response ?? new LeaderboardEntryResponse
            {
                Success = false,
                ErrorMessage = "Failed to fetch user entry"
            };
        }

        public async UniTask<SubmitScoreResponse> SubmitScoreAsync(int score)
        {
            _logger.Log($"[Leaderboard] Submitting score: {score}");

            var response = await _firebaseGateway.CallFunctionAsync<SubmitScoreResponse>(
                "submitScore",
                new { score }
            );

            return response ?? new SubmitScoreResponse
            {
                Success = false,
                ErrorMessage = "Failed to submit score"
            };
        }
    }
}
