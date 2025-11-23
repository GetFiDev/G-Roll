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

    public static bool IsReady => _items != null && _items.Count > 0;

    /// <summary>
    /// 1) Local cache'i anında yükler (offline-friendly)
    /// 2) Arkasından remote'tan taze veri çekip cache'i yeniler (başarırsa)
    /// </summary>
    public static async Task InitializeAsync()
    {
        // 1) Lokal cache'i hemen oku (null dönerse bile sorun yok)
        var local = ItemLocalDatabase.Load();
        Dictionary<string, RemoteItemService.ItemData> result = null;

        // 2) Remote (asıl kaynak) – her açılışta denenecek
        try
        {
            var fetched = await RemoteItemService.FetchAllItemsWithIconsAsync();
            if (fetched != null && fetched.Count > 0)
            {
                result = fetched;
                ItemLocalDatabase.Save(fetched);
                Debug.Log($"[ItemDatabaseManager] Refreshed {fetched.Count} items from remote.");
            }
            else
            {
                Debug.LogError("[ItemDatabaseManager] Remote returned 0 items. This is treated as an error; falling back to local cache if available.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ItemDatabaseManager] Remote fetch failed, falling back to local cache if available. {ex.Message}");
        }

        // 3) Remote başarısızsa ama lokal doluysa local cache'i kullan
        if ((result == null || result.Count == 0) && local != null && local.Count > 0)
        {
            Debug.LogWarning("[ItemDatabaseManager] Using local item cache as fallback.");
            result = local;
        }

        // 4) Sonucu belleğe yaz
        _items = result;

        // 5) Hâlâ boşsa, IsReady false kalacak; shop bir sonraki denemeye kadar item gösteremez
        if (_items == null || _items.Count == 0)
        {
            Debug.LogError("[ItemDatabaseManager] No items available after initialization. Shop will remain empty until a successful fetch.");
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