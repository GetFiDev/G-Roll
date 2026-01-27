using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// Task service implementation.
    /// Batched optimistic update pattern ile task yönetimi yapar.
    /// Full snapshot/rollback desteği ile.
    /// </summary>
    public class TaskService : ITaskService, IDisposable
    {
        private readonly TaskState _state;
        private readonly ITaskRemoteService _remoteService;
        private readonly ICurrencyService _currencyService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        // Batching
        private readonly Dictionary<string, int> _pendingProgressUpdates = new();
        private readonly object _batchLock = new();
        private readonly float _batchIntervalSeconds = 2.0f;
        private bool _batchTimerRunning;
        private CancellationTokenSource _batchCts;

        public event Action<TaskProgressMessage> OnTaskProgressChanged;

        [Inject]
        public TaskService(
            ITaskRemoteService remoteService,
            ICurrencyService currencyService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _state = new TaskState();
            _remoteService = remoteService;
            _currencyService = currencyService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #region ISnapshotable<TaskStateSnapshot> Implementation

        /// <summary>
        /// Full task state snapshot oluşturur.
        /// </summary>
        public TaskStateSnapshot CreateSnapshot()
        {
            return new TaskStateSnapshot(_state.GetTasksCopy());
        }

        /// <summary>
        /// Snapshot'tan full state restore eder.
        /// </summary>
        public void RestoreSnapshot(TaskStateSnapshot snapshot)
        {
            _state.RestoreFromSnapshot(snapshot.Tasks);
            _logger.Log("[Task] State restored from snapshot");
        }

        #endregion

        #region ITaskService Implementation

        public IReadOnlyList<GameTask> GetActiveTasks() => _state.GetActiveTasks();

        public IReadOnlyList<GameTask> GetCompletedTasks() => _state.GetCompletedTasks();

        public GameTask GetTask(string taskId) => _state.GetTask(taskId);

        /// <summary>
        /// Optimistic progress update with batching.
        /// Updates are collected and sent to server every 2 seconds.
        /// </summary>
        public void AddProgressOptimistic(string taskId, int amount)
        {
            var task = _state.GetTask(taskId);
            if (task == null || task.IsCompleted) return;

            // 1. OPTIMISTIC UPDATE (immediate)
            _state.AddProgress(taskId, amount);
            var updatedTask = _state.GetTask(taskId);

            // 2. Publish immediate UI update
            PublishProgress(updatedTask, true);

            // 3. Queue for batch
            lock (_batchLock)
            {
                if (!_pendingProgressUpdates.ContainsKey(taskId))
                {
                    _pendingProgressUpdates[taskId] = 0;
                }
                _pendingProgressUpdates[taskId] += amount;
            }

            _logger.Log($"[Task] Optimistic progress: {taskId} +{amount} (queued for batch)");

            // 4. Start batch timer if not running
            StartBatchTimerIfNeeded();
        }

        public async UniTask<OperationResult> ClaimTaskRewardOptimisticAsync(string taskId)
        {
            var task = _state.GetTask(taskId);

            // Validation
            if (task == null)
                return OperationResult.ValidationError("Task not found");

            if (!task.IsCompleted)
                return OperationResult.ValidationError("Task not completed");

            if (task.IsClaimed)
                return OperationResult.ValidationError("Task already claimed");

            // Flush pending progress first
            await FlushProgressBatchAsync();

            // 1. DUAL SNAPSHOT - Full task state AND currency state BEFORE any changes
            // Bu sayede herhangi bir hata durumunda her ikisi de tutarlı şekilde geri alınabilir
            var taskSnapshot = CreateSnapshot();
            var currencySnapshot = _currencyService.CreateSnapshot();
            var rewardGiven = false;

            // 2. OPTIMISTIC UPDATE - Mark task as claimed
            _state.MarkClaimed(taskId);
            PublishProgress(_state.GetTask(taskId), true);

            _logger.Log($"[Task] Optimistic claim: {taskId}");

            // 3. Give reward optimistically (track if given for rollback decision)
            if (task.Reward != null)
            {
                var rewardResult = await _currencyService.AddCurrencyOptimisticAsync(
                    task.Reward.CurrencyType,
                    task.Reward.Amount,
                    $"task_reward_{taskId}"
                );
                rewardGiven = rewardResult.IsSuccess;

                // If reward failed to be added, rollback task claim and return
                if (!rewardGiven)
                {
                    RestoreSnapshot(taskSnapshot);
                    PublishProgress(_state.GetTask(taskId), false);
                    PublishRollback("ClaimTaskReward", "Failed to add reward currency", RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack("Failed to add reward currency");
                }
            }

            // 4. SERVER REQUEST - Confirm the claim
            try
            {
                var response = await _remoteService.ClaimTaskRewardAsync(taskId);

                if (response.Success)
                {
                    PublishProgress(_state.GetTask(taskId), false);
                    return OperationResult.Success();
                }
                else
                {
                    // FULL ROLLBACK - Server rejected, restore both task and currency to pre-claim state
                    RestoreSnapshot(taskSnapshot);
                    if (rewardGiven)
                    {
                        _currencyService.RestoreSnapshot(currencySnapshot);
                    }
                    PublishProgress(_state.GetTask(taskId), false);
                    PublishRollback("ClaimTaskReward", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // FULL ROLLBACK - Network error, restore both task and currency to pre-claim state
                _logger.LogError($"[Task] Claim error: {ex.Message}");
                RestoreSnapshot(taskSnapshot);
                if (rewardGiven)
                {
                    _currencyService.RestoreSnapshot(currencySnapshot);
                }
                PublishProgress(_state.GetTask(taskId), false);
                PublishRollback("ClaimTaskReward", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
        }

        public async UniTask SyncWithServerAsync()
        {
            // Flush pending first
            await FlushProgressBatchAsync();

            try
            {
                var serverTasks = await _remoteService.FetchTasksAsync();
                _state.SetTasks(serverTasks);

                foreach (var task in serverTasks)
                {
                    PublishProgress(task, false);
                }

                _logger.Log("[Task] Synced with server");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Task] Sync failed: {ex.Message}");
            }
        }

        #endregion

        #region Batching Logic

        private void StartBatchTimerIfNeeded()
        {
            lock (_batchLock)
            {
                if (_batchTimerRunning) return;
                _batchTimerRunning = true;
            }

            // Dispose previous CancellationTokenSource to prevent memory leak
            _batchCts?.Cancel();
            _batchCts?.Dispose();
            _batchCts = new CancellationTokenSource();
            RunBatchTimer(_batchCts.Token).Forget();
        }

        private async UniTaskVoid RunBatchTimer(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool hasPending;
                    lock (_batchLock)
                    {
                        hasPending = _pendingProgressUpdates.Count > 0;
                    }

                    if (!hasPending)
                    {
                        lock (_batchLock)
                        {
                            _batchTimerRunning = false;
                        }
                        return;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(_batchIntervalSeconds), cancellationToken: ct);

                    lock (_batchLock)
                    {
                        if (_pendingProgressUpdates.Count > 0)
                        {
                            FlushProgressBatchAsync().Forget();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timer cancelled, ignore
            }

            lock (_batchLock)
            {
                _batchTimerRunning = false;
            }
        }

        private async UniTask FlushProgressBatchAsync()
        {
            Dictionary<string, int> batch;
            TaskStateSnapshot snapshotBeforeBatch;

            lock (_batchLock)
            {
                if (_pendingProgressUpdates.Count == 0) return;

                // Copy and clear
                batch = new Dictionary<string, int>(_pendingProgressUpdates);
                _pendingProgressUpdates.Clear();
            }

            // Snapshot before sending - for rollback if server rejects
            snapshotBeforeBatch = CreateSnapshot();

            _logger.Log($"[Task] Flushing batch: {batch.Count} tasks");

            try
            {
                var response = await _remoteService.BatchUpdateProgressAsync(batch);

                if (!response.Success)
                {
                    // ROLLBACK all in batch using snapshot
                    foreach (var kvp in batch)
                    {
                        var task = _state.GetTask(kvp.Key);
                        if (task != null)
                        {
                            var newProgress = Math.Max(0, task.CurrentProgress - kvp.Value);
                            _state.UpdateProgress(kvp.Key, newProgress);
                            PublishProgress(_state.GetTask(kvp.Key), false);
                        }
                    }

                    PublishRollback("BatchProgressUpdate", response.ErrorMessage, RollbackCategory.BusinessRule);
                    _logger.LogWarning($"[Task] Batch failed, rolled back {batch.Count} tasks");
                }
                else
                {
                    // Sync with server values
                    foreach (var serverTask in response.Tasks)
                    {
                        _state.UpdateProgress(serverTask.TaskId, serverTask.CurrentProgress);
                        _state.SetClaimed(serverTask.TaskId, serverTask.IsClaimed);
                        PublishProgress(_state.GetTask(serverTask.TaskId), false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Task] Batch error: {ex.Message}");

                // Re-queue failed batch for retry
                lock (_batchLock)
                {
                    foreach (var kvp in batch)
                    {
                        if (!_pendingProgressUpdates.ContainsKey(kvp.Key))
                            _pendingProgressUpdates[kvp.Key] = 0;

                        _pendingProgressUpdates[kvp.Key] += kvp.Value;
                    }
                }

                // Restart batch timer
                StartBatchTimerIfNeeded();
            }
        }

        #endregion

        #region Private Helpers

        private void PublishProgress(GameTask task, bool isOptimistic)
        {
            if (task == null) return;

            var message = new TaskProgressMessage(
                task.TaskId,
                task.CurrentProgress,
                task.TargetProgress,
                task.IsCompleted,
                isOptimistic,
                task.IsClaimed
            );

            OnTaskProgressChanged?.Invoke(message);
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

        #region IDisposable

        public void Dispose()
        {
            _batchCts?.Cancel();
            _batchCts?.Dispose();
            _batchCts = null;
        }

        #endregion
    }
}
