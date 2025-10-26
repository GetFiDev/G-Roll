using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Firebase.Storage;

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

    [SerializeField] private bool setNativeSizeOnLoad = false;

    private Coroutine _blinkCoroutine;

    public ItemDatabaseManager.ReadableItemData Data { get; private set; }
    public ShopCategory Category { get; private set; }

    public void Setup(ItemDatabaseManager.ReadableItemData data, ShopCategory category)
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
        priceText.text = BuildPriceLabel(data, category);

        // ---- Stat Chips ----
        Clear(statChipsRoot);
        if (data != null) BuildStatChips(data);
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
}