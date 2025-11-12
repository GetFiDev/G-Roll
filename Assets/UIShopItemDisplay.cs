using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    [SerializeField] private Sprite iconEquip;     // Equip ikonu
    [SerializeField] private Sprite iconUnequip;   // Unequip ikonu
    [SerializeField] private Sprite iconReferral;  // Referral ikonu
    [SerializeField] private Sprite iconAd;        // Reklam ikonu

    [Header("Fetching Overlay")]
    [SerializeField] private GameObject fetchingPanel; // İşlem sırasında butonun üstünü kapatan panel

    [SerializeField] private bool setNativeSizeOnLoad = false;

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
    private Material _iconMaterialInstance;

    [Header("Status Feedback")]
    [SerializeField] private TMP_Text statusLabel;    // (opsiyonel) buton yakınında küçük metin
    [SerializeField] private float statusDuration = 1.8f; // saniye
    private Coroutine _statusRoutine;                 // çalışan status coroutine

    private void OnEnable()
    {
        if (UserInventoryManager.Instance != null)
            UserInventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;

        // Yeni iç butonun click'ini Shop mantığına bağla
        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnClickBuy);
        }
        // Overlay ilk açılışta kapalı olsun
        if (fetchingPanel != null) fetchingPanel.SetActive(false);

        // Ensure material instance if icon exists
        EnsureIconMaterialInstance();

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
            iconImage.sprite = data.iconSprite;
            var col = iconImage.color;
            col.a = 1f;
            iconImage.color = col;
        }
        else
        {
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
        // Referral: threshold karşılanmadıysa kilit, karşılandıysa Normal_Owned (fiyat gizlenir)
        if (category == ShopCategory.Referral)
        {
            if (data != null && referralCount < data.referralThreshold)
            {
                return ShopVisualState.Referral_Locked;
            }
            else
            {
                return ShopVisualState.Normal_Owned;
            }
        }

        // Premium görseli gereken durumlar:
        // 1) Kategori Premium ise
        // 2) Kategori BullMarket ama item gerçek parayla alınabiliyorsa (dollarPrice > 0)
        bool shouldUsePremiumVisual = (category == ShopCategory.Premium)
                                      || (category == ShopCategory.BullMarket && data != null && data.dollarPrice > 0);

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
                if (nameText != null) nameText.text = "Locked";
                break;
        }

        if (iconImage != null)
        {
            iconImage.enabled = true; // Referral_Locked dahil artık ikon görünür

            bool owned = false;
            bool equipped = false;
            var d = Data;

            if (UserInventoryManager.Instance != null && d != null)
            {
                var nid = IdUtil.NormalizeId(d.id);
                owned = UserInventoryManager.Instance.IsOwned(nid);
                equipped = UserInventoryManager.Instance.IsEquipped(nid);
            }

            int referralCount = _cachedReferralCount;
            bool referralUnlocked = (Category == ShopCategory.Referral) && d != null && referralCount >= d.referralThreshold;

            float saturation = (owned || referralUnlocked) ? 1f : 0f;
            SetIconSaturation(saturation);
        }

        UpdateActionButton(state);
        if (fetchingPanel != null && !_buyInProgress)
            fetchingPanel.SetActive(false);
    }

    private void UpdateActionButton(ShopVisualState state)
    {
        if (actionButton == null) return;

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
                actionButton.interactable = false;
                break;

            case ShopVisualState.Normal_NotOwned:
            case ShopVisualState.Premium_NotOwned:
                if (isAdConsumable)
                {
                    label = "Watch Ad";
                    icon = iconAd;
                }
                else if (Category == ShopCategory.Premium || (Category == ShopCategory.BullMarket && d != null && d.dollarPrice > 0))
                {
                    // $ fiyat
                    if (d != null) label = "$" + d.dollarPrice.ToString("F2");
                    icon = null; // dolar için ikon göstermiyoruz
                }
                else
                {
                    // GET fiyat
                    if (d != null) label = d.getPrice.ToString("F2");
                    icon = iconCurrency;
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
                // Gerçek para
                return d.dollarPrice > 0 ? $"${d.dollarPrice:F2}" : "$0.00";

            case ShopCategory.BullMarket:
                // Tüketilebilir; 3 yöntemin herhangi biri olabilir (etiketi basit tut)
                if (d.isRewardedAd) return "Watch Ad";
                if (d.dollarPrice > 0) return $"${d.dollarPrice:F2}";
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

    private void Clear(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    /// <summary>
    /// UI Button callback. Kategorisine ve item verisine göre uygun satın alma metodunu çağırır.
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

        // Referral tabında satın alma yok
        if (Category == ShopCategory.Referral)
        {
            Debug.Log("[UIShopItemDisplay] Referral item cannot be purchased from shop. Unlock via referrals.");
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
        if (Category == ShopCategory.Premium || (Category == ShopCategory.BullMarket && Data.dollarPrice > 0))
            method = UserInventoryManager.PurchaseMethod.IAP;
        else if (Data.isRewardedAd)
            method = UserInventoryManager.PurchaseMethod.Ad;
        else
            method = UserInventoryManager.PurchaseMethod.Get;

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
    private Material EnsureIconMaterialInstance()
    {
        if (iconImage == null) return null;
        var current = iconImage.material;
        if (_iconMaterialInstance == null && current != null)
        {
            _iconMaterialInstance = new Material(current);
            iconImage.material = _iconMaterialInstance;
        }
        return _iconMaterialInstance;
    }

    private void SetIconSaturation(float value)
    {
        if (iconImage == null) return;
        var mat = EnsureIconMaterialInstance();
        if (mat != null && mat.HasProperty("_HsvSaturation"))
        {
            mat.SetFloat("_HsvSaturation", value);
        }
    }

    private void OnDestroy()
    {
        if (_iconMaterialInstance != null)
        {
            Destroy(_iconMaterialInstance);
            _iconMaterialInstance = null;
        }
    }
}