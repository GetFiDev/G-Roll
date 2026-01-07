using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Linq;

public enum ShopCategory
{
    Referral,
    Core,
    Premium,
    BullMarket
}

/// <summary>
/// Tek bir shop kartını doldurur. (Prefab'a ekleyin)
/// Tüm veri ItemDatabaseManager.ReadableItemData + ShopCategory üzerinden gelir.
/// </summary>
public class UIShopItemDisplay : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;

    [Header("Action Button (New)")]
    [SerializeField] private Button actionButton;           // İç buton
    [SerializeField] private TMP_Text actionLabel;          // Buton üzerindeki metin
    [SerializeField] private Image actionIcon;              // Buton üzerindeki ikon

    [Header("Stat Chips")]
    [SerializeField] private Transform statChipsRoot;
    [SerializeField] private GameObject statChipPrefab; // İçinde: Image icon, TMP_Text value

    [Header("Stat Icons")]
    [SerializeField] private Sprite iconCoinPercent;
    [SerializeField] private Sprite iconComboPower;
    [SerializeField] private Sprite iconGameplaySpeedPercent;
    [SerializeField] private Sprite iconMagnetPercent;
    [SerializeField] private Sprite iconAcceleration;
    [SerializeField] private Sprite iconPlayerSizePercent;
    [SerializeField] private Sprite iconPlayerSpeed;

    [Header("Action Icons")]
    [SerializeField] private Sprite iconCurrency;  // GET para ikonu
    [SerializeField] private Sprite iconPremium;   // Premium (Gem) ikonu
    [SerializeField] private Sprite iconEquip;     // Equip ikonu
    [SerializeField] private Sprite iconUnequip;   // Unequip ikonu
    [SerializeField] private Sprite iconReferral;  // Referral ikonu
    [SerializeField] private Sprite iconAd;        // Reklam ikonu

    [Header("Fetching Overlay")]
    [SerializeField] private GameObject fetchingPanel; // İşlem sırasında butonun üstünü kapatan panel



    [Header("Background Sprites")]
    [SerializeField] private Sprite bgNormalNotOwned;
    [SerializeField] private Sprite bgNormalOwned;
    [SerializeField] private Sprite bgNormalEquipped;
    [SerializeField] private Sprite bgPremiumNotOwned;
    [SerializeField] private Sprite bgPremiumOwned;
    [SerializeField] private Sprite bgPremiumEquipped;
    [SerializeField] private Sprite bgReferralLocked;

    [SerializeField] private Image backgroundImage;

    private Coroutine _blinkCoroutine;
    private int _cachedReferralCount = 0;
    private bool _buyInProgress = false;
    private bool _pendingRefresh; // if true, run refresh on OnEnable
    private Coroutine _countdownCo;
    private bool _isConsumable; // BullMarket kartları consumable olarak kabul ediliyor

    // Icon için: orijinal sprite'ı sakla ve grayscale versiyonlarını cache'le
    private Sprite _originalIconSprite;


    [Header("Status Feedback")]
    [SerializeField] private TMP_Text statusLabel;    // (opsiyonel) buton yakınında küçük metin
    [SerializeField] private float statusDuration = 1.8f; // saniye

    private Coroutine _statusRoutine;                 // çalışan status coroutine

    private void Awake()
    {
        // Runtime fix: If statChipsRoot points to a prefab asset (not a child of this instance),
        // try to find the correct child by name within this instance.
        if (statChipsRoot != null && !statChipsRoot.IsChildOf(transform))
        {
            var found = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == statChipsRoot.name && t.IsChildOf(transform));

            if (found != null)
            {
                Debug.LogWarning($"[UIShopItemDisplay] Auto-fixed broken 'statChipsRoot' reference on '{name}'.");
                statChipsRoot = found;
            }
            else
            {
                Debug.LogError($"[UIShopItemDisplay] 'statChipsRoot' on '{name}' points to an asset/external object and could not be auto-fixed!");
            }
        }
    }

    private void OnEnable()
    {
        if (UserInventoryManager.Instance != null)
            UserInventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;

        // Active consumable event
        if (UserInventoryManager.Instance != null)
        {
            UserInventoryManager.Instance.OnActiveConsumablesChanged -= HandleActiveConsumablesChanged;
            UserInventoryManager.Instance.OnActiveConsumablesChanged += HandleActiveConsumablesChanged;
        }

        // Yeni iç butonun click'ini Shop mantığına bağla
        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnClickBuy);
        }
        // Overlay ilk açılışta kapalı olsun
        if (fetchingPanel != null) fetchingPanel.SetActive(false);


        // If already active at enable, ensure countdown starts
        TryStartCountdown();

        // If this card was inactive when a refresh was requested, do it now safely
        if (_pendingRefresh)
        {
            _pendingRefresh = false;
            SendMessage("RefreshVisualState", SendMessageOptions.DontRequireReceiver);
            StartCoroutine(_RequestRefreshCo()); // 1-frame delayed second pass
        }
    }

    private void OnDisable()
    {
        if (UserInventoryManager.Instance != null)
        {
            UserInventoryManager.Instance.OnActiveConsumablesChanged -= HandleActiveConsumablesChanged;
        }
        StopCountdown();
        if (actionButton != null)
            actionButton.onClick.RemoveAllListeners();
        if (UserInventoryManager.Instance != null)
            UserInventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged()
    {
        // Inventory değiştiğinde tek kartı güncelle; inaktifse ertele
        if (Data == null) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _pendingRefresh = true;
            return;
        }
        StartCoroutine(RefreshVisualStateCoroutine());
    }
    // Called via SendMessage from RequestRefresh or external callers
    public void RefreshVisualState()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _pendingRefresh = true; // run on next OnEnable
            return;
        }
        StartCoroutine(RefreshVisualStateCoroutine());
    }

    private enum ShopVisualState
    {
        Normal_NotOwned,
        Normal_Owned,
        Normal_Equipped,
        Premium_NotOwned,
        Premium_Owned,
        Premium_Equipped,
        Referral_Locked
    }

    public ItemDatabaseManager.ReadableItemData Data { get; private set; }
    public ShopCategory Category { get; private set; }

    public IEnumerator Setup(ItemDatabaseManager.ReadableItemData data, ShopCategory category)
    {
        Debug.Log($"[UIShopItemDisplay] Setup called | goActive={gameObject.activeInHierarchy} enabled={enabled} name={data?.name ?? "<null>"}");

        Data = data;
        Category = category;
        _isConsumable = (Category == ShopCategory.BullMarket);

        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        // ---- Header ----
        nameText.text = data?.name ?? "Unnamed";
        descriptionText.text = data?.description ?? "";

        // Icon
        if (data?.iconSprite != null)
        {
            _originalIconSprite = data.iconSprite;
            iconImage.sprite = data.iconSprite;
            
            // If already loaded, show immediately fully opaque
            var col = iconImage.color;
            col.a = 1f;
            iconImage.color = col;
            iconImage.enabled = true;
        }
        else if (data != null && !string.IsNullOrEmpty(data.iconUrl))
        {
            // LAZY LOAD:
            // 1. Hide image initially (transparent)
            _originalIconSprite = null;
            iconImage.sprite = null;
            
            var col = iconImage.color;
            col.a = 0f; // Start transparent
            iconImage.color = col;
            iconImage.enabled = true; // Enabled but invisible

            // 2. Start download & fade-in routine
            StartCoroutine(DownloadImageAndFadeIn(data));
        }
        else
        {
            _originalIconSprite = null;
            iconImage.sprite = null;
            _blinkCoroutine = StartCoroutine(BlinkWhileNoIcon());
        }

        // ---- Price / Category Badge ----
        // priceText.text = BuildPriceLabel(data, category); // Price is now shown on the action button

        // ---- Stat Chips ----
        Clear(statChipsRoot);
        if (data != null) BuildStatChips(data);

        // Determine ownership, equipped and referral count
        bool owned = false;
        bool equipped = false;
        int referralCount = 0;

        if (UserInventoryManager.Instance != null && data != null)
        {
            var nid = IdUtil.NormalizeId(data.id);
            owned = UserInventoryManager.Instance.IsOwned(nid);
            equipped = UserInventoryManager.Instance.IsEquipped(nid);
        }

        if (UserDatabaseManager.Instance != null)
        {
            var task = UserDatabaseManager.Instance.GetReferralCountAsync();
            while (!task.IsCompleted)
                yield return null;
            if (task.Exception == null)
                referralCount = task.Result;
            else
                Debug.LogWarning($"[UIShopItemDisplay] Failed to get referral count: {task.Exception}");
        }

        _cachedReferralCount = referralCount;
        var visualState = DetermineVisualState(data, category, owned, equipped, referralCount);
        ApplyVisualState(visualState);
    }

    private ShopVisualState DetermineVisualState(ItemDatabaseManager.ReadableItemData data, ShopCategory category, bool owned, bool equipped, int referralCount)
    {
        // Referral: threshold karşılanmadıysa kilitli, karşılandıysa owned/equipped gibi davran
        if (category == ShopCategory.Referral)
        {
            if (data != null && referralCount < data.referralThreshold)
            {
                // Threshold henüz karşılanmadı → kilitli
                return ShopVisualState.Referral_Locked;
            }
            else
            {
                // Threshold karşılandı → normal item gibi owned/equipped state kullanalım
                if (equipped) return ShopVisualState.Normal_Equipped;
                if (owned) return ShopVisualState.Normal_Owned;
                
                // Threshold met BUT not owned -> Show as NotOwned (which will trigger "Claim" button)
                return ShopVisualState.Normal_NotOwned;
            }
        }

        // Premium görseli gereken durumlar:
        // 1) Kategori Premium ise
        // 2) Kategori BullMarket ama item premium parayla alınıyorsa (premiumPrice > 0)
        bool shouldUsePremiumVisual = (category == ShopCategory.Premium)
                                      || (category == ShopCategory.BullMarket && data != null && data.premiumPrice > 0);

        if (shouldUsePremiumVisual)
        {
            if (equipped) return ShopVisualState.Premium_Equipped;
            if (owned)    return ShopVisualState.Premium_Owned;
            return ShopVisualState.Premium_NotOwned;
        }

        // Geri kalan her şey Normal (Core ve BullMarket'ın $ olmayan versiyonları)
        if (equipped) return ShopVisualState.Normal_Equipped;
        if (owned)    return ShopVisualState.Normal_Owned;
        return ShopVisualState.Normal_NotOwned;
    }

    private void ApplyVisualState(ShopVisualState state)
    {
        if (backgroundImage == null) return;

        // Eski priceText artık ayrı gösterilmeyecek; metin buton üzerine yazılacak
        if (priceText != null) priceText.gameObject.SetActive(false);
        // Yeni action buton aktif
        if (actionButton != null) actionButton.gameObject.SetActive(true);

        if (iconImage != null) iconImage.enabled = true;

        switch (state)
        {
            case ShopVisualState.Normal_NotOwned:
                backgroundImage.sprite = bgNormalNotOwned;
                break;
            case ShopVisualState.Normal_Owned:
                backgroundImage.sprite = bgNormalOwned;
                break;
            case ShopVisualState.Normal_Equipped:
                backgroundImage.sprite = bgNormalEquipped;
                break;
            case ShopVisualState.Premium_NotOwned:
                backgroundImage.sprite = bgPremiumNotOwned;
                break;
            case ShopVisualState.Premium_Owned:
                backgroundImage.sprite = bgPremiumOwned;
                break;
            case ShopVisualState.Premium_Equipped:
                backgroundImage.sprite = bgPremiumEquipped;
                break;
            case ShopVisualState.Referral_Locked:
                backgroundImage.sprite = bgReferralLocked;
                if (nameText != null) nameText.text = Data?.name ?? "Unnamed";
                break;
        }

        if (iconImage != null)
        {
            iconImage.enabled = true;

            // Shader kullanmadan grayscale göster: referral kilitliyken,
            // sprite'ın grayscale kopyasını kullan, diğer durumda orijinal sprite'a dön.
            if (_originalIconSprite != null)
            {
                iconImage.sprite = _originalIconSprite;
            }
        }

        UpdateActionButton(state);
        // Maintain countdown lifecycle depending on active state
        if (TryStartCountdown() == false)
            StopCountdown();
        if (fetchingPanel != null && !_buyInProgress)
            fetchingPanel.SetActive(false);
    }

    private void UpdateActionButton(ShopVisualState state)
    {
        if (actionButton == null) return;

        string nidForActive = (Data != null) ? IdUtil.NormalizeId(Data.id) : null;
        bool isActiveConsumable = _isConsumable && nidForActive != null && UserInventoryManager.Instance != null
                                  && UserInventoryManager.Instance.IsConsumableActive(nidForActive);

        // Varsayılan etkileşim açık; özel durumlarda kapatacağız
        actionButton.interactable = true;

        // Mevcut sahiplik/equip durumu
        bool owned = false;
        bool equipped = false;
        var d = Data;
        if (UserInventoryManager.Instance != null && d != null)
        {
            var nid = IdUtil.NormalizeId(d.id);
            owned = UserInventoryManager.Instance.IsOwned(nid);
            equipped = UserInventoryManager.Instance.IsEquipped(nid);
        }

        // If this is a consumable and currently active, force the button into 'Active • countdown' mode
        if (isActiveConsumable)
        {
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(true);
                actionButton.interactable = false;
            }
            if (actionIcon != null) actionIcon.gameObject.SetActive(false); // no icon for active state
            if (actionLabel != null)
            {
                var remain = UserInventoryManager.Instance.GetConsumableRemaining(nidForActive);
                actionLabel.gameObject.SetActive(true);
                actionLabel.text = FormatRemain(remain);
            }
            return;
        }

        string label = string.Empty;
        Sprite icon = null;

        // Referral sayısı
        int referralCount = _cachedReferralCount;

        // BullMarket + Ad kontrolünü d.id üzerinden yapıyoruz (Data null ise zaten aşağıdaki case'ler çalışmayacak)
        bool isAdConsumable = (Category == ShopCategory.BullMarket && d != null && d.isRewardedAd);

        switch (state)
        {
            case ShopVisualState.Referral_Locked:
                // 2/3 gibi
                int target = d != null ? d.referralThreshold : 0;
                label = $"{referralCount}/{target}";
                icon = iconReferral;
                // Kilitli halde butona basılmasını engelleyebiliriz
                // Kilitli halde butona basılmasını engelleyebiliriz -> Artık basılsın istiyoruz
                actionButton.interactable = true;
                break;

            case ShopVisualState.Normal_NotOwned:
            case ShopVisualState.Premium_NotOwned:
                if (isAdConsumable)
                {
                    label = "Watch Ad";
                    icon = iconAd;
                }
                else if (Category == ShopCategory.Premium || (Category == ShopCategory.BullMarket && d != null && d.premiumPrice > 0))
                {
                    // Premium (Gem) fiyat
                    if (d != null) label = d.premiumPrice.ToString("F0"); // Genelde tam sayı
                    icon = iconPremium; 
                }
                else
                {
                    // GET fiyat
                    if (d != null) label = d.getPrice.ToString("F2");
                    icon = iconCurrency;
                }
                
                // Override for Referral items that are unlocked but not owned
                if (Category == ShopCategory.Referral)
                {
                    label = "Claim";
                    icon = null; // Or use iconReferral if desired, but usually "Claim" implies free/action
                }
                break;

            case ShopVisualState.Normal_Owned:
            case ShopVisualState.Premium_Owned:
                label = "Equip";
                icon = iconEquip;
                break;

            case ShopVisualState.Normal_Equipped:
            case ShopVisualState.Premium_Equipped:
                label = "Unequip";
                icon = iconUnequip;
                break;
        }

        // Force-enable the label GameObject
        if (actionLabel != null) actionLabel.gameObject.SetActive(true);

        // UI'ya uygula
        if (actionLabel != null) actionLabel.text = label ?? string.Empty;
        if (actionIcon != null)
        {
            if (icon != null)
            {
                actionIcon.sprite = icon;
                actionIcon.gameObject.SetActive(true);
            }
            else
            {
                actionIcon.gameObject.SetActive(false);
            }
        }
    }

    private string BuildPriceLabel(ItemDatabaseManager.ReadableItemData d, ShopCategory cat)
    {
        switch (cat)
        {
            case ShopCategory.Referral:
                // Tamamen referral ile kazanılır
                return $"Invite {Mathf.Max(1, d.referralThreshold)}";

            case ShopCategory.Core:
                // Oyun içi para
                return d.getPrice > 0 ? $"{d.getPrice:F2} GET" : "GET 0";

            case ShopCategory.Premium:
                // Premium para
                return d.premiumPrice > 0 ? d.premiumPrice.ToString("F0") : "0";

            case ShopCategory.BullMarket:
                // Tüketilebilir; 3 yöntemin herhangi biri olabilir (etiketi basit tut)
                if (d.isRewardedAd) return "Watch Ad";
                if (d.premiumPrice > 0) return d.premiumPrice.ToString("F0");
                if (d.getPrice > 0) return $"{d.getPrice:F2} GET";
                return "Consumable";
        }
        return "-";
    }

    private void BuildStatChips(ItemDatabaseManager.ReadableItemData d)
    {
        var s = d.stats;

        // Sıfır OLMAYAN statlar gösterilir
        AddPercentChipIfNonZero(iconCoinPercent, s.coinMultiplierPercent, "%");
        AddValueChipIfNonZero(iconComboPower, s.comboPower); // yüzdelik değil
        AddPercentChipIfNonZero(iconGameplaySpeedPercent, s.gameplaySpeedMultiplierPercent, "%");
        AddPercentChipIfNonZero(iconMagnetPercent, s.magnetPowerPercent, "%");
        AddValueChipIfNonZero(iconAcceleration, s.playerAcceleration); // float
        AddPercentChipIfNonZero(iconPlayerSizePercent, s.playerSizePercent, "%");
        AddValueChipIfNonZero(iconPlayerSpeed, s.playerSpeed); // float
    }

    private void AddPercentChipIfNonZero(Sprite icon, double value, string suffix)
    {
        if (Mathf.Approximately((float)value, 0f)) return;
        string txt = $"{Signed(value)}{suffix}";
        SpawnChip(icon, txt);
    }

    private void AddValueChipIfNonZero(Sprite icon, double value)
    {
        if (Mathf.Approximately((float)value, 0f)) return;
        string txt = $"{Signed(value)}";
        SpawnChip(icon, txt);
    }

    private string Signed(double v)
    {
        // +/− işaretli ve 1 ondalık (örn: +20.0, -0.5)
        return (v > 0 ? "+" : "") + v.ToString("0.0");
    }

    private void SpawnChip(Sprite icon, string text)
    {
        if (statChipPrefab == null || statChipsRoot == null) return;

        var go = Instantiate(statChipPrefab, statChipsRoot);

        var imgs = go.GetComponentsInChildren<Image>(true);
        Image img = null;
        foreach (var i in imgs)
        {
            if (i.gameObject != go)
            {
                img = i;
                break;
            }
        }

        var tmps = go.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text tmp = null;
        foreach (var t in tmps)
        {
            if (t.gameObject != go)
            {
                tmp = t;
                break;
            }
        }

        if (img != null) img.sprite = icon;
        if (tmp != null) tmp.text = text;
    }

    private IEnumerator BlinkWhileNoIcon()
    {
        const float speed = 2.5f;
        while (iconImage.sprite == null)
        {
            if (iconImage != null)
            {
                float alpha = Mathf.Lerp(0.3f, 0.7f, Mathf.PingPong(Time.unscaledTime * speed, 1f));
                var col = iconImage.color; col.a = alpha; iconImage.color = col;
            }
            yield return null;
        }
        if (iconImage != null)
        {
            var finalCol = iconImage.color; finalCol.a = 1f; iconImage.color = finalCol;
        }
        _blinkCoroutine = null;
    }

    private IEnumerator DownloadImageAndFadeIn(ItemDatabaseManager.ReadableItemData data)
    {
        // Safety check
        if (string.IsNullOrEmpty(data.iconUrl)) yield break;

        // Download (this is async but we wrap in coroutine by waiting on Task)
        var task = RemoteItemService.DownloadTextureAsync(data.iconUrl);
        while (!task.IsCompleted) yield return null;

        if (task.Exception != null || task.Result == null)
        {
            // Fail silently or maybe show placeholder
            Debug.LogWarning($"[UIShopItemDisplay] Failed to load icon for {data.id}");
            // Optional: fallback to blink or default?
            yield break;
        }

        var texture = task.Result;
        var sprite = RemoteItemService.CreateSprite(texture);

        // Cache it on the data object so next time it's instant
        data.iconSprite = sprite;
        _originalIconSprite = sprite;

        // Assign to UI
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            
            // Fade In Effect: 0 -> 1 over 0.5s
            float duration = 0.5f;
            float elapsed = 0f;
            Color c = iconImage.color;
            c.a = 0f;
            iconImage.color = c;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = t;
                if (iconImage != null) iconImage.color = c;
                yield return null;
            }
            
            // Ensure full alpha at end
            c.a = 1f;
            if (iconImage != null) iconImage.color = c;
        }
    }

    private void Clear(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    /// <summary>
    /// UI Button callback. Kategorisine ve item verisine göre uygun satın alma/equip metodunu çağırır.
    /// </summary>
    public void OnClickBuy()
    {
        if (_buyInProgress) return;
        if (fetchingPanel != null) fetchingPanel.SetActive(true);
        if (Data == null)
        {
            Debug.LogWarning("[UIShopItemDisplay] OnClickBuy but Data is null.");
            if (fetchingPanel != null) fetchingPanel.SetActive(false);
            return;
        }

        var nid = IdUtil.NormalizeId(Data.id);
        var inv = UserInventoryManager.Instance;
        if (inv == null)
        {
            if (fetchingPanel != null) fetchingPanel.SetActive(false);
            return;
        }

        bool owned = inv.IsOwned(nid);
        bool equipped = inv.IsEquipped(nid);

        // Referral tabında SATIN ALMA yok, ama sahip olunan referral item equip/unequip edilebilir.
        if (Category == ShopCategory.Referral)
        {
            if (!owned)
            {
               // Check if locked
               var currentRefCount = _cachedReferralCount;
               if (currentRefCount < Data.referralThreshold)
               {
                   Debug.Log("[UIShopItemDisplay] Referral item is locked. Unlock via referrals.");
                   
                   var mainMenu = FindObjectOfType<UIMainMenu>();
                   if (mainMenu != null)
                   {
                       mainMenu.ShowPanel(UIMainMenu.PanelType.Referral);
                   }

                   if (fetchingPanel != null) fetchingPanel.SetActive(false);
                   return;
               }
               
               // If unlocked (referral count met) but not owned, proceed to claiming (BuyRoutine)
            }

            // Referral item zaten envanterdeyse normal toggle davranışı uygula
            if (owned)
            {
                StartCoroutine(ToggleEquipRoutine(inv, nid, equipped));
                return;
            }
            
            // Unlocked but not owned -> Claim it via BuyRoutine
            StartCoroutine(BuyRoutine());
            return;
        }

        // Eğer zaten sahipsek toggle davranışı uygula
        if (owned)
        {
            // coroutine başlat; equip/unequip asenkron yapılacak
            StartCoroutine(ToggleEquipRoutine(inv, nid, equipped));
            return;
        }

        // Sahip değilse satın alma sürecine devam et
        StartCoroutine(BuyRoutine());
    }
    private IEnumerator ToggleEquipRoutine(UserInventoryManager inv, string itemId, bool currentlyEquipped)
    {
        _buyInProgress = true;
        if (fetchingPanel != null) fetchingPanel.SetActive(true);
        if (actionButton != null) actionButton.interactable = false;

        ShowStatus(currentlyEquipped ? "Unequipping..." : "Equipping...");

        var task = currentlyEquipped ? inv.UnequipAsync(itemId) : inv.EquipAsync(itemId);
        while (!task.IsCompleted)
            yield return null;

        if (task.Exception != null)
        {
            Debug.LogWarning($"[UIShopItemDisplay] Toggle equip EX: {task.Exception.Message}");
            ShowStatus("Action failed!");
            if (fetchingPanel != null) fetchingPanel.SetActive(false);
            if (actionButton != null) actionButton.interactable = true;
            _buyInProgress = false;
            yield break;
        }

        bool ok = task.Result;
        if (!ok)
        {
            ShowStatus("Action failed!");
            if (fetchingPanel != null) fetchingPanel.SetActive(false);
        }
        else
        {
            ShowStatus(currentlyEquipped ? "Unequipped!" : "Equipped!");
        }

        // UI'yi güvenli şekilde iki aşamalı tazele (hemen + 1 frame sonra)
        yield return null;
        RequestRefresh();

        if (fetchingPanel != null) fetchingPanel.SetActive(false);
        _buyInProgress = false;
        if (actionButton != null) actionButton.interactable = true;
    }

    private IEnumerator BuyRoutine()
    {
        _buyInProgress = true;
        if (fetchingPanel != null) fetchingPanel.SetActive(true);
        if (actionButton != null) actionButton.interactable = false;

        // Satın alma yöntemi seçimi
        UserInventoryManager.PurchaseMethod method;
        if (Category == ShopCategory.Premium || (Category == ShopCategory.BullMarket && Data.premiumPrice > 0))
            method = UserInventoryManager.PurchaseMethod.Premium;
        else if (Data.isRewardedAd)
            method = UserInventoryManager.PurchaseMethod.Ad;
        else
            method = UserInventoryManager.PurchaseMethod.Get;

        // --- NEW: Spend FX Logic ---
        // "eğer para yetiyorsa o itemi almaya; normal satın alma akışı başlayacak. 
        // bu normal satın alma akışı ile birlikte ui top panelde referansını ekleyeceğimiz uiparticleların attactionu satın alınan itemin sarın alma butonuna set edilecek ve playe basılacak."
        bool canAffordLocal = true;
        bool isPremium = (method == UserInventoryManager.PurchaseMethod.Premium);
        
        // Ads don't cost currency, so no spend FX
        if (method != UserInventoryManager.PurchaseMethod.Ad)
        {
            if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
            {
                var ud = UserDatabaseManager.Instance.currentUserData;
                double cost = isPremium ? Data.premiumPrice : Data.getPrice;
                
                // Check balance
                double balance = isPremium ? ud.premiumCurrency : ud.currency;
                
                if (balance < cost)
                {
                    canAffordLocal = false;
                }
            }
        }

        if (canAffordLocal && method != UserInventoryManager.PurchaseMethod.Ad)
        {
            if (UITopPanel.Instance != null && UITopPanel.Instance.statsDisplayer != null && actionButton != null)
            {
                // Play particle flowing TO the button
                UITopPanel.Instance.statsDisplayer.PlaySpendingParticle(isPremium, actionButton.transform);
            }
        }

        // Yeni API: token/receipt göndermiyoruz; UserInventoryManager sunucu yanıtını normalize ediyor
        ShowStatus("Purchase in progress...");
        var task = UserInventoryManager.Instance.TryPurchaseItemAsync(Data.id, method);
        while (!task.IsCompleted)
            yield return null;

        if (task.Exception != null)
        {
            Debug.LogWarning($"[UIShopItemDisplay] Purchase EX for {Data.id}: {task.Exception.Message}");
            ShowStatus("Purchase failed!");
            if (fetchingPanel != null) fetchingPanel.SetActive(false);
            if (actionButton != null) actionButton.interactable = true;
            _buyInProgress = false;
            yield break;
        }

        var result = task.Result; // PurchaseResult beklenir (ok/alreadyOwned bilgisi içerir)
        bool success = result.ok || result.alreadyOwned;
        if (success)
        {
            ShowStatus("Purchase done!");
        }
        else
        {
            var err = result.error ?? string.Empty;
            if (err.IndexOf("currency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                ShowStatus("Out of currency!");
            else
                ShowStatus("Purchase failed!");
        }
        if (!success)
        {
            Debug.LogWarning($"[UIShopItemDisplay] Purchase failed for {Data.id}: {result.error}");
            ShowStatus("Purchase failed!");
            if (fetchingPanel != null) fetchingPanel.SetActive(false);
            if (actionButton != null) actionButton.interactable = true;
            _buyInProgress = false;
            yield break;
        }
        

        // 1 frame bekleyelim; event ve cache güncellemeleri otursun
        yield return null;
        // Başarılı → görsel durumu tazele (owned/equipped/price)
        yield return RefreshVisualStateCoroutine();

        if (fetchingPanel != null) fetchingPanel.SetActive(false);
        _buyInProgress = false;
        if (actionButton != null) actionButton.interactable = true;
        UITopPanel.Instance.Initialize();
    }

    private IEnumerator RefreshVisualStateCoroutine()
    {
        // 1 frame bekle: aynı frame'de gelen OnInventoryChanged yarışlarını kes
        yield return null;
        bool owned = false;
        bool equipped = false;
        int referralCount = _cachedReferralCount;

        if (UserInventoryManager.Instance != null && Data != null)
        {
            var nid = IdUtil.NormalizeId(Data.id);
            owned = UserInventoryManager.Instance.IsOwned(nid);
            equipped = UserInventoryManager.Instance.IsEquipped(nid);
        }

        if (UserDatabaseManager.Instance != null)
        {
            var task = UserDatabaseManager.Instance.GetReferralCountAsync();
            while (!task.IsCompleted)
                yield return null;
            if (task.Exception == null)
                referralCount = task.Result;
        }

        _cachedReferralCount = referralCount;
        var visualState = DetermineVisualState(Data, Category, owned, equipped, referralCount);
        ApplyVisualState(visualState);
    }

    public void RequestRefresh()
    {
        // Inaktif objede coroutine başlatma; ertele
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _pendingRefresh = true;
            return;
        }

        // Aktifken: hemen ve 1 frame sonra tekrar
        SendMessage("RefreshVisualState", SendMessageOptions.DontRequireReceiver);
        StartCoroutine(_RequestRefreshCo());
    }

    private IEnumerator _RequestRefreshCo()
    {
        yield return null;
        SendMessage("RefreshVisualState", SendMessageOptions.DontRequireReceiver);
    }

    private void ShowStatus(string msg)
    {
        if (statusLabel == null) return; // Bağlanmadıysa sessizce geç
        if (_statusRoutine != null) StopCoroutine(_statusRoutine);
        _statusRoutine = StartCoroutine(StatusRoutine(msg));
    }

    private IEnumerator StatusRoutine(string msg)
    {
        statusLabel.text = msg;
        statusLabel.gameObject.SetActive(true);
        yield return new WaitForSeconds(statusDuration);
        statusLabel.gameObject.SetActive(false);
    }
    private void HandleActiveConsumablesChanged()
    {
        // When server pushes changes, simply refresh visuals
        RequestRefresh();
    }

    private bool TryStartCountdown()
    {
        if (!_isConsumable || Data == null || UserInventoryManager.Instance == null) return false;
        var nid = IdUtil.NormalizeId(Data.id);
        if (!UserInventoryManager.Instance.IsConsumableActive(nid)) return false;

        if (_countdownCo == null)
            _countdownCo = StartCoroutine(CountdownRoutine());
        if (actionButton != null)
        {
            actionButton.gameObject.SetActive(true);
            actionButton.interactable = false;
        }
        return true;
    }

    private void StopCountdown()
    {
        if (_countdownCo != null)
        {
            StopCoroutine(_countdownCo);
            _countdownCo = null;
        }
    }

    private IEnumerator CountdownRoutine()
    {
        // Update label once per second while active; when expired, trigger refresh
        var nid = IdUtil.NormalizeId(Data.id);
        while (isActiveAndEnabled && UserInventoryManager.Instance != null &&
               UserInventoryManager.Instance.IsConsumableActive(nid))
        {
            var remain = UserInventoryManager.Instance.GetConsumableRemaining(nid);
            if (actionLabel != null)
                actionLabel.text = FormatRemain(remain);
            yield return new WaitForSecondsRealtime(1f);
        }
        // One last refresh to flip UI back to purchase mode
        RequestRefresh();
        _countdownCo = null;
    }

    private static string FormatRemain(System.TimeSpan t)
    {
        int hours = (int)t.TotalHours;
        return $"{hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
    }


}