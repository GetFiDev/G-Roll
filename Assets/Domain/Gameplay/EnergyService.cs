using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Gameplay
{
    /// <summary>
    /// Energy service implementation.
    /// Optimistic update pattern ile energy yönetimi yapar.
    /// Full snapshot/rollback desteği ile. Thread-safe.
    /// </summary>
    public class EnergyService : IEnergyService, ISnapshotable<EnergyStateSnapshot>
    {
        private int _currentEnergy;
        private int _maxEnergy = 5;
        private DateTime _nextRegenTime;
        private readonly int _regenIntervalSeconds = 300; // 5 minutes

        private readonly IEnergyRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        // Thread safety
        private readonly object _stateLock = new();
        private bool _hasPendingOperation;

        public event Action<EnergyChangedMessage> OnEnergyChanged;

        [Inject]
        public EnergyService(
            IEnergyRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #region ISnapshotable<EnergyStateSnapshot> Implementation

        /// <summary>
        /// Full state snapshot oluşturur (energy, maxEnergy, nextRegenTime).
        /// Thread-safe.
        /// </summary>
        public EnergyStateSnapshot CreateSnapshot()
        {
            lock (_stateLock)
            {
                return new EnergyStateSnapshot(_currentEnergy, _maxEnergy, _nextRegenTime);
            }
        }

        /// <summary>
        /// Snapshot'tan full state restore eder.
        /// Thread-safe.
        /// </summary>
        public void RestoreSnapshot(EnergyStateSnapshot snapshot)
        {
            int previousEnergy;

            lock (_stateLock)
            {
                previousEnergy = _currentEnergy;
                _currentEnergy = snapshot.CurrentEnergy;
                _maxEnergy = snapshot.MaxEnergy;
                _nextRegenTime = snapshot.NextRegenTime;
            }

            PublishChange(previousEnergy, false);
            _logger.Log("[Energy] State restored from snapshot");
        }

        #endregion

        #region IEnergyService Implementation

        public int CurrentEnergy
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentEnergy;
                }
            }
        }

        public int MaxEnergy
        {
            get
            {
                lock (_stateLock)
                {
                    return _maxEnergy;
                }
            }
        }

        public DateTime NextRegenTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _nextRegenTime;
                }
            }
        }

        public TimeSpan TimeUntilFull
        {
            get
            {
                lock (_stateLock)
                {
                    if (_currentEnergy >= _maxEnergy)
                        return TimeSpan.Zero;

                    var energyNeeded = _maxEnergy - _currentEnergy;
                    var totalSeconds = energyNeeded * _regenIntervalSeconds;
                    var now = DateTime.UtcNow;

                    if (_nextRegenTime > now)
                    {
                        var timeToNextRegen = (_nextRegenTime - now).TotalSeconds;
                        return TimeSpan.FromSeconds(timeToNextRegen + (energyNeeded - 1) * _regenIntervalSeconds);
                    }

                    return TimeSpan.FromSeconds(totalSeconds);
                }
            }
        }

        public bool HasEnoughEnergy(int amount)
        {
            lock (_stateLock)
            {
                return _currentEnergy >= amount;
            }
        }

        public async UniTask<OperationResult> ConsumeEnergyOptimisticAsync(int amount)
        {
            // Validation
            if (amount <= 0)
                return OperationResult.ValidationError("Amount must be positive");

            EnergyStateSnapshot snapshot;
            int previousEnergy;

            // Thread-safe state check and update
            lock (_stateLock)
            {
                if (_hasPendingOperation)
                    return OperationResult.ValidationError("Another operation is pending");

                if (_currentEnergy < amount)
                    return OperationResult.ValidationError("Insufficient energy");

                _hasPendingOperation = true;

                // 1. FULL SNAPSHOT (energy, maxEnergy, nextRegenTime)
                snapshot = new EnergyStateSnapshot(_currentEnergy, _maxEnergy, _nextRegenTime);
                previousEnergy = _currentEnergy;

                // 2. OPTIMISTIC UPDATE
                _currentEnergy -= amount;
            }

            PublishChange(previousEnergy, true);
            _logger.Log($"[Energy] Optimistic consume: -{amount}");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.ConsumeEnergyAsync(amount);

                if (response.Success)
                {
                    int currentBefore;
                    lock (_stateLock)
                    {
                        currentBefore = _currentEnergy;
                        _currentEnergy = response.CurrentEnergy;
                        _nextRegenTime = response.NextRegenTime;
                    }
                    PublishChange(currentBefore, false);
                    return OperationResult.Success();
                }
                else
                {
                    // FULL ROLLBACK
                    RestoreSnapshot(snapshot);
                    PublishRollback("ConsumeEnergy", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // FULL ROLLBACK
                _logger.LogError($"[Energy] Consume error: {ex.Message}");
                RestoreSnapshot(snapshot);
                PublishRollback("ConsumeEnergy", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
            finally
            {
                lock (_stateLock)
                {
                    _hasPendingOperation = false;
                }
            }
        }

        public async UniTask<OperationResult> RefillEnergyOptimisticAsync()
        {
            EnergyStateSnapshot snapshot;
            int previousEnergy;

            // Thread-safe state check and update
            lock (_stateLock)
            {
                if (_hasPendingOperation)
                    return OperationResult.ValidationError("Another operation is pending");

                _hasPendingOperation = true;

                // 1. FULL SNAPSHOT (energy, maxEnergy, nextRegenTime)
                snapshot = new EnergyStateSnapshot(_currentEnergy, _maxEnergy, _nextRegenTime);
                previousEnergy = _currentEnergy;

                // 2. OPTIMISTIC UPDATE
                _currentEnergy = _maxEnergy;
            }

            PublishChange(previousEnergy, true);
            _logger.Log("[Energy] Optimistic refill");

            // 3. SERVER REQUEST (Ad-based or IAP-based)
            try
            {
                var response = await _remoteService.RefillEnergyAsync();

                if (response.Success)
                {
                    int currentBefore;
                    lock (_stateLock)
                    {
                        currentBefore = _currentEnergy;
                        _currentEnergy = response.CurrentEnergy;
                        _nextRegenTime = response.NextRegenTime;
                    }
                    PublishChange(currentBefore, false);
                    return OperationResult.Success();
                }
                else
                {
                    // FULL ROLLBACK
                    RestoreSnapshot(snapshot);
                    PublishRollback("RefillEnergy", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // FULL ROLLBACK
                _logger.LogError($"[Energy] Refill error: {ex.Message}");
                RestoreSnapshot(snapshot);
                PublishRollback("RefillEnergy", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
            finally
            {
                lock (_stateLock)
                {
                    _hasPendingOperation = false;
                }
            }
        }

        public async UniTask<EnergySnapshot> FetchSnapshotAsync()
        {
            try
            {
                var response = await _remoteService.FetchEnergyStateAsync();
                int previousEnergy;

                lock (_stateLock)
                {
                    previousEnergy = _currentEnergy;
                    _currentEnergy = response.CurrentEnergy;
                    _maxEnergy = response.MaxEnergy;
                    _nextRegenTime = response.NextRegenTime;
                }

                PublishChange(previousEnergy, false);
                _logger.Log("[Energy] Fetched snapshot from server");

                lock (_stateLock)
                {
                    return new EnergySnapshot
                    {
                        CurrentEnergy = _currentEnergy,
                        MaxEnergy = _maxEnergy,
                        NextRegenTimestamp = new DateTimeOffset(_nextRegenTime).ToUnixTimeMilliseconds(),
                        RegenIntervalSeconds = response.RegenIntervalSeconds
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Energy] Fetch error: {ex.Message}");

                lock (_stateLock)
                {
                    return new EnergySnapshot
                    {
                        CurrentEnergy = _currentEnergy,
                        MaxEnergy = _maxEnergy,
                        NextRegenTimestamp = new DateTimeOffset(_nextRegenTime).ToUnixTimeMilliseconds(),
                        RegenIntervalSeconds = _regenIntervalSeconds
                    };
                }
            }
        }

        #endregion

        #region Private Helpers

        private void PublishChange(int previousEnergy, bool isOptimistic)
        {
            int currentEnergy, maxEnergy;
            DateTime nextRegenTime;

            lock (_stateLock)
            {
                currentEnergy = _currentEnergy;
                maxEnergy = _maxEnergy;
                nextRegenTime = _nextRegenTime;
            }

            var message = new EnergyChangedMessage(
                currentEnergy,
                maxEnergy,
                previousEnergy,
                nextRegenTime,
                isOptimistic
            );
            OnEnergyChanged?.Invoke(message);
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
