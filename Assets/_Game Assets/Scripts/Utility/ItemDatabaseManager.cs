using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using GRoll.Infrastructure.Persistence;
using UnityEngine;

/// <summary>
/// Legacy facade for ItemService.
/// This static class provides backward compatibility for existing code.
/// New code should inject IItemService directly via DI.
/// </summary>
[Obsolete("Use IItemService via DI instead. This class will be removed in future versions.")]
public static class ItemDatabaseManager
{
    private static IItemService _itemService;
    private static bool _serviceResolved;

    /// <summary>
    /// Sets the ItemService instance. Called by DI container setup.
    /// </summary>
    public static void SetService(IItemService service)
    {
        _itemService = service;
        _serviceResolved = true;
    }

    public static bool IsReady => _itemService?.IsReady ?? false;

    /// <summary>
    /// Ensures ItemDatabaseManager is initialized before accessing items.
    /// Safe to call multiple times.
    /// </summary>
    public static async Task EnsureInitializedAsync()
    {
        if (_itemService == null)
        {
            Debug.LogWarning("[ItemDatabaseManager] Service not injected. Use DI-based IItemService instead.");
            return;
        }

        await _itemService.InitializeAsync();
    }

    /// <summary>
    /// Legacy initialization method. Use EnsureInitializedAsync instead.
    /// </summary>
    public static async Task InitializeAsync()
    {
        await EnsureInitializedAsync();
    }

    // ---- Public API ----

    /// <summary>
    /// ID ile item verisi döner. Bulunamazsa null.
    /// </summary>
    public static ReadableItemData GetItemData(string itemId)
    {
        if (_itemService == null || !_itemService.IsReady)
            return null;

        var item = _itemService.GetItem(itemId);
        return item != null ? ToReadable(item) : null;
    }

    /// <summary>
    /// Tüm itemları enumerate etmek için.
    /// </summary>
    public static IEnumerable<ReadableItemData> GetAllItems()
    {
        if (_itemService == null || !_itemService.IsReady)
            yield break;

        foreach (var item in _itemService.GetAllItems())
        {
            yield return ToReadable(item);
        }
    }

    /// <summary>
    /// Item satin al.
    /// </summary>
    public static async Task<bool> BuyItem(string itemId)
    {
        if (_itemService == null)
        {
            Debug.LogWarning("[ItemDatabaseManager] Service not available");
            return false;
        }

        var result = await _itemService.PurchaseAsync(itemId, PurchaseMethod.GET);
        return result.Success;
    }

    /// <summary>
    /// Item'i ekiple.
    /// </summary>
    public static async Task<bool> EquipItem(string itemId)
    {
        if (_itemService == null)
        {
            Debug.LogWarning("[ItemDatabaseManager] Service not available");
            return false;
        }

        return await _itemService.EquipAsync(itemId);
    }

    /// <summary>
    /// Sahiplik ve equip durumunu sorgula.
    /// </summary>
    public static async Task<(bool owned, bool equipped)> CheckItemOwnershipAndEquipState(string itemId)
    {
        if (_itemService == null)
        {
            Debug.LogWarning("[ItemDatabaseManager] Service not available");
            return (false, false);
        }

        var state = await _itemService.GetOwnershipStateAsync(itemId);
        return (state.Owned, state.Equipped);
    }

    // ---- Mapping ----

    public class ReadableItemData
    {
        public string id;
        public string name;
        public string description;
        public string iconUrl;
        public Sprite iconSprite;

        public double premiumPrice;
        public double getPrice;
        public bool isConsumable;
        public bool isRewardedAd;
        public int referralThreshold;

        public ItemStats stats;

        public override string ToString()
        {
            return $"{id} - {name} | Premium:{premiumPrice} / get:{getPrice} | " +
                   $"consumable:{isConsumable} ad:{isRewardedAd} ref:{referralThreshold} | {stats}";
        }
    }

    public struct ItemStats
    {
        public double coinMultiplierPercent;
        public double comboPower;
        public double gameplaySpeedMultiplierPercent;
        public double magnetPowerPercent;
        public double playerAcceleration;
        public double playerSizePercent;
        public double playerSpeed;

        public override string ToString()
        {
            return $"stats(coin%:{coinMultiplierPercent}, combo:{comboPower}, gSpeed%:{gameplaySpeedMultiplierPercent}, " +
                   $"magnet%:{magnetPowerPercent}, accel:{playerAcceleration}, size%:{playerSizePercent}, pSpeed:{playerSpeed})";
        }
    }

    private static ReadableItemData ToReadable(ItemData item)
    {
        return new ReadableItemData
        {
            id = item.Id,
            name = item.Name ?? string.Empty,
            description = item.Description ?? string.Empty,
            iconUrl = item.IconUrl ?? string.Empty,
            iconSprite = item.IconSprite,

            premiumPrice = item.PremiumPrice,
            getPrice = item.GetPrice,
            isConsumable = item.IsConsumable,
            isRewardedAd = item.IsRewardedAd,
            referralThreshold = item.ReferralThreshold,

            stats = new ItemStats
            {
                coinMultiplierPercent = item.Stats.CoinMultiplierPercent,
                comboPower = item.Stats.ComboPower,
                gameplaySpeedMultiplierPercent = item.Stats.GameplaySpeedMultiplierPercent,
                magnetPowerPercent = item.Stats.MagnetPowerPercent,
                playerAcceleration = item.Stats.PlayerAcceleration,
                playerSizePercent = item.Stats.PlayerSizePercent,
                playerSpeed = item.Stats.PlayerSpeed
            }
        };
    }
}
