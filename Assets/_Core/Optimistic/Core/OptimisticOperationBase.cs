using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using UnityEngine;

namespace GRoll.Core.Optimistic
{
    /// <summary>
    /// Optimistic operation'lar için base class.
    /// Template Method pattern kullanır.
    ///
    /// Kullanım:
    /// <code>
    /// public class EquipItemOperation : OptimisticOperationBase&lt;InventorySnapshot, EquipResponse&gt;
    /// {
    ///     protected override InventorySnapshot CreateSnapshot() { ... }
    ///     protected override void ApplyOptimisticUpdate() { ... }
    ///     protected override UniTask&lt;ServerResult&lt;EquipResponse&gt;&gt; ExecuteServerOperationAsync() { ... }
    ///     protected override void RestoreFromSnapshot(InventorySnapshot snapshot) { ... }
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="TSnapshot">Snapshot tipi</typeparam>
    /// <typeparam name="TResult">Server response tipi</typeparam>
    public abstract class OptimisticOperationBase<TSnapshot, TResult>
    {
        protected readonly IMessageBus _messageBus;
        protected TSnapshot _snapshot;
        protected bool _isRolledBack;

        protected OptimisticOperationBase(IMessageBus messageBus)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// Optimistic operation'ı execute eder.
        /// </summary>
        public async UniTask<OperationResult<TResult>> ExecuteAsync()
        {
            // 1. Validation
            var validationResult = Validate();
            if (!validationResult.IsValid)
            {
                return OperationResult<TResult>.ValidationError(validationResult.ErrorMessage);
            }

            // 2. Snapshot
            _snapshot = CreateSnapshot();

            // 3. Optimistic Update
            try
            {
                ApplyOptimisticUpdate();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OptimisticOperation] Optimistic update failed: {ex}");
                return OperationResult<TResult>.ValidationError("Failed to apply optimistic update");
            }

            // 4. Server Request
            try
            {
                var serverResult = await ExecuteServerOperationAsync();

                if (serverResult.IsSuccess)
                {
                    // 5a. Confirm
                    ConfirmWithServerData(serverResult.Data);
                    return OperationResult<TResult>.Success(serverResult.Data);
                }
                else
                {
                    // 5b. Rollback (business error)
                    Rollback(serverResult.Message, RollbackCategory.BusinessRule);
                    return OperationResult<TResult>.RolledBack(serverResult.Message);
                }
            }
            catch (Exception ex)
            {
                // 5c. Rollback (network error)
                Debug.LogError($"[OptimisticOperation] Server operation failed: {ex}");

                var category = CategorizeException(ex);
                Rollback(ex.Message, category);

                var canRetry = IsRetryableException(ex);
                return OperationResult<TResult>.NetworkError(ex, canRetry);
            }
        }

        #region Abstract Methods - Subclass Must Implement

        /// <summary>
        /// Snapshot oluşturur. Subclass implement etmeli.
        /// </summary>
        protected abstract TSnapshot CreateSnapshot();

        /// <summary>
        /// Optimistic update uygular. Subclass implement etmeli.
        /// Bu metod UI'ın hemen güncellenmesini sağlamalı.
        /// </summary>
        protected abstract void ApplyOptimisticUpdate();

        /// <summary>
        /// Server operation'ı execute eder. Subclass implement etmeli.
        /// </summary>
        protected abstract UniTask<ServerResult<TResult>> ExecuteServerOperationAsync();

        /// <summary>
        /// Snapshot'tan state'i restore eder. Subclass implement etmeli.
        /// </summary>
        protected abstract void RestoreFromSnapshot(TSnapshot snapshot);

        #endregion

        #region Virtual Methods - Subclass Can Override

        /// <summary>
        /// İşlem öncesi validation. Override edilebilir.
        /// </summary>
        protected virtual ValidationResult Validate() => ValidationResult.Valid();

        /// <summary>
        /// Server response ile state'i confirm eder. Override edilebilir.
        /// Server'dan gelen ek verilerle (timestamp, bonus, etc.) state güncellemesi için.
        /// </summary>
        protected virtual void ConfirmWithServerData(TResult serverData) { }

        /// <summary>
        /// Rollback'te kullanıcı bilgilendirilmeli mi?
        /// </summary>
        protected virtual bool ShouldNotifyUserOnRollback() => true;

        /// <summary>
        /// Operation tipi adı (logging ve notification için)
        /// </summary>
        protected virtual string OperationTypeName => GetType().Name;

        /// <summary>
        /// Exception retry edilebilir mi?
        /// </summary>
        protected virtual bool IsRetryableException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is System.Net.WebException ||
                   (ex.Message?.Contains("network", StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (ex.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        /// <summary>
        /// Exception kategorisini belirler.
        /// </summary>
        protected virtual RollbackCategory CategorizeException(Exception ex)
        {
            if (IsRetryableException(ex))
                return RollbackCategory.Transient;

            return RollbackCategory.Critical;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Rollback işlemini yapar ve event publish eder.
        /// </summary>
        protected void Rollback(string reason, RollbackCategory category)
        {
            if (_isRolledBack) return;
            _isRolledBack = true;

            try
            {
                RestoreFromSnapshot(_snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OptimisticOperation] Rollback failed: {ex}");
            }

            _messageBus.Publish(new OperationRolledBackMessage(
                OperationTypeName,
                reason,
                ShouldNotifyUserOnRollback(),
                category
            ));
        }

        #endregion

        #region Helper Structs

        /// <summary>
        /// Validation sonucu
        /// </summary>
        protected readonly struct ValidationResult
        {
            public bool IsValid { get; }
            public string ErrorMessage { get; }

            private ValidationResult(bool isValid, string errorMessage)
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }

            public static ValidationResult Valid() => new(true, null);
            public static ValidationResult Invalid(string message) => new(false, message);
        }

        /// <summary>
        /// Server operation sonucu
        /// </summary>
        protected readonly struct ServerResult<T>
        {
            public bool IsSuccess { get; }
            public T Data { get; }
            public string Message { get; }

            private ServerResult(bool success, T data, string message)
            {
                IsSuccess = success;
                Data = data;
                Message = message;
            }

            public static ServerResult<T> Success(T data) => new(true, data, null);
            public static ServerResult<T> Failure(string message) => new(false, default, message);
        }

        #endregion
    }
}
