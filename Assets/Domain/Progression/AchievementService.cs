using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Domain.Progression.Models;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Progression
{
    /// <summary>
    /// Achievement service implementation.
    /// Optimistic update pattern ile achievement yönetimi yapar.
    /// Full snapshot/rollback desteği ile.
    ///
    /// NOT: Circular dependency önlemek için ICurrencyService'e Lazy injection kullanılır.
    /// Bu sayede AchievementService başlatılırken CurrencyService henüz hazır olmasa bile sorun çıkmaz.
    /// </summary>
    public class AchievementService : IAchievementService, ISnapshotable<AchievementStateSnapshot>, IDisposable
    {
        private readonly AchievementState _state;
        private readonly IAchievementRemoteService _remoteService;
        private readonly Lazy<ICurrencyService> _currencyServiceLazy;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private ICurrencyService CurrencyService => _currencyServiceLazy.Value;

        public event Action<AchievementChangedMessage> OnAchievementChanged;

        [Inject]
        public AchievementService(
            IAchievementRemoteService remoteService,
            Lazy<ICurrencyService> currencyService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _state = new AchievementState();
            _remoteService = remoteService;
            _currencyServiceLazy = currencyService;
            _messageBus = messageBus;
            _logger = logger;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        #region ISnapshotable<AchievementStateSnapshot> Implementation

        /// <summary>
        /// Full achievement state snapshot oluşturur.
        /// </summary>
        public AchievementStateSnapshot CreateSnapshot()
        {
            return new AchievementStateSnapshot(_state.GetAchievementsCopy());
        }

        /// <summary>
        /// Snapshot'tan full state restore eder.
        /// </summary>
        public void RestoreSnapshot(AchievementStateSnapshot snapshot)
        {
            _state.RestoreFromSnapshot(snapshot.Achievements);
            _logger.Log("[Achievement] State restored from snapshot");
        }

        #endregion

        #region IAchievementService Implementation

        public IReadOnlyList<Achievement> GetAllAchievements() => _state.GetAllAchievements();

        public Achievement GetAchievement(string achievementId) => _state.GetAchievement(achievementId);

        public bool IsUnlocked(string achievementId) => _state.IsUnlocked(achievementId);

        public bool IsClaimed(string achievementId) => _state.IsClaimed(achievementId);

        /// <summary>
        /// Optimistic progress update (no server call, batched elsewhere).
        /// </summary>
        public void UpdateProgressOptimistic(string achievementId, int progress)
        {
            var achievement = _state.GetAchievement(achievementId);
            if (achievement == null) return;
            if (achievement.IsUnlocked) return;

            var previousProgress = achievement.CurrentProgress;
            _state.UpdateProgress(achievementId, progress);

            var updatedAchievement = _state.GetAchievement(achievementId);

            // Check for unlock
            if (updatedAchievement.IsUnlocked)
            {
                PublishChange(achievementId, AchievementChangeType.Unlocked, true,
                    updatedAchievement.CurrentProgress, updatedAchievement.TargetProgress);
                _logger.Log($"[Achievement] Unlocked: {achievementId}");
            }
            else
            {
                PublishChange(achievementId, AchievementChangeType.ProgressUpdated, true,
                    updatedAchievement.CurrentProgress, updatedAchievement.TargetProgress);
            }
        }

        public async UniTask<OperationResult> ClaimAchievementOptimisticAsync(string achievementId)
        {
            var achievement = _state.GetAchievement(achievementId);

            // Validation
            if (achievement == null)
                return OperationResult.ValidationError("Achievement not found");

            if (!achievement.IsUnlocked)
                return OperationResult.ValidationError("Achievement not unlocked");

            if (achievement.IsClaimed)
                return OperationResult.ValidationError("Achievement already claimed");

            // 1. DUAL SNAPSHOT - Full achievement state AND currency state BEFORE any changes
            // Bu sayede herhangi bir hata durumunda her ikisi de tutarlı şekilde geri alınabilir
            var achievementSnapshot = CreateSnapshot();
            var currencySnapshot = CurrencyService.CreateSnapshot();
            var rewardGiven = false;

            // 2. OPTIMISTIC UPDATE - Mark achievement as claimed
            _state.MarkClaimed(achievementId);
            PublishChange(achievementId, AchievementChangeType.Claimed, true,
                achievement.CurrentProgress, achievement.TargetProgress);

            _logger.Log($"[Achievement] Optimistic claim: {achievementId}");

            // 3. Give reward optimistically (track if given for rollback decision)
            if (achievement.Reward != null)
            {
                var rewardResult = await CurrencyService.AddCurrencyOptimisticAsync(
                    achievement.Reward.CurrencyType,
                    achievement.Reward.Amount,
                    $"achievement_{achievementId}"
                );
                rewardGiven = rewardResult.IsSuccess;

                // If reward failed to be added, rollback achievement claim and return
                if (!rewardGiven)
                {
                    RestoreSnapshot(achievementSnapshot);
                    PublishChange(achievementId, AchievementChangeType.RolledBack, false,
                        achievement.CurrentProgress, achievement.TargetProgress);
                    PublishRollback("ClaimAchievement", "Failed to add reward currency", RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack("Failed to add reward currency");
                }
            }

            // 4. SERVER REQUEST - Confirm the claim
            try
            {
                var response = await _remoteService.ClaimAchievementAsync(achievementId);

                if (response.Success)
                {
                    PublishChange(achievementId, AchievementChangeType.Claimed, false,
                        achievement.CurrentProgress, achievement.TargetProgress);
                    return OperationResult.Success();
                }
                else
                {
                    // FULL ROLLBACK - Server rejected, restore both achievement and currency to pre-claim state
                    RestoreSnapshot(achievementSnapshot);
                    if (rewardGiven)
                    {
                        CurrencyService.RestoreSnapshot(currencySnapshot);
                    }
                    PublishChange(achievementId, AchievementChangeType.RolledBack, false,
                        achievement.CurrentProgress, achievement.TargetProgress);
                    PublishRollback("ClaimAchievement", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // FULL ROLLBACK - Network error, restore both achievement and currency to pre-claim state
                _logger.LogError($"[Achievement] Claim error: {ex.Message}");
                RestoreSnapshot(achievementSnapshot);
                if (rewardGiven)
                {
                    CurrencyService.RestoreSnapshot(currencySnapshot);
                }
                PublishChange(achievementId, AchievementChangeType.RolledBack, false,
                    achievement.CurrentProgress, achievement.TargetProgress);
                PublishRollback("ClaimAchievement", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
        }

        public async UniTask SyncWithServerAsync()
        {
            try
            {
                var serverAchievements = await _remoteService.FetchAchievementsAsync();
                _state.SetAchievements(serverAchievements);

                _logger.Log("[Achievement] Synced with server");
                _messageBus.Publish(new AchievementsSyncedMessage());
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Achievement] Sync failed: {ex.Message}");
            }
        }

        public IReadOnlyList<AchievementDefinition> GetDefinitions()
        {
            return _state.GetDefinitions();
        }

        public async UniTask<OperationResult<int>> ClaimAllEligibleOptimisticAsync(string achievementId)
        {
            var achievement = _state.GetAchievement(achievementId);

            if (achievement == null)
                return OperationResult<int>.ValidationError("Achievement not found");

            if (!achievement.HasMultipleLevels)
                return OperationResult<int>.ValidationError("Achievement has no multiple levels");

            // Find all eligible levels that are not yet claimed
            var eligibleLevels = new List<int>();
            for (int i = 0; i < achievement.Levels.Count; i++)
            {
                var level = achievement.Levels[i];
                var levelNumber = level.Level;
                if (achievement.CurrentProgress >= level.TargetProgress && !achievement.IsLevelClaimed(levelNumber))
                {
                    eligibleLevels.Add(levelNumber);
                }
            }

            if (eligibleLevels.Count == 0)
                return OperationResult<int>.ValidationError("No eligible levels to claim");

            // 1. DUAL SNAPSHOT
            var achievementSnapshot = CreateSnapshot();
            var currencySnapshot = CurrencyService.CreateSnapshot();
            int totalRewardGiven = 0;

            // 2. OPTIMISTIC UPDATE - Claim all eligible levels
            foreach (var levelNumber in eligibleLevels)
            {
                _state.MarkLevelClaimed(achievementId, levelNumber);

                // Give reward for each level
                var levelIndex = levelNumber - 1; // 1-indexed to 0-indexed
                if (levelIndex >= 0 && levelIndex < achievement.Levels.Count)
                {
                    var levelData = achievement.Levels[levelIndex];
                    if (levelData != null)
                    {
                        var rewardResult = await CurrencyService.AddCurrencyOptimisticAsync(
                            levelData.RewardType,
                            levelData.RewardAmount,
                            $"achievement_{achievementId}_level_{levelNumber}"
                        );
                        if (rewardResult.IsSuccess)
                        {
                            totalRewardGiven += levelData.RewardAmount;
                        }
                    }
                }
            }

            PublishChange(achievementId, AchievementChangeType.Claimed, true,
                achievement.CurrentProgress, achievement.TargetProgress);

            _logger.Log($"[Achievement] Optimistic claim all eligible: {achievementId}, levels: {eligibleLevels.Count}");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.ClaimAllEligibleLevelsAsync(achievementId);

                if (response.Success)
                {
                    PublishChange(achievementId, AchievementChangeType.Claimed, false,
                        achievement.CurrentProgress, achievement.TargetProgress);
                    return OperationResult<int>.Success(eligibleLevels.Count);
                }
                else
                {
                    // ROLLBACK
                    RestoreSnapshot(achievementSnapshot);
                    CurrencyService.RestoreSnapshot(currencySnapshot);
                    PublishChange(achievementId, AchievementChangeType.RolledBack, false,
                        achievement.CurrentProgress, achievement.TargetProgress);
                    PublishRollback("ClaimAllEligible", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult<int>.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // ROLLBACK
                _logger.LogError($"[Achievement] ClaimAllEligible error: {ex.Message}");
                RestoreSnapshot(achievementSnapshot);
                CurrencyService.RestoreSnapshot(currencySnapshot);
                PublishChange(achievementId, AchievementChangeType.RolledBack, false,
                    achievement.CurrentProgress, achievement.TargetProgress);
                PublishRollback("ClaimAllEligible", ex.Message, RollbackCategory.Transient);
                return OperationResult<int>.NetworkError(ex);
            }
        }

        #endregion

        #region Private Helpers

        private void PublishChange(string id, AchievementChangeType type, bool isOptimistic, int current, int target)
        {
            var message = new AchievementChangedMessage(id, type, isOptimistic, current, target);
            OnAchievementChanged?.Invoke(message);
            _messageBus.Publish(message);
        }

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
