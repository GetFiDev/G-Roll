using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Social
{
    /// <summary>
    /// Leaderboard service implementation.
    /// Skor gönderme ve sıralama sorgulama işlemlerini yönetir.
    /// Optimistic update pattern ile skor gönderimi.
    /// </summary>
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ILeaderboardRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        // Cached data
        private readonly object _cacheLock = new();
        private List<LeaderboardEntry> _cachedTopEntries = new();
        private LeaderboardEntry _cachedUserEntry;
        private int _pendingScore = -1;

        public event Action OnLeaderboardUpdated;

        [Inject]
        public LeaderboardService(
            ILeaderboardRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #region ILeaderboardService Implementation

        public async UniTask<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(int count)
        {
            try
            {
                var response = await _remoteService.GetTopEntriesAsync(count);

                if (response.Success)
                {
                    lock (_cacheLock)
                    {
                        _cachedTopEntries = response.Entries ?? new List<LeaderboardEntry>();
                    }
                    return response.Entries;
                }
                else
                {
                    _logger.LogWarning($"[Leaderboard] GetTopEntries failed: {response.ErrorMessage}");
                    lock (_cacheLock)
                    {
                        return _cachedTopEntries;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Leaderboard] GetTopEntries error: {ex.Message}");
                lock (_cacheLock)
                {
                    return _cachedTopEntries;
                }
            }
        }

        public async UniTask<IReadOnlyList<LeaderboardEntry>> GetNearbyEntriesAsync(string userId, int range = 5)
        {
            try
            {
                var response = await _remoteService.GetNearbyEntriesAsync(userId, range);

                if (response.Success)
                {
                    return response.Entries ?? new List<LeaderboardEntry>();
                }
                else
                {
                    _logger.LogWarning($"[Leaderboard] GetNearbyEntries failed: {response.ErrorMessage}");
                    return new List<LeaderboardEntry>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Leaderboard] GetNearbyEntries error: {ex.Message}");
                return new List<LeaderboardEntry>();
            }
        }

        public async UniTask<LeaderboardEntry> GetUserEntryAsync(string userId)
        {
            try
            {
                var response = await _remoteService.GetUserEntryAsync(userId);

                if (response.Success)
                {
                    lock (_cacheLock)
                    {
                        _cachedUserEntry = response.Entry;
                    }
                    return response.Entry;
                }
                else
                {
                    _logger.LogWarning($"[Leaderboard] GetUserEntry failed: {response.ErrorMessage}");
                    lock (_cacheLock)
                    {
                        return _cachedUserEntry;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Leaderboard] GetUserEntry error: {ex.Message}");
                lock (_cacheLock)
                {
                    return _cachedUserEntry;
                }
            }
        }

        public async UniTask<OperationResult> SubmitScoreOptimisticAsync(int score)
        {
            if (score < 0)
                return OperationResult.ValidationError("Score cannot be negative");

            // 1. Store pending score for optimistic UI
            lock (_cacheLock)
            {
                _pendingScore = score;
            }

            _logger.Log($"[Leaderboard] Submitting score optimistically: {score}");

            // 2. Notify UI immediately (optimistic)
            OnLeaderboardUpdated?.Invoke();

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.SubmitScoreAsync(score);

                if (response.Success)
                {
                    lock (_cacheLock)
                    {
                        _pendingScore = -1;

                        // Update cached user entry if available
                        if (_cachedUserEntry != null)
                        {
                            if (score > _cachedUserEntry.Score)
                            {
                                _cachedUserEntry.Score = score;
                                _cachedUserEntry.Rank = response.NewRank;
                            }
                        }
                    }

                    // Notify UI of confirmed update
                    OnLeaderboardUpdated?.Invoke();
                    _logger.Log($"[Leaderboard] Score submitted. New rank: {response.NewRank}, IsHighScore: {response.IsNewHighScore}");
                    return OperationResult.Success();
                }
                else
                {
                    // ROLLBACK
                    lock (_cacheLock)
                    {
                        _pendingScore = -1;
                    }

                    OnLeaderboardUpdated?.Invoke();
                    PublishRollback("SubmitScore", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // ROLLBACK
                _logger.LogError($"[Leaderboard] SubmitScore error: {ex.Message}");
                lock (_cacheLock)
                {
                    _pendingScore = -1;
                }

                OnLeaderboardUpdated?.Invoke();
                PublishRollback("SubmitScore", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
        }

        #endregion

        #region Private Helpers

        private void PublishRollback(string operationType, string reason, RollbackCategory category)
        {
            var message = new OperationRolledBackMessage(
                operationType,
                reason,
                shouldNotify: true,
                category
            );
            _messageBus.Publish(message);
        }

        #endregion
    }
}
