using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// RemoteItemService + ItemLocalDatabase üstünden tek kapı.
/// Lazy loading destekli - Shop açıldığında InitializeAsync() çağrılır.
/// EnsureInitializedAsync() ile güvenli erişim sağlanır.
/// </summary>
public static class ItemDatabaseManager
{
    // Bellek-içi aktif veri
    private static Dictionary<string, RemoteItemService.ItemData> _items;

    // Initialization state tracking
    private static bool _isInitializing = false;
    private static Task _initTask = null;

    // Cache age tracking for conditional fetches
    private static DateTime _lastRemoteFetchTime = DateTime.MinValue;
    private const int CACHE_MAX_AGE_MINUTES = 60; // Re-fetch from remote if cache older than this

    public static bool IsReady => _items != null && _items.Count > 0;

    /// <summary>
    /// Ensures ItemDatabaseManager is initialized before accessing items.
    /// Safe to call multiple times - will only initialize once.
    /// Use this method when you need items (e.g., when Shop opens).
    /// </summary>
    public static async Task EnsureInitializedAsync()
    {
        // Already initialized
        if (IsReady) return;

        // Initialization in progress - wait for it
        if (_isInitializing && _initTask != null)
        {
            await _initTask;
            return;
        }

        // Start initialization
        await InitializeAsync();
    }

    /// <summary>
    /// 1) Local cache'i anında yükler (offline-friendly)
    /// 2) Conditional fetch: Eğer cache yeterince yeniyse remote'a gitmez
    /// 3) Arkasından remote'tan taze veri çekip cache'i yeniler (başarırsa)
    /// </summary>
    public static async Task InitializeAsync()
    {
        // Prevent concurrent initialization
        if (_isInitializing) return;
        _isInitializing = true;

        try
        {
            _initTask = InitializeInternalAsync();
            await _initTask;
        }
        finally
        {
            _isInitializing = false;
            _initTask = null;
        }
    }

    private static async Task InitializeInternalAsync()
    {
        // 1) Lokal cache'i hemen oku (null dönerse bile sorun yok)
        var local = ItemLocalDatabase.Load();
        Dictionary<string, RemoteItemService.ItemData> result = null;

        // 2) Check if local cache is fresh enough (optimization for repeat sessions)
        bool shouldFetchRemote = true;
        if (local != null && local.Count > 0)
        {
            var cacheAge = DateTime.Now - _lastRemoteFetchTime;
            if (_lastRemoteFetchTime != DateTime.MinValue && cacheAge.TotalMinutes < CACHE_MAX_AGE_MINUTES)
            {
                // Cache is fresh, use it immediately without network call
                Debug.Log($"[ItemDatabaseManager] Using fresh local cache ({cacheAge.TotalMinutes:F0}m old). Skipping remote fetch.");
                _items = local;
                shouldFetchRemote = false;
            }
            else
            {
                // Cache exists but might be stale - use it immediately, fetch in background
                _items = local;
                Debug.Log("[ItemDatabaseManager] Using local cache immediately. Will refresh from remote.");
            }
        }

        // 3) Remote fetch (if needed)
        if (shouldFetchRemote)
        {
            try
            {
                // Changed from FetchAllItemsWithIconsAsync to FetchAllItemsAsync for lazy loading
                var fetched = await RemoteItemService.FetchAllItemsAsync();
                if (fetched != null && fetched.Count > 0)
                {
                    result = fetched;
                    ItemLocalDatabase.Save(fetched);
                    _lastRemoteFetchTime = DateTime.Now;
                    Debug.Log($"[ItemDatabaseManager] Refreshed {fetched.Count} items from remote.");
                }
                else
                {
                    Debug.LogError("[ItemDatabaseManager] Remote returned 0 items. Using local cache if available.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemDatabaseManager] Remote fetch failed: {ex.Message}");
            }

            // 4) Remote başarısızsa ama lokal doluysa local cache'i kullan
            if ((result == null || result.Count == 0) && local != null && local.Count > 0)
            {
                Debug.LogWarning("[ItemDatabaseManager] Using local item cache as fallback.");
                result = local;
            }

            // 5) Sonucu belleğe yaz (remote başarılıysa override et)
            if (result != null && result.Count > 0)
            {
                _items = result;
            }
        }

        // 6) Hâlâ boşsa, IsReady false kalacak
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

        public double premiumPrice;
        public double getPrice;
        public bool isConsumable;
        public bool isRewardedAd;
        public int referralThreshold;

        public ItemStats stats; // oyun içi stat paketi (okunabilir)

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

    private static ReadableItemData ToReadable(string id, RemoteItemService.ItemData src)
    {
        return new ReadableItemData
        {
            id = id,
            name = src.itemName ?? string.Empty,
            description = src.itemDescription ?? string.Empty,
            iconUrl = src.itemIconUrl ?? string.Empty,
            iconSprite = src.iconSprite,

            premiumPrice = src.itemPremiumPrice,
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