using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    /// Inventory service implementation.
    /// Optimistic update pattern ile inventory yönetimi yapar.
    /// </summary>
    public class InventoryService : IInventoryService
    {
        private readonly InventoryState _state;
        private readonly IInventoryRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        public event Action<InventoryChangedMessage> OnInventoryChanged;

        [Inject]
        public InventoryService(
            IInventoryRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _state = new InventoryState();
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #region ISnapshotable<InventorySnapshot> Implementation

        public InventorySnapshot CreateSnapshot()
        {
            return new InventorySnapshot
            {
                EquippedItems = _state.GetEquippedSlotsCopy(),
                OwnedItems = _state.GetItemsCopy(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public void RestoreSnapshot(InventorySnapshot snapshot)
        {
            // Restore owned items first (full item data)
            _state.SetItems(snapshot.OwnedItems);

            // Then restore equipped slots
            _state.SetEquippedSlots(snapshot.EquippedItems);
        }

        #endregion

        #region IInventoryService Implementation

        public bool HasItem(string itemId) => _state.HasItem(itemId);

        public bool IsEquipped(string itemId) => _state.IsEquipped(itemId);

        public IReadOnlyList<string> GetEquippedItems(string slotId)
        {
            var item = _state.GetEquippedItem(slotId);
            return item != null ? new List<string> { item } : new List<string>();
        }

        public IReadOnlyList<InventoryItem> GetAllItems() => _state.GetAllItems();

        public async UniTask<OperationResult> EquipItemOptimisticAsync(string itemId, string slotId)
        {
            // Validation
            if (!_state.HasItem(itemId))
                return OperationResult.ValidationError("Item not in inventory");

            if (_state.IsEquipped(itemId))
                return OperationResult.ValidationError("Item already equipped");

            // 1. SNAPSHOT
            var snapshot = CreateSnapshot();
            var previousEquipped = _state.GetEquippedItem(slotId);

            // 2. OPTIMISTIC UPDATE
            _state.EquipItem(itemId, slotId);
            PublishChange(InventoryChangeType.ItemEquipped, itemId, slotId, true);

            _logger.Log($"[Inventory] Optimistic equip: {itemId} in slot {slotId}");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.EquipItemAsync(itemId, slotId);

                if (response.Success)
                {
                    // 4a. CONFIRM
                    PublishChange(InventoryChangeType.ItemEquipped, itemId, slotId, false);
                    return OperationResult.Success();
                }
                else
                {
                    // 4b. ROLLBACK
                    RestoreSnapshot(snapshot);
                    PublishChange(InventoryChangeType.ItemUnequipped, itemId, slotId, false);
                    PublishRollback("EquipItem", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // 4c. ROLLBACK
                _logger.LogError($"[Inventory] Network error: {ex.Message}");
                RestoreSnapshot(snapshot);
                PublishChange(InventoryChangeType.ItemUnequipped, itemId, slotId, false);
                PublishRollback("EquipItem", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult> UnequipItemOptimisticAsync(string itemId)
        {
            // Validation
            if (!_state.IsEquipped(itemId))
                return OperationResult.ValidationError("Item not equipped");

            // Find the slot - TryGetSlotForItem kullan (FirstOrDefault bug fix)
            if (!_state.TryGetSlotForItem(itemId, out var slotId))
                return OperationResult.ValidationError("Slot not found for item");

            // 1. SNAPSHOT
            var snapshot = CreateSnapshot();

            // 2. OPTIMISTIC UPDATE
            _state.UnequipByItemId(itemId);
            PublishChange(InventoryChangeType.ItemUnequipped, itemId, slotId, true);

            _logger.Log($"[Inventory] Optimistic unequip: {itemId}");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.UnequipItemAsync(itemId);

                if (response.Success)
                {
                    PublishChange(InventoryChangeType.ItemUnequipped, itemId, slotId, false);
                    return OperationResult.Success();
                }
                else
                {
                    RestoreSnapshot(snapshot);
                    PublishChange(InventoryChangeType.ItemEquipped, itemId, slotId, false);
                    PublishRollback("UnequipItem", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Inventory] Network error: {ex.Message}");
                RestoreSnapshot(snapshot);
                PublishChange(InventoryChangeType.ItemEquipped, itemId, slotId, false);
                PublishRollback("UnequipItem", ex.Message, RollbackCategory.Transient);
                return OperationResult.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult<InventoryItem>> AcquireItemOptimisticAsync(
            string itemId,
            string source)
        {
            // Validation
            if (_state.HasItem(itemId))
                return OperationResult<InventoryItem>.ValidationError("Item already owned");

            // 1. SNAPSHOT
            var snapshot = CreateSnapshot();

            // 2. OPTIMISTIC UPDATE - Create temporary item
            var tempItem = new InventoryItem
            {
                ItemId = itemId,
                Name = itemId, // Placeholder
                AcquiredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            _state.AddItem(tempItem);
            PublishChange(InventoryChangeType.ItemAcquired, itemId, null, true);

            _logger.Log($"[Inventory] Optimistic acquire: {itemId} (source: {source})");

            // 3. SERVER REQUEST
            try
            {
                var response = await _remoteService.AcquireItemAsync(itemId, source);

                if (response.Success)
                {
                    // Update with actual server data
                    _state.RemoveItem(itemId);
                    _state.AddItem(response.Item);
                    PublishChange(InventoryChangeType.ItemAcquired, itemId, null, false);
                    return OperationResult<InventoryItem>.Success(response.Item);
                }
                else
                {
                    // ROLLBACK - RestoreSnapshot already restores to pre-acquire state
                    // (item didn't exist before, so it won't exist after restore)
                    RestoreSnapshot(snapshot);
                    PublishChange(InventoryChangeType.ItemRemoved, itemId, null, false);
                    PublishRollback("AcquireItem", response.ErrorMessage, RollbackCategory.BusinessRule);
                    return OperationResult<InventoryItem>.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // ROLLBACK - RestoreSnapshot already restores to pre-acquire state
                _logger.LogError($"[Inventory] Network error: {ex.Message}");
                RestoreSnapshot(snapshot);
                PublishChange(InventoryChangeType.ItemRemoved, itemId, null, false);
                PublishRollback("AcquireItem", ex.Message, RollbackCategory.Transient);
                return OperationResult<InventoryItem>.NetworkError(ex);
            }
        }

        public async UniTask SyncWithServerAsync()
        {
            try
            {
                var serverState = await _remoteService.FetchInventoryAsync();
                _state.SetItems(serverState.Items);
                _state.SetEquippedSlots(serverState.EquippedSlots);

                _logger.Log("[Inventory] Synced with server");
                _messageBus.Publish(new InventorySyncedMessage());
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Inventory] Sync failed: {ex.Message}");
            }
        }

        #endregion

        #region Private Helpers

        private void PublishChange(InventoryChangeType type, string itemId, string slotId, bool isOptimistic)
        {
            var message = new InventoryChangedMessage(type, itemId, slotId, isOptimistic);
            OnInventoryChanged?.Invoke(message);
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
