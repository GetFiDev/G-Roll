using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Inventory (envanter) yönetimi için service interface.
    /// Item equip/unequip ve acquire işlemlerini yönetir.
    /// </summary>
    public interface IInventoryService : ISnapshotable<InventorySnapshot>
    {
        /// <summary>
        /// Belirtilen item'a sahip mi?
        /// </summary>
        bool HasItem(string itemId);

        /// <summary>
        /// Belirtilen item equipped mı?
        /// </summary>
        bool IsEquipped(string itemId);

        /// <summary>
        /// Belirtilen slot'ta equipped item'ları döndürür.
        /// </summary>
        IReadOnlyList<string> GetEquippedItems(string slotId);

        /// <summary>
        /// Tüm item'ları döndürür.
        /// </summary>
        IReadOnlyList<InventoryItem> GetAllItems();

        /// <summary>
        /// Item'ı optimistic olarak equip eder.
        /// UI hemen güncellenir, başarısızlıkta rollback yapılır.
        /// </summary>
        UniTask<OperationResult> EquipItemOptimisticAsync(string itemId, string slotId);

        /// <summary>
        /// Item'ı optimistic olarak unequip eder.
        /// </summary>
        UniTask<OperationResult> UnequipItemOptimisticAsync(string itemId);

        /// <summary>
        /// Yeni item'ı optimistic olarak ekler.
        /// </summary>
        UniTask<OperationResult<InventoryItem>> AcquireItemOptimisticAsync(string itemId, string source);

        /// <summary>
        /// Server ile tam senkronizasyon yapar.
        /// </summary>
        UniTask SyncWithServerAsync();

        /// <summary>
        /// Inventory değiştiğinde tetiklenen event.
        /// </summary>
        event Action<InventoryChangedMessage> OnInventoryChanged;
    }

    /// <summary>
    /// Inventory item data
    /// </summary>
    public class InventoryItem
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string SlotId { get; set; }
        public bool IsEquipped { get; set; }
        public long AcquiredAt { get; set; }
    }

    /// <summary>
    /// Inventory state snapshot - rollback için.
    /// Full item data saklar, böylece rollback sırasında tüm bilgiler restore edilebilir.
    /// </summary>
    public class InventorySnapshot
    {
        /// <summary>
        /// Equipped slot mapping (slotId -> itemId)
        /// </summary>
        public Dictionary<string, string> EquippedItems { get; set; } = new();

        /// <summary>
        /// Owned items - Full item data (itemId -> InventoryItem)
        /// Rollback sırasında item'ların tüm bilgilerini (Name, AcquiredAt, etc.) korur.
        /// </summary>
        public Dictionary<string, InventoryItem> OwnedItems { get; set; } = new();

        /// <summary>
        /// Snapshot timestamp
        /// </summary>
        public long Timestamp { get; set; }
    }
}
