using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class ShopItemManager : MonoBehaviour
{
    [Header("Item Prefab")]
    [SerializeField] private UIShopItemDisplay itemPrefab;
    [SerializeField] private UIShopItemDisplay bullMarketItemPrefab;
    [SerializeField] private GameObject diamondArtworkPrefab;

    [Header("Tab Roots (assign in Inspector)")]
    [SerializeField] private Transform myItemsRoot;
    [SerializeField] private Transform referralRoot;
    [SerializeField] private Transform coreRoot;
    [SerializeField] private Transform premiumRoot;
    [SerializeField] private Transform bullMarketRoot;

    [Header("Stats Labels")]
    [SerializeField] private TextMeshProUGUI myBagCountText;         // "x items"
    [SerializeField] private TextMeshProUGUI coreOwnedCountText;     // "owned/total" for Core
    [SerializeField] private TextMeshProUGUI premiumOwnedCountText;  // "owned/total" for Premium
    [SerializeField] private TextMeshProUGUI referralFrensText;      // "x Frens"

    [Header("Loading Overlay")]
    [SerializeField] private GameObject loadingOverlay;

    private int _referralCount;

    private readonly List<UIShopItemDisplay> _spawned = new();
    private readonly List<GameObject> _extraSpawned = new();
    private bool _isRefreshing;

    private async void OnEnable()
    {
        // Subscribe to active-consumable updates to live-refresh countdown/labels
        if (UserInventoryManager.Instance != null)
        {
            UserInventoryManager.Instance.OnActiveConsumablesChanged -= HandleActiveConsumablesChanged;
            UserInventoryManager.Instance.OnActiveConsumablesChanged += HandleActiveConsumablesChanged;
        }

        await RefreshShopAsync();
    }

    private void OnDisable()
    {
        if (UserInventoryManager.Instance != null)
        {
            UserInventoryManager.Instance.OnActiveConsumablesChanged -= HandleActiveConsumablesChanged;
        }
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

            // Lazy loading: ItemDatabaseManager boot sırasında yüklenmez, shop açılınca yüklenir
            await ItemDatabaseManager.EnsureInitializedAsync();
            
            if (!isActiveAndEnabled) return;

            // Ensure inventory is initialized before spawning cards so ownership is correct on first render
            if (UserInventoryManager.Instance != null && !UserInventoryManager.Instance.IsInitialized)
            {
                await UserInventoryManager.Instance.InitializeAsync();
                if (!isActiveAndEnabled) return;
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
                .OrderBy(i => i.premiumPrice)
                .ToList();
            SpawnList(premiumItems, ShopCategory.Premium);

            // --- BULL MARKET --- (RewardedAd first -> GetPrice asc -> DollarPrice asc)
            var bullAll = allItems.Where(i => DetermineCategory(i) == ShopCategory.BullMarket).ToList();
            var bullRewarded = bullAll.Where(i => i.isRewardedAd).ToList();
            var bullInGame   = bullAll.Where(i => !i.isRewardedAd && i.getPrice > 0 && i.premiumPrice <= 0)
                                      .OrderBy(i => i.getPrice).ToList();
            var bullDollar   = bullAll.Where(i => !i.isRewardedAd && i.premiumPrice > 0)
                                      .OrderBy(i => i.premiumPrice).ToList();
            var bullItemsOrdered = bullDollar.Concat(bullInGame).Concat(bullRewarded).ToList();
            SpawnList(bullItemsOrdered, ShopCategory.BullMarket);

            // --- MY ITEMS --- sadece sahip olunan item'ları listele
            SpawnMyItems(allItems);

            // --- LABELS / COUNTS ---

            // Owned counts rely on inventory; if inventory yoksa hepsi 0 kabul edilir.
            int myBagOwned   = CountOwned(allItems);
            int coreTotal    = coreItems.Count;
            int coreOwned    = CountOwned(coreItems);
            int premiumTotal = premiumItems.Count;
            int premiumOwned = CountOwned(premiumItems);
            
            // New: Fetch referral count dynamically if user is logged in
            if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.IsAuthenticated())
            {
                // We await this to ensure label is correct immediately
                _referralCount = await UserDatabaseManager.Instance.GetReferralCountAsync();
            }

            // My Bag: "x items"
            if (myBagCountText != null)
                myBagCountText.text = $"{myBagOwned} items";

            // Core: "owned/total"
            if (coreOwnedCountText != null)
                coreOwnedCountText.text = $"{coreOwned}/{coreTotal}";

            // Premium: "owned/total"
            if (premiumOwnedCountText != null)
                premiumOwnedCountText.text = $"{premiumOwned}/{premiumTotal}";

            // Referral: "x Frens" (value set via SetReferralCount)
            UpdateReferralLabel();

            // Post-spawn nudge: once inventory is initialized, ask cards to re-evaluate their visual state.
            foreach (var card in _spawned)
            {
                if (card != null)
                {
                    card.RequestRefresh(); // safe: defers if inactive
                }
            }

            Debug.Log($"[ShopItemManager] Spawned: {_spawned.Count}");

            // In case active consumables were already available, one more UI nudge
            HandleActiveConsumablesChanged();
        }
        finally
        {
            // Overlay OFF
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// Verilen item listesinde, UserInventoryManager'a göre kaç tanesinin oyuncu tarafından sahiplenildiğini döner.
    /// Inventory hazır değilse 0 döner.
    /// </summary>
    private int CountOwned(IEnumerable<ItemDatabaseManager.ReadableItemData> list)
    {
        if (UserInventoryManager.Instance == null || !UserInventoryManager.Instance.IsInitialized)
            return 0;

        int count = 0;
        foreach (var item in list)
        {
            var nid = IdUtil.NormalizeId(item.id);
            try
            {
                if (UserInventoryManager.Instance.IsOwned(nid))
                    count++;
            }
            catch
            {
                // IsOwned tanımlı değilse veya hata verirse, güvenli tarafta kal
            }
        }
        return count;
    }

    /// <summary>
    /// Called when UserInventoryManager reports active consumables changed.
    /// We avoid a full respawn; instead, ask existing cards to refresh their visual state.
    /// </summary>
    private void HandleActiveConsumablesChanged()
    {
        if (_spawned == null || _spawned.Count == 0) return;
        for (int i = 0; i < _spawned.Count; i++)
        {
            var card = _spawned[i];
            if (card == null) continue;
            // Safe: UIShopItemDisplay implements RequestRefresh() (defers if inactive)
            card.RequestRefresh();
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

        for (int i = 0; i < _extraSpawned.Count; i++)
        {
            if (_extraSpawned[i] != null)
                Destroy(_extraSpawned[i]);
        }
        _extraSpawned.Clear();
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

        // 3) Premium (premium gems only)
        if (d.premiumPrice > 0 && d.getPrice <= 0 && d.referralThreshold == 0 && !d.isConsumable && !d.isRewardedAd)
            return ShopCategory.Premium;

        // 4) Core (in-game currency only - or free fallback)
        if (d.getPrice > 0 && d.premiumPrice <= 0 && d.referralThreshold == 0 && !d.isConsumable && !d.isRewardedAd)
            return ShopCategory.Core;

        // Uymayanları uyar ve Core'a düşür (ya da skip etmeyi tercih edebilirsin)
        Debug.LogWarning($"[ShopItemManager] Item '{d.id}' no strict category matched. Fallback -> Core.");
        return ShopCategory.Core;
    }
    private void SpawnList(List<ItemDatabaseManager.ReadableItemData> list, ShopCategory cat)
    {
        if (!isActiveAndEnabled) return;

        var parent = GetParentFor(cat);
        if (parent == null) {
            Debug.LogWarning($"[ShopItemManager] No root for category {cat}");
            return;
        }

        // Determine which prefab to use
        UIShopItemDisplay prefabToUse = itemPrefab;
        if (cat == ShopCategory.BullMarket && bullMarketItemPrefab != null)
        {
            prefabToUse = bullMarketItemPrefab;
        }

        int indexCounter = 0;
        foreach (var item in list)
        {
            var inst = Instantiate(prefabToUse, parent);
            if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);
            // Normalize id at bind-time for consistent naming and optional SetItemId()
            var nid = IdUtil.NormalizeId(item.id);
            inst.gameObject.name = $"ShopCard_{nid}";
            // If UIShopItemDisplay exposes SetItemId(string), this will set; otherwise it's safely ignored
            inst.SendMessage("SetItemId", nid, SendMessageOptions.DontRequireReceiver);
            if (isActiveAndEnabled) StartCoroutine(inst.Setup(item, cat));
            _spawned.Add(inst);

            // Inject Diamond Artwork Button after the 1st item (index 0) is spawned, so it becomes index 1
            // Only for Premium ('Pro') and BullMarket
            if (indexCounter == 0 && diamondArtworkPrefab != null)
            {
                if (cat == ShopCategory.Premium || cat == ShopCategory.BullMarket)
                {
                    var diamondBtn = Instantiate(diamondArtworkPrefab, parent);
                    if (!diamondBtn.activeSelf) diamondBtn.SetActive(true);
                    diamondBtn.name = "DiamondArtworkButton";
                    
                    // Directly add listener to the button component since the user confirmed it's a button
                    var btn = diamondBtn.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => {
                            if (UIManager.Instance != null) UIManager.Instance.ShowIAPShop();
                        });
                    }

                    _extraSpawned.Add(diamondBtn);
                }
            }
            indexCounter++;
        }
    }
    private void SpawnMyItems(List<ItemDatabaseManager.ReadableItemData> list)
    {
        if (!isActiveAndEnabled) return;

        if (myItemsRoot == null)
        {
            Debug.LogWarning("[ShopItemManager] myItemsRoot is null. Cannot spawn My Items cards.");
            return;
        }

        // Sadece kullanıcıya ait (sahip olduğu) item'lar listelensin
        IEnumerable<ItemDatabaseManager.ReadableItemData> source = list;

        if (UserInventoryManager.Instance != null && UserInventoryManager.Instance.IsInitialized)
        {
            source = list.Where(item =>
            {
                var nid = IdUtil.NormalizeId(item.id);
                // NOTE: Burada UserInventoryManager içinde IsOwned(string normalizedId) benzeri
                // bir API olduğu varsayılıyor. Eğer farklı isimdeyse kendi metoduna uyarlayabilirsin.
                try
                {
                    return UserInventoryManager.Instance.IsOwned(nid);
                }
                catch
                {
                    // IsOwned yoksa ya da hata verirse, güvenli tarafta kalıp false döndürelim.
                    return false;
                }
            });
        }
        else
        {
            // Inventory hazır değilken My Items doldurmak mantıklı değil; hiç spawn etmeyelim.
            Debug.LogWarning("[ShopItemManager] UserInventoryManager not initialized; My Items will be empty.");
            return;
        }

        foreach (var item in source)
        {
            var inst = Instantiate(itemPrefab, myItemsRoot);
            if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);

            var nid = IdUtil.NormalizeId(item.id);
            inst.gameObject.name = $"MyItemCard_{nid}";
            inst.SendMessage("SetItemId", nid, SendMessageOptions.DontRequireReceiver);

            // My Items sekmesinde de item'in kendi kategorisine göre Setup çalışsın
            var cat = DetermineCategory(item);
            if (isActiveAndEnabled) StartCoroutine(inst.Setup(item, cat));

            _spawned.Add(inst);
        }
    }

    /// <summary>
    /// Dış bir sistem (ReferralService vb.) referral sayısını güncellediğinde çağrılır.
    /// </summary>
    public void SetReferralCount(int count)
    {
        _referralCount = Mathf.Max(0, count);
        UpdateReferralLabel();
    }

    private void UpdateReferralLabel()
    {
        if (referralFrensText != null)
            referralFrensText.text = $"{_referralCount} Frens";
    }
}