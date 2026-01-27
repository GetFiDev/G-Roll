namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Inventory değiştiğinde yayınlanan message.
    /// Item equip, unequip, acquire işlemlerinde kullanılır.
    /// </summary>
    public readonly struct InventoryChangedMessage : IMessage, IOptimisticMessage
    {
        /// <summary>
        /// Değişiklik tipi
        /// </summary>
        public InventoryChangeType ChangeType { get; }

        /// <summary>
        /// Etkilenen item'ın ID'si
        /// </summary>
        public string ItemId { get; }

        /// <summary>
        /// Slot ID'si (equip/unequip için)
        /// </summary>
        public string SlotId { get; }

        /// <summary>
        /// True ise optimistic update, false ise server confirmed
        /// </summary>
        public bool IsOptimistic { get; }

        public InventoryChangedMessage(
            InventoryChangeType type,
            string itemId,
            string slotId,
            bool isOptimistic)
        {
            ChangeType = type;
            ItemId = itemId;
            SlotId = slotId;
            IsOptimistic = isOptimistic;
        }
    }

    /// <summary>
    /// Inventory değişiklik tipleri
    /// </summary>
    public enum InventoryChangeType
    {
        /// <summary>Item equip edildi</summary>
        ItemEquipped,

        /// <summary>Item unequip edildi</summary>
        ItemUnequipped,

        /// <summary>Yeni item kazanıldı</summary>
        ItemAcquired,

        /// <summary>Item silindi/kaldırıldı</summary>
        ItemRemoved
    }
}
