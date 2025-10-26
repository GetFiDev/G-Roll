using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// RemoteItemService + ItemLocalDatabase üstünden tek kapı.
/// Oyun açılışında InitializeAsync() çağır; sonrası sadece GetItemData/…
/// </summary>
public static class ItemDatabaseManager
{
    // Bellek-içi aktif veri
    private static Dictionary<string, RemoteItemService.ItemData> _items;

    public static bool IsReady => _items != null;

    /// <summary>
    /// 1) Local cache'i anında yükler (offline-friendly)
    /// 2) Arkasından remote'tan taze veri çekip cache'i yeniler (başarırsa)
    /// </summary>
    public static async Task InitializeAsync()
    {
        // 1) Lokal
        _items = ItemLocalDatabase.Load();

        // 2) Remote (best-effort)
        try
        {
            var fetched = await RemoteItemService.FetchAllItemsWithIconsAsync();
            if (fetched != null && fetched.Count > 0)
            {
                _items = fetched;
                ItemLocalDatabase.Save(fetched);
                Debug.Log($"[ItemDatabaseManager] Refreshed {fetched.Count} items from remote.");
            }
            else
            {
                Debug.Log("[ItemDatabaseManager] Remote returned 0 items, using local cache.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ItemDatabaseManager] Remote fetch failed, using local cache. {ex.Message}");
        }
    }

    // ---- Public API ----

    /// <summary>
    /// ID ile item verisi (işlenmiş okunabilir model) döner. Bulunamazsa null.
    /// </summary>
    public static ReadableItemData GetItemData(string itemId)
    {
        if (!IsReady || string.IsNullOrEmpty(itemId) || !_items.TryGetValue(itemId, out var raw))
            return null;

        return ToReadable(itemId, raw);
    }

    /// <summary>
    /// Tüm itemları (işlenmiş) enumerate etmek için.
    /// </summary>
    public static IEnumerable<ReadableItemData> GetAllItems()
    {
        if (!IsReady) yield break;
        foreach (var kv in _items)
            yield return ToReadable(kv.Key, kv.Value);
    }

    /// <summary>
    /// Sunucuda satın alma isteği (TODO: server call)
    /// </summary>
    public static async Task<bool> BuyItem(string itemId)
    {
        // TODO: Cloud Function çağır (buyItem)
        await Task.Yield();
        Debug.Log($"[ItemDatabaseManager] TODO BuyItem({itemId})");
        return false;
    }

    /// <summary>
    /// Sunucuda equip isteği (TODO: server call)
    /// </summary>
    public static async Task<bool> EquipItem(string itemId)
    {
        // TODO: Cloud Function çağır (equipItem)
        await Task.Yield();
        Debug.Log($"[ItemDatabaseManager] TODO EquipItem({itemId})");
        return false;
    }

    /// <summary>
    /// Sunucudan sahiplik/equip state sorgusu (TODO: server call)
    /// </summary>
    public static async Task<(bool owned, bool equipped)> CheckItemOwnershipAndEquipState(string itemId)
    {
        // TODO: Cloud Function çağır (checkOwnershipAndEquip)
        await Task.Yield();
        Debug.Log($"[ItemDatabaseManager] TODO CheckItemOwnershipAndEquipState({itemId})");
        return (false, false);
    }

    // ---- Mapping ----

    public class ReadableItemData
    {
        public string id;
        public string name;
        public string description;
        public string iconUrl;
        public Sprite iconSprite;

        public double dollarPrice;
        public double getPrice;
        public bool isConsumable;
        public bool isRewardedAd;
        public int referralThreshold;

        public ItemStats stats; // oyun içi stat paketi (okunabilir)

        public override string ToString()
        {
            return $"{id} - {name} | ${dollarPrice} / get:{getPrice} | " +
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

    private static ReadableItemData ToReadable(string id, RemoteItemService.ItemData src)
    {
        return new ReadableItemData
        {
            id = id,
            name = src.itemName ?? string.Empty,
            description = src.itemDescription ?? string.Empty,
            iconUrl = src.itemIconUrl ?? string.Empty,
            iconSprite = src.iconSprite,

            dollarPrice = src.itemDollarPrice,
            getPrice = src.itemGetPrice,
            isConsumable = src.itemIsConsumable,
            isRewardedAd = src.itemIsRewardedAd,
            referralThreshold = src.itemReferralThreshold,

            stats = new ItemStats
            {
                coinMultiplierPercent = src.itemstat_coinMultiplierPercent,
                comboPower = src.itemstat_comboPower,
                gameplaySpeedMultiplierPercent = src.itemstat_gameplaySpeedMultiplierPercent,
                magnetPowerPercent = src.itemstat_magnetPowerPercent,
                playerAcceleration = src.itemstat_playerAcceleration,
                playerSizePercent = src.itemstat_playerSizePercent,
                playerSpeed = src.itemstat_playerSpeed
            }
        };
    }
}