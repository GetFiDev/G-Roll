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
    [SerializeField] private TMP_Text categoryText;

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

    public ItemDatabaseManager.ReadableItemData Data { get; private set; }
    public ShopCategory Category { get; private set; }

    public void Setup(ItemDatabaseManager.ReadableItemData data, ShopCategory category)
    {
        Data = data;
        Category = category;

        // ---- Header ----
        nameText.text = data?.name ?? "Unnamed";
        descriptionText.text = data?.description ?? "";

        // Icon (remote)
        iconImage.sprite = null;
        if (!string.IsNullOrEmpty(data?.iconUrl))
            StartCoroutine(LoadIcon(data.iconUrl));

        // ---- Price / Category Badge ----
        categoryText.text = Category.ToString();
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
        var img = go.GetComponentInChildren<Image>();
        var tmp = go.GetComponentInChildren<TMP_Text>();

        if (img != null) img.sprite = icon;
        if (tmp != null) tmp.text = text;
    }

    private IEnumerator LoadIcon(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                iconImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                Debug.LogWarning($"[UIShopItemDisplay] Icon load failed: {url}");
            }
        }
    }

    private void Clear(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}