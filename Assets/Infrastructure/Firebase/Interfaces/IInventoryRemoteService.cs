using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Inventory işlemleri için remote service interface.
    /// Firebase Cloud Functions çağrılarını soyutlar.
    /// </summary>
    public interface IInventoryRemoteService
    {
        /// <summary>
        /// Item'ı equip eder.
        /// </summary>
        UniTask<EquipItemResponse> EquipItemAsync(string itemId, string slotId);

        /// <summary>
        /// Item'ı unequip eder.
        /// </summary>
        UniTask<UnequipItemResponse> UnequipItemAsync(string itemId);

        /// <summary>
        /// Yeni item kazanır.
        /// </summary>
        UniTask<AcquireItemResponse> AcquireItemAsync(string itemId, string source);

        /// <summary>
        /// Server'dan tüm inventory'yi alır.
        /// </summary>
        UniTask<InventoryStateResponse> FetchInventoryAsync();
    }

    /// <summary>
    /// Equip item response
    /// </summary>
    public struct EquipItemResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static EquipItemResponse Successful()
        {
            return new EquipItemResponse { Success = true };
        }

        public static EquipItemResponse Failed(string error)
        {
            return new EquipItemResponse { Success = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// Unequip item response
    /// </summary>
    public struct UnequipItemResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static UnequipItemResponse Successful()
        {
            return new UnequipItemResponse { Success = true };
        }

        public static UnequipItemResponse Failed(string error)
        {
            return new UnequipItemResponse { Success = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// Acquire item response
    /// </summary>
    public struct AcquireItemResponse
    {
        public bool Success { get; set; }
        public InventoryItem Item { get; set; }
        public string ErrorMessage { get; set; }

        public static AcquireItemResponse Successful(InventoryItem item)
        {
            return new AcquireItemResponse { Success = true, Item = item };
        }

        public static AcquireItemResponse Failed(string error)
        {
            return new AcquireItemResponse { Success = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// Inventory state response
    /// </summary>
    public struct InventoryStateResponse
    {
        public Dictionary<string, InventoryItem> Items { get; set; }
        public Dictionary<string, string> EquippedSlots { get; set; }
    }
}
