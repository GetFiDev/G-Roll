using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Remote item service interface.
    /// Sunucudan item verilerini cekme ve shop islemleri.
    /// </summary>
    public interface IRemoteItemService
    {
        /// <summary>
        /// Tum itemlari sunucudan cek.
        /// </summary>
        UniTask<Dictionary<string, RemoteItemData>> FetchAllItemsAsync();

        /// <summary>
        /// Tum itemlari ikonlariyla birlikte cek.
        /// </summary>
        UniTask<Dictionary<string, RemoteItemData>> FetchAllItemsWithIconsAsync();

        /// <summary>
        /// Belirli bir URL'den texture indir.
        /// </summary>
        UniTask<Texture2D> DownloadTextureAsync(string url);

        /// <summary>
        /// Kullanicinin sahip oldugu itemlari kontrol et.
        /// </summary>
        UniTask<ItemOwnershipResponse> CheckOwnershipAsync();

        /// <summary>
        /// Envanter snapshot'i al (owned items + equipped items).
        /// </summary>
        UniTask<InventorySnapshotResponse> GetInventorySnapshotAsync();

        /// <summary>
        /// Item satin al.
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <param name="method">Satin alma yontemi: GET, PREMIUM, AD</param>
        /// <param name="adToken">AD yontemi icin reklam tokeni</param>
        UniTask<PurchaseItemResponse> PurchaseItemAsync(string itemId, string method, string adToken = null);

        /// <summary>
        /// Item'i ekiple (equip).
        /// </summary>
        UniTask<ItemEquipResponse> EquipItemAsync(string itemId);

        /// <summary>
        /// Item'i cikar (unequip).
        /// </summary>
        UniTask<ItemEquipResponse> UnequipItemAsync(string itemId);

        /// <summary>
        /// Aktif consumable'lari getir.
        /// </summary>
        UniTask<ActiveConsumablesResponse> GetActiveConsumablesAsync();
    }

    #region Response Types

    public struct ItemOwnershipResponse
    {
        public bool Success { get; set; }
        public List<string> OwnedItemIds { get; set; }
        public string ErrorMessage { get; set; }
    }

    public struct InventorySnapshotResponse
    {
        public bool Success { get; set; }
        public Dictionary<string, InventoryItemData> Inventory { get; set; }
        public List<string> EquippedItemIds { get; set; }
        public string ErrorMessage { get; set; }
    }

    public struct InventoryItemData
    {
        public string Id { get; set; }
        public bool Owned { get; set; }
        public bool Equipped { get; set; }
        public int Quantity { get; set; }
        public bool IsConsumable { get; set; }
    }

    public struct PurchaseItemResponse
    {
        public bool Success { get; set; }
        public string ItemId { get; set; }
        public bool Owned { get; set; }
        public bool IsConsumable { get; set; }
        public double CurrencyLeft { get; set; }
        public double PremiumCurrencyLeft { get; set; }
        public long? ExpiresAtMillis { get; set; }
        public string ErrorMessage { get; set; }
    }

    public struct ItemEquipResponse
    {
        public bool Success { get; set; }
        public string ItemId { get; set; }
        public string ErrorMessage { get; set; }
    }

    public struct ActiveConsumablesResponse
    {
        public bool Success { get; set; }
        public long ServerNowMillis { get; set; }
        public List<ActiveConsumableData> Items { get; set; }
        public string ErrorMessage { get; set; }
    }

    public struct ActiveConsumableData
    {
        public string ItemId { get; set; }
        public bool Active { get; set; }
        public long? ExpiresAtMillis { get; set; }
    }

    #endregion

    /// <summary>
    /// Remote item data structure
    /// </summary>
    public class RemoteItemData
    {
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public string ItemIconUrl { get; set; }
        public double ItemPremiumPrice { get; set; }
        public double ItemGetPrice { get; set; }
        public bool ItemIsConsumable { get; set; }
        public bool ItemIsRewardedAd { get; set; }
        public int ItemReferralThreshold { get; set; }

        // Stats
        public double CoinMultiplierPercent { get; set; }
        public double ComboPower { get; set; }
        public double GameplaySpeedMultiplierPercent { get; set; }
        public double MagnetPowerPercent { get; set; }
        public double PlayerAcceleration { get; set; }
        public double PlayerSizePercent { get; set; }
        public double PlayerSpeed { get; set; }

        // Loaded sprite (optional)
        public Sprite IconSprite { get; set; }
    }
}
