using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Domain.Economy.Models;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Economy
{
    /// <summary>
    /// Currency service implementation.
    /// Optimistic update pattern ile currency yönetimi yapar.
    /// </summary>
    public class CurrencyService : ICurrencyService
    {
        private readonly CurrencyState _state;
        private readonly ICurrencyRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private readonly object _operationLock = new();
        private CurrencySnapshot _lastSnapshot;
        private bool _hasPendingOperation;

        public event Action<CurrencyChangedMessage> OnCurrencyChanged;

        [Inject]
        public CurrencyService(
            ICurrencyRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _state = new CurrencyState();
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #region ISnapshotable<CurrencySnapshot> Implementation

        public CurrencySnapshot CreateSnapshot()
        {
            var balances = _state.GetBalancesCopy();
            return new CurrencySnapshot
            {
                SoftCurrencyBalance = balances.TryGetValue(CurrencyType.SoftCurrency, out var soft) ? soft : 0,
                HardCurrencyBalance = balances.TryGetValue(CurrencyType.HardCurrency, out var hard) ? hard : 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public void RestoreSnapshot(CurrencySnapshot snapshot)
        {
            var previousSoft = _state.GetBalance(CurrencyType.SoftCurrency);
            var previousHard = _state.GetBalance(CurrencyType.HardCurrency);

            _state.SetBalance(CurrencyType.SoftCurrency, snapshot.SoftCurrencyBalance);
            _state.SetBalance(CurrencyType.HardCurrency, snapshot.HardCurrencyBalance);

            // Publish rollback events
            if (previousSoft != snapshot.SoftCurrencyBalance)
            {
                PublishChange(CurrencyType.SoftCurrency, previousSoft, snapshot.SoftCurrencyBalance, false, "rollback");
            }
            if (previousHard != snapshot.HardCurrencyBalance)
            {
                PublishChange(CurrencyType.HardCurrency, previousHard, snapshot.HardCurrencyBalance, false, "rollback");
            }
        }

        #endregion

        #region ICurrencyService Implementation

        public int GetBalance(CurrencyType type) => _state.GetBalance(type);

        public bool CanAfford(CurrencyType type, int amount) => _state.CanAfford(type, amount);

        public async UniTask<OperationResult> AddCurrencyOptimisticAsync(
            CurrencyType type,
            int amount,
            string source)
        {
            // Validation
            if (amount <= 0)
                return OperationResult.ValidationError("Amount must be positive");

            if (string.IsNullOrEmpty(source))
                return OperationResult.ValidationError("Source is required for tracking");

            // Thread-safe check and set for pending operation
            lock (_operationLock)
            {
                if (_hasPendingOperation)
                    return OperationResult.ValidationError("Another operation is pending");

                _hasPendingOperation = true;
            }

            // 1. SNAPSHOT
            _lastSnapshot = CreateSnapshot();
            var previousBalance = _state.GetBalance(type);

            // 2. OPTIMISTIC UPDATE
            _state.Add(type, amount);
            var newBalance = _state.GetBalance(type);
            PublishChange(type, previousBalance, newBalance, true, source);

            _logger.Log($"[Currency] Optimistic add: {type} +{amount} (source: {source})");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.AddCurrencyAsync(type, amount, source);

                if (response.Success)
                {
                    // 4a. CONFIRM - Sync with server balance
                    if (response.NewBalance != newBalance)
                    {
                        _logger.LogWarning($"[Currency] Balance mismatch. Client: {newBalance}, Server: {response.NewBalance}");
                        var currentBalance = _state.GetBalance(type);
                        _state.SetBalance(type, response.NewBalance);
                        PublishChange(type, currentBalance, response.NewBalance, false, source);
                    }

                    return OperationResult.Success();
                }
                else
                {
                    // 4b. ROLLBACK - Business error
                    RestoreSnapshot(_lastSnapshot);
                    PublishRollback("AddCurrency", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // 4c. ROLLBACK - Network error
                _logger.LogError($"[Currency] Network error: {ex.Message}");
                RestoreSnapshot(_lastSnapshot);
                PublishRollback("AddCurrency", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
            finally
            {
                // Thread-safe release of pending operation flag
                lock (_operationLock)
                {
                    _hasPendingOperation = false;
                }
            }
        }

        public async UniTask<OperationResult> SpendCurrencyOptimisticAsync(
            CurrencyType type,
            int amount,
            string reason)
        {
            // Validation
            if (amount <= 0)
                return OperationResult.ValidationError("Amount must be positive");

            if (string.IsNullOrEmpty(reason))
                return OperationResult.ValidationError("Reason is required for tracking");

            if (!_state.CanAfford(type, amount))
                return OperationResult.ValidationError($"Insufficient {type}");

            // Thread-safe check and set for pending operation
            lock (_operationLock)
            {
                if (_hasPendingOperation)
                    return OperationResult.ValidationError("Another operation is pending");

                _hasPendingOperation = true;
            }

            // 1. SNAPSHOT
            _lastSnapshot = CreateSnapshot();
            var previousBalance = _state.GetBalance(type);

            // 2. OPTIMISTIC UPDATE
            _state.TrySpend(type, amount);
            var newBalance = _state.GetBalance(type);
            PublishChange(type, previousBalance, newBalance, true, reason);

            _logger.Log($"[Currency] Optimistic spend: {type} -{amount} (reason: {reason})");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.SpendCurrencyAsync(type, amount, reason);

                if (response.Success)
                {
                    // 4a. CONFIRM
                    if (response.NewBalance != newBalance)
                    {
                        var currentBalance = _state.GetBalance(type);
                        _state.SetBalance(type, response.NewBalance);
                        PublishChange(type, currentBalance, response.NewBalance, false, reason);
                    }

                    return OperationResult.Success();
                }
                else
                {
                    // 4b. ROLLBACK
                    RestoreSnapshot(_lastSnapshot);
                    PublishRollback("SpendCurrency", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // 4c. ROLLBACK
                _logger.LogError($"[Currency] Network error: {ex.Message}");
                RestoreSnapshot(_lastSnapshot);
                PublishRollback("SpendCurrency", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
            finally
            {
                // Thread-safe release of pending operation flag
                lock (_operationLock)
                {
                    _hasPendingOperation = false;
                }
            }
        }

        public async UniTask SyncWithServerAsync()
        {
            try
            {
                var serverState = await _remoteService.FetchBalancesAsync();

                foreach (var kvp in serverState.Balances)
                {
                    var localBalance = _state.GetBalance(kvp.Key);
                    if (localBalance != kvp.Value)
                    {
                        _state.SetBalance(kvp.Key, kvp.Value);
                        PublishChange(kvp.Key, localBalance, kvp.Value, false, "sync");
                    }
                }

                _logger.Log("[Currency] Synced with server");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Currency] Sync failed: {ex.Message}");
            }
        }

        #endregion

        #region Private Helpers

        private void PublishChange(CurrencyType type, int previous, int newAmount, bool isOptimistic, string source)
        {
            var message = new CurrencyChangedMessage(type, previous, newAmount, isOptimistic, source);
            OnCurrencyChanged?.Invoke(message);
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
