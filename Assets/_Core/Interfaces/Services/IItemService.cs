using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Item/Shop service interface.
    /// Item veritabani, satin alma ve envanter yonetimi.
    /// </summary>
    public interface IItemService
    {
        /// <summary>
        /// Servis hazir mi?
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Servisi baslat ve itemlari yukle.
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// ID ile item verisi doner.
        /// </summary>
        ItemData GetItem(string itemId);

        /// <summary>
        /// Tum itemlari doner.
        /// </summary>
        IReadOnlyList<ItemData> GetAllItems();

        /// <summary>
        /// Item'in sahiplik durumunu kontrol et.
        /// </summary>
        UniTask<ItemOwnershipState> GetOwnershipStateAsync(string itemId);

        /// <summary>
        /// Item satin al.
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <param name="method">Satin alma yontemi: GET, PREMIUM, AD</param>
        /// <param name="adToken">AD icin reklam tokeni (opsiyonel)</param>
        UniTask<ItemPurchaseResult> PurchaseAsync(string itemId, PurchaseMethod method, string adToken = null);

        /// <summary>
        /// Item'i ekiple (equip).
        /// </summary>
        UniTask<bool> EquipAsync(string itemId);

        /// <summary>
        /// Item'i cikar (unequip).
        /// </summary>
        UniTask<bool> UnequipAsync(string itemId);

        /// <summary>
        /// Sahip olunan item ID'lerini doner.
        /// </summary>
        UniTask<List<string>> GetOwnedItemIdsAsync();

        /// <summary>
        /// Equipped item ID'lerini doner.
        /// </summary>
        UniTask<List<string>> GetEquippedItemIdsAsync();

        /// <summary>
        /// Aktif consumable'lari doner.
        /// </summary>
        UniTask<List<ActiveConsumable>> GetActiveConsumablesAsync();
    }

    /// <summary>
    /// Item verisi
    /// </summary>
    public class ItemData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public Sprite IconSprite { get; set; }

        public double PremiumPrice { get; set; }
        public double GetPrice { get; set; }
        public bool IsConsumable { get; set; }
        public bool IsRewardedAd { get; set; }
        public int ReferralThreshold { get; set; }

        public ItemStats Stats { get; set; }
    }

    /// <summary>
    /// Item stat paketi
    /// </summary>
    public struct ItemStats
    {
        public double CoinMultiplierPercent { get; set; }
        public double ComboPower { get; set; }
        public double GameplaySpeedMultiplierPercent { get; set; }
        public double MagnetPowerPercent { get; set; }
        public double PlayerAcceleration { get; set; }
        public double PlayerSizePercent { get; set; }
        public double PlayerSpeed { get; set; }
    }

    /// <summary>
    /// Item sahiplik durumu
    /// </summary>
    public struct ItemOwnershipState
    {
        public bool Owned { get; set; }
        public bool Equipped { get; set; }
        public bool IsConsumable { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Satin alma yontemleri
    /// </summary>
    public enum PurchaseMethod
    {
        /// <summary>Normal currency ile</summary>
        GET,
        /// <summary>Premium currency ile</summary>
        PREMIUM,
        /// <summary>Reklam izleyerek</summary>
        AD
    }

    /// <summary>
    /// Satin alma sonucu
    /// </summary>
    public struct ItemPurchaseResult
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

    /// <summary>
    /// Aktif consumable
    /// </summary>
    public struct ActiveConsumable
    {
        public string ItemId { get; set; }
        public bool Active { get; set; }
        public long? ExpiresAtMillis { get; set; }
    }
}
