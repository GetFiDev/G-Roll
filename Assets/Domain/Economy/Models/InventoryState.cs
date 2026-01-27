using System;
using System.Collections.Generic;
using System.Linq;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Domain.Economy.Models
{
    /// <summary>
    /// Inventory state'ini tutan internal class.
    /// InventoryService tarafından yönetilir.
    /// Thread-safe.
    /// </summary>
    public class InventoryState
    {
        private readonly Dictionary<string, InventoryItem> _items = new();
        private readonly Dictionary<string, string> _equippedSlots = new(); // slotId -> itemId
        private readonly object _lock = new();

        /// <summary>
        /// Belirtilen item envanterde var mı?
        /// </summary>
        public bool HasItem(string itemId)
        {
            lock (_lock)
            {
                return _items.ContainsKey(itemId);
            }
        }

        /// <summary>
        /// Belirtilen item equipped mı?
        /// </summary>
        public bool IsEquipped(string itemId)
        {
            lock (_lock)
            {
                return _equippedSlots.ContainsValue(itemId);
            }
        }

        /// <summary>
        /// Belirtilen slot'ta equipped item'ın ID'sini döndürür.
        /// </summary>
        public string GetEquippedItem(string slotId)
        {
            lock (_lock)
            {
                return _equippedSlots.TryGetValue(slotId, out var itemId) ? itemId : null;
            }
        }

        /// <summary>
        /// Belirtilen item'ın equipped olduğu slot'u döndürür.
        /// FirstOrDefault bug fix - doğru KeyValuePair kontrolü yapılır.
        /// </summary>
        /// <param name="itemId">Aranacak item ID</param>
        /// <param name="slotId">Bulunan slot ID (out parameter)</param>
        /// <returns>Slot bulunduysa true, bulunamadıysa false</returns>
        public bool TryGetSlotForItem(string itemId, out string slotId)
        {
            lock (_lock)
            {
                foreach (var kvp in _equippedSlots)
                {
                    if (kvp.Value == itemId)
                    {
                        slotId = kvp.Key;
                        return true;
                    }
                }
                slotId = null;
                return false;
            }
        }

        /// <summary>
        /// Tüm equipped item ID'lerini döndürür.
        /// </summary>
        public IReadOnlyList<string> GetEquippedItemIds()
        {
            lock (_lock)
            {
                return _equippedSlots.Values.ToList();
            }
        }

        /// <summary>
        /// Tüm item'ları döndürür.
        /// </summary>
        public IReadOnlyList<InventoryItem> GetAllItems()
        {
            lock (_lock)
            {
                return _items.Values.ToList();
            }
        }

        /// <summary>
        /// Belirtilen item'ı döndürür.
        /// </summary>
        public InventoryItem GetItem(string itemId)
        {
            lock (_lock)
            {
                return _items.TryGetValue(itemId, out var item) ? item : null;
            }
        }

        /// <summary>
        /// Envantere item ekler.
        /// </summary>
        public void AddItem(InventoryItem item)
        {
            lock (_lock)
            {
                _items[item.ItemId] = item;
            }
        }

        /// <summary>
        /// Envanterden item kaldırır.
        /// Eğer equipped ise otomatik unequip eder.
        /// </summary>
        public void RemoveItem(string itemId)
        {
            lock (_lock)
            {
                _items.Remove(itemId);

                // Unequip if equipped - proper iteration without FirstOrDefault
                string slotToRemove = null;
                foreach (var kvp in _equippedSlots)
                {
                    if (kvp.Value == itemId)
                    {
                        slotToRemove = kvp.Key;
                        break;
                    }
                }
                if (slotToRemove != null)
                {
                    _equippedSlots.Remove(slotToRemove);
                }
            }
        }

        /// <summary>
        /// Item'ı belirtilen slot'a equip eder.
        /// </summary>
        public void EquipItem(string itemId, string slotId)
        {
            lock (_lock)
            {
                if (!_items.ContainsKey(itemId))
                    throw new InvalidOperationException($"Item {itemId} not in inventory");

                // Eğer slot doluysa önce unequip et
                if (_equippedSlots.ContainsKey(slotId))
                {
                    var oldItemId = _equippedSlots[slotId];
                    if (_items.TryGetValue(oldItemId, out var oldItem))
                    {
                        oldItem.IsEquipped = false;
                    }
                }

                _equippedSlots[slotId] = itemId;

                if (_items.TryGetValue(itemId, out var item))
                {
                    item.IsEquipped = true;
                    item.SlotId = slotId;
                }
            }
        }

        /// <summary>
        /// Belirtilen slot'tan item'ı unequip eder.
        /// </summary>
        public void UnequipSlot(string slotId)
        {
            lock (_lock)
            {
                if (_equippedSlots.TryGetValue(slotId, out var itemId))
                {
                    _equippedSlots.Remove(slotId);

                    if (_items.TryGetValue(itemId, out var item))
                    {
                        item.IsEquipped = false;
                        item.SlotId = null;
                    }
                }
            }
        }

        /// <summary>
        /// Belirtilen item'ı unequip eder.
        /// </summary>
        public void UnequipByItemId(string itemId)
        {
            lock (_lock)
            {
                // Proper iteration without FirstOrDefault bug
                string slotToRemove = null;
                foreach (var kvp in _equippedSlots)
                {
                    if (kvp.Value == itemId)
                    {
                        slotToRemove = kvp.Key;
                        break;
                    }
                }

                if (slotToRemove != null)
                {
                    _equippedSlots.Remove(slotToRemove);

                    if (_items.TryGetValue(itemId, out var item))
                    {
                        item.IsEquipped = false;
                        item.SlotId = null;
                    }
                }
            }
        }

        /// <summary>
        /// Equipped slot'ların kopyasını döndürür.
        /// </summary>
        public Dictionary<string, string> GetEquippedSlotsCopy()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_equippedSlots);
            }
        }

        /// <summary>
        /// Tüm item'ların kopyasını döndürür.
        /// </summary>
        public Dictionary<string, InventoryItem> GetItemsCopy()
        {
            lock (_lock)
            {
                var copy = new Dictionary<string, InventoryItem>();
                foreach (var kvp in _items)
                {
                    copy[kvp.Key] = CloneItem(kvp.Value);
                }
                return copy;
            }
        }

        /// <summary>
        /// State'i verilen verilerle değiştirir.
        /// Deep copy yapılır, snapshot mutasyonundan korunur.
        /// </summary>
        public void SetItems(Dictionary<string, InventoryItem> items)
        {
            lock (_lock)
            {
                _items.Clear();
                foreach (var kvp in items)
                {
                    // Deep copy to prevent snapshot mutation affecting restored state
                    _items[kvp.Key] = CloneItem(kvp.Value);
                }
            }
        }

        /// <summary>
        /// Equipped slot'ları verilen verilerle değiştirir.
        /// </summary>
        public void SetEquippedSlots(Dictionary<string, string> slots)
        {
            lock (_lock)
            {
                _equippedSlots.Clear();
                foreach (var kvp in slots)
                {
                    _equippedSlots[kvp.Key] = kvp.Value;
                }

                // IsEquipped flag'lerini güncelle - proper iteration without FirstOrDefault
                foreach (var item in _items.Values)
                {
                    string foundSlot = null;
                    foreach (var slot in _equippedSlots)
                    {
                        if (slot.Value == item.ItemId)
                        {
                            foundSlot = slot.Key;
                            break;
                        }
                    }
                    item.IsEquipped = foundSlot != null;
                    item.SlotId = foundSlot;
                }
            }
        }

        private static InventoryItem CloneItem(InventoryItem item)
        {
            return new InventoryItem
            {
                ItemId = item.ItemId,
                Name = item.Name,
                SlotId = item.SlotId,
                IsEquipped = item.IsEquipped,
                AcquiredAt = item.AcquiredAt
            };
        }
    }
}
