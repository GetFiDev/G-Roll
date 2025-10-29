using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

public class ShopItemManager : MonoBehaviour
{
    [Header("Item Prefab")]
    [SerializeField] private UIShopItemDisplay itemPrefab;

    [Header("Tab Roots (assign in Inspector)")]
    [SerializeField] private Transform referralRoot;
    [SerializeField] private Transform coreRoot;
    [SerializeField] private Transform premiumRoot;
    [SerializeField] private Transform bullMarketRoot;

    [Header("Loading Overlay")]
    [SerializeField] private GameObject loadingOverlay;

    private readonly List<UIShopItemDisplay> _spawned = new();
    private bool _isRefreshing;

    private async void OnEnable()
    {
        await RefreshShopAsync();
    }

    public async Task RefreshShopAsync()
    {
        if (_isRefreshing)
        {
            // already running; avoid concurrent refreshes and overlay flicker
            return;
        }
        _isRefreshing = true;

        // Overlay ON
        if (loadingOverlay != null) loadingOverlay.SetActive(true);

        try
        {
            if (itemPrefab == null)
            {
                Debug.LogWarning("[ShopItemManager] itemPrefab not assigned.");
                return;
            }

            // Repo hazır değilse initialize et
            if (!ItemDatabaseManager.IsReady)
                await ItemDatabaseManager.InitializeAsync();

            // Ensure inventory is initialized before spawning cards so ownership is correct on first render
            if (UserInventoryManager.Instance != null && !UserInventoryManager.Instance.IsInitialized)
            {
                await UserInventoryManager.Instance.InitializeAsync();
            }
            else if (UserInventoryManager.Instance == null)
            {
                Debug.LogWarning("[ShopItemManager] UserInventoryManager.Instance is null. Ownership may be incorrect on first render.");
            }

            ClearSpawned();

            var allItems = ItemDatabaseManager.GetAllItems().ToList();

            // --- REFERRAL --- (min referral -> max)
            var referralItems = allItems
                .Where(i => DetermineCategory(i) == ShopCategory.Referral)
                .OrderBy(i => i.referralThreshold)
                .ToList();
            SpawnList(referralItems, ShopCategory.Referral);

            // --- CORE --- (cheapest getPrice -> expensive)
            var coreItems = allItems
                .Where(i => DetermineCategory(i) == ShopCategory.Core)
                .OrderBy(i => i.getPrice)
                .ToList();
            SpawnList(coreItems, ShopCategory.Core);

            // --- PREMIUM --- (cheapest dollarPrice -> expensive)
            var premiumItems = allItems
                .Where(i => DetermineCategory(i) == ShopCategory.Premium)
                .OrderBy(i => i.dollarPrice)
                .ToList();
            SpawnList(premiumItems, ShopCategory.Premium);

            // --- BULL MARKET --- (RewardedAd first -> GetPrice asc -> DollarPrice asc)
            var bullAll = allItems.Where(i => DetermineCategory(i) == ShopCategory.BullMarket).ToList();
            var bullRewarded = bullAll.Where(i => i.isRewardedAd).ToList();
            var bullInGame   = bullAll.Where(i => !i.isRewardedAd && i.getPrice > 0 && i.dollarPrice <= 0)
                                      .OrderBy(i => i.getPrice).ToList();
            var bullDollar   = bullAll.Where(i => !i.isRewardedAd && i.dollarPrice > 0)
                                      .OrderBy(i => i.dollarPrice).ToList();
            var bullItemsOrdered = bullRewarded.Concat(bullInGame).Concat(bullDollar).ToList();
            SpawnList(bullItemsOrdered, ShopCategory.BullMarket);

            // Post-spawn nudge: once inventory is initialized, ask cards to re-evaluate their visual state.
            foreach (var card in _spawned)
            {
                if (card != null)
                {
                    card.RequestRefresh(); // safe: defers if inactive
                }
            }

            Debug.Log($"[ShopItemManager] Spawned: {_spawned.Count}");
        }
        finally
        {
            // Overlay OFF
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
            _isRefreshing = false;
        }
    }

    private Transform GetParentFor(ShopCategory cat)
    {
        switch (cat)
        {
            case ShopCategory.Referral:   return referralRoot;
            case ShopCategory.Core:       return coreRoot;
            case ShopCategory.Premium:    return premiumRoot;
            case ShopCategory.BullMarket: return bullMarketRoot;
        }
        return null;
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();
    }

    /// <summary>
    /// Kurallar (öncelik sırası):
    /// 1) Referral: referralThreshold > 0
    /// 2) BullMarket: isConsumable == true && referralThreshold == 0
    /// 3) Premium: dollarPrice > 0 && getPrice == 0 && referralThreshold == 0 && isConsumable == false && isRewardedAd == false
    /// 4) Core: getPrice > 0 && dollarPrice == 0 && referralThreshold == 0 && isConsumable == false && isRewardedAd == false
    /// (Hiçbirine uymuyorsa logla ve Core'a at.)
    /// </summary>
    private ShopCategory DetermineCategory(ItemDatabaseManager.ReadableItemData d)
    {
        // 1) Referral
        if (d.referralThreshold > 0)
            return ShopCategory.Referral;

        // 2) BullMarket (tüketilebilir, 0 referral)
        if (d.isConsumable && d.referralThreshold == 0)
            return ShopCategory.BullMarket;

        // 3) Premium (real money only)
        if (d.dollarPrice > 0 && d.getPrice <= 0 && d.referralThreshold == 0 && !d.isConsumable && !d.isRewardedAd)
            return ShopCategory.Premium;

        // 4) Core (in-game currency only)
        if (d.getPrice > 0 && d.dollarPrice <= 0 && d.referralThreshold == 0 && !d.isConsumable && !d.isRewardedAd)
            return ShopCategory.Core;

        // Uymayanları uyar ve Core'a düşür (ya da skip etmeyi tercih edebilirsin)
        Debug.LogWarning($"[ShopItemManager] Item '{d.id}' no strict category matched. Fallback -> Core.");
        return ShopCategory.Core;
    }
    private void SpawnList(List<ItemDatabaseManager.ReadableItemData> list, ShopCategory cat)
    {
        var parent = GetParentFor(cat);
        if (parent == null) {
            Debug.LogWarning($"[ShopItemManager] No root for category {cat}");
            return;
        }
        foreach (var item in list)
        {
            var inst = Instantiate(itemPrefab, parent);
            if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);
            // Normalize id at bind-time for consistent naming and optional SetItemId()
            var nid = IdUtil.NormalizeId(item.id);
            inst.gameObject.name = $"ShopCard_{nid}";
            // If UIShopItemDisplay exposes SetItemId(string), this will set; otherwise it's safely ignored
            inst.SendMessage("SetItemId", nid, SendMessageOptions.DontRequireReceiver);
            StartCoroutine(inst.Setup(item, cat));
            _spawned.Add(inst);
        }
    }
}