using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Dışardan çalışan, tek merkezli notification badge yöneticisi.
/// - Achievement badge: claim edilebilir ödül varsa yanar.
/// - Shop badge: oyuna yeni bir item eklendiyse yanar.
/// 
/// Mevcut sistemlere dokunmadan çalışır:
///  - AchievementService üzerinden snapshot çeker.
///  - ItemDatabaseManager üzerinden item listesini okur.
/// 
/// Kullanım:
///  1) Sahneye bir GameObject ekle ve bu scripti koy.
///  2) Inspector'da:
///     - achievementTabBadge      (bottom bar "Tasks/Achievements" butonunun köşesindeki kırmızı nokta)
///     - shopTabBadge             (bottom bar "Shop" butonunun köşesindeki kırmızı nokta)
///     - achievementRowBadges     (isteğe bağlı: her bir achievement row için typeId + badge objesi)
///  3) Achievement paneli açılırken/claim sonrası:
///       NotificationBadgeManager.Instance.RefreshAchievementBadges();
///  4) Shop paneli açıldığında:
///       NotificationBadgeManager.Instance.MarkAllShopItemsSeen();
/// </summary>
public class NotificationBadgeManager : MonoBehaviour
{
    public static NotificationBadgeManager Instance { get; private set; }

    [Header("Achievement Badges")]
    [Tooltip("Bottom panelde Tasks/Achievements sekmesini açan butonun köşesindeki kırmızı nokta.")]
    public GameObject achievementTabBadge;

    [Tooltip("İsteğe bağlı: Her bir achievement satırı için typeId + row üzerindeki badge GameObject’i.")]
    public List<AchievementRowBinding> achievementRowBadges = new List<AchievementRowBinding>();

    [Header("Shop Badges")]
    [Tooltip("Bottom panelde Shop sekmesini açan butonun köşesindeki kırmızı nokta.")]
    public GameObject shopTabBadge;

    [Header("Config")]
    [Tooltip("Oyun açılışında otomatik olarak achievement & shop badge refresh yapılsın mı?")]
    public bool autoRefreshOnStart = true;

    // --- internal state ---
    private AchSnapshot _lastAchSnapshot;
    private bool _isRefreshingAchievements;

    private HashSet<string> _seenShopItemIds;
    private const string SHOP_SEEN_KEY = "NOTIF_SHOP_SEEN_ITEMS_V1";

    [Serializable]
    public class AchievementRowBinding
    {
        [Tooltip("Server achievement typeId (AchDef.typeId / AchState.typeId ile birebir aynı olmalı).")]
        public string typeId;

        [Tooltip("Bu typeId'ye ait row üzerinde gösterilecek kırmızı nokta GameObject'i.")]
        public GameObject badgeObject;
    }

    [Serializable]
    private class SeenItemsWrapper
    {
        public List<string> ids = new List<string>();
    }

    // ----------------- LIFECYCLE -----------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NotificationBadgeManager] Duplicate instance; destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadSeenShopItems();
    }

    private async void Start()
    {
        if (!autoRefreshOnStart) return;

        // Achievement & Shop badge durumlarını oyun açılışında bir kere değerlendir.
        await RefreshAchievementBadges();
        RefreshShopBadge();
    }

    // ----------------- PUBLIC API -----------------

    /// <summary>
    /// Achievement snapshot'ı server'dan çeker ve
    /// - Bottom achievement tab badge
    /// - (isteğe bağlı) row badge'lerini update eder.
    /// </summary>
    public async Task RefreshAchievementBadges()
    {
        if (_isRefreshingAchievements)
            return;

        _isRefreshingAchievements = true;

        try
        {
            var snap = await AchievementService.GetSnapshotAsync();
            _lastAchSnapshot = snap;

            bool anyClaimable = HasAnyClaimableAchievement(snap);

            // Bottom tab badge
            if (achievementTabBadge != null)
                achievementTabBadge.SetActive(anyClaimable);

            // Row bazlı badge'ler
            UpdateAchievementRowBadges(snap);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NotificationBadgeManager] RefreshAchievementBadges error: {e.Message}");
            // Hata durumunda tüm achievement badge'lerini kapatmak daha güvenli
            if (achievementTabBadge != null)
                achievementTabBadge.SetActive(false);
            UpdateAchievementRowBadges(null);
        }
        finally
        {
            _isRefreshingAchievements = false;
        }
    }

    /// <summary>
    /// Şu anki snapshot üzerinden claim edilebilir achievement var mı?
    /// UI'da başka yerlerde kullanmak istersen erişilebilsin diye property yaptım.
    /// </summary>
    public bool HasClaimableAchievements =>
        _lastAchSnapshot != null && HasAnyClaimableAchievement(_lastAchSnapshot);

    /// <summary>
    /// Shop için "yeni item" kontrolü yapar ve shopTabBadge durumunu günceller.
    /// </summary>
    public void RefreshShopBadge()
    {
        // ItemDatabaseManager henüz hazır değilse badge'i kapatmak güvenli.
        if (!ItemDatabaseManager.IsReady)
        {
            if (shopTabBadge != null)
                shopTabBadge.SetActive(false);
            return;
        }

        // Tüm item id'lerini çek
        var allItems = ItemDatabaseManager.GetAllItems()?.ToList() ?? new List<ItemDatabaseManager.ReadableItemData>();
        var allIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            var nid = IdUtil.NormalizeId(item.id);
            if (!string.IsNullOrEmpty(nid))
                allIds.Add(nid);
        }

        if (allIds.Count == 0)
        {
            if (shopTabBadge != null)
                shopTabBadge.SetActive(false);
            return;
        }

        // Seen listemizde olmayan herhangi bir id var mı?
        bool hasNew = allIds.Any(id => !_seenShopItemIds.Contains(id));

        if (shopTabBadge != null)
            shopTabBadge.SetActive(hasNew);
    }

    /// <summary>
    /// Shop panelini açtığında çağır:
    ///  - O anki tüm item'ları "görülmüş" olarak işaretler,
    ///  - Shop badge'ini kapatır.
    /// </summary>
    public void MarkAllShopItemsSeen()
    {
        if (!ItemDatabaseManager.IsReady)
        {
            // Henüz itemlar yoksa yapacak bir şey yok.
            return;
        }

        var allItems = ItemDatabaseManager.GetAllItems()?.ToList() ?? new List<ItemDatabaseManager.ReadableItemData>();
        foreach (var item in allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            var nid = IdUtil.NormalizeId(item.id);
            if (!string.IsNullOrEmpty(nid))
                _seenShopItemIds.Add(nid);
        }

        SaveSeenShopItems();

        if (shopTabBadge != null)
            shopTabBadge.SetActive(false);
    }

    /// <summary>
    /// Dışarıdan "achievement paneli açıldı" sinyali gelirse
    /// istersen burada özel davranış ekleyebilirsin.
    /// Şu an sadece snapshot'a göre badge durumunu koruyor.
    /// </summary>
    public async void OnAchievementPanelOpened()
    {
        // Panel açıldığında en güncel durumu görmek için tekrar refresh etmek mantıklı.
        await RefreshAchievementBadges();
    }

    // ----------------- ACHIEVEMENT İÇ LOGİK -----------------

    /// <summary>
    /// Herhangi bir achievement type için claim edilebilir seviyeler var mı?
    /// Definition: ClaimAllEligibleAsync ile birebir aynı mantık:
    ///   lv = 1..state.level içinde claimedLevels'te olmayan seviyeler claim edilebilir.
    /// </summary>
    private bool HasAnyClaimableAchievement(AchSnapshot snap)
    {
        if (snap == null || snap.states == null || snap.defs == null)
            return false;

        foreach (var st in snap.states)
        {
            if (st == null) continue;
            var def = snap.DefOf(st.typeId);
            if (def == null) continue;

            if (IsTypeClaimable(def, st))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Belirli bir typeId için claim edilebilir en az bir seviye var mı?
    /// </summary>
    private bool IsTypeClaimable(AchDef def, AchState st)
    {
        if (def == null || st == null) return false;

        var claimed = new HashSet<int>(st.claimedLevels ?? new List<int>());
        // level: server'ın "ulaşılmış seviye" bilgisi
        for (int lv = 1; lv <= st.level; lv++)
        {
            if (claimed.Contains(lv)) continue;
            // Bu seviyeye ulaşılmış ve henüz claim edilmemiş -> claimable
            return true;
        }
        return false;
    }

    /// <summary>
    /// Inspector'dan bağladığın row badge binding'leri update eder.
    /// </summary>
    private void UpdateAchievementRowBadges(AchSnapshot snap)
    {
        if (achievementRowBadges == null || achievementRowBadges.Count == 0)
            return;

        if (snap == null || snap.states == null || snap.defs == null)
        {
            // Snapshot yokken tüm row badge'lerini kapatalım
            foreach (var binding in achievementRowBadges)
            {
                if (binding != null && binding.badgeObject != null)
                    binding.badgeObject.SetActive(false);
            }
            return;
        }

        foreach (var binding in achievementRowBadges)
        {
            if (binding == null || string.IsNullOrEmpty(binding.typeId))
            {
                if (binding != null && binding.badgeObject != null)
                    binding.badgeObject.SetActive(false);
                continue;
            }

            var typeId = binding.typeId;
            var def = snap.DefOf(typeId);
            var st = snap.StateOf(typeId);

            bool claimable = IsTypeClaimable(def, st);

            if (binding.badgeObject != null)
                binding.badgeObject.SetActive(claimable);
        }
    }

    // ----------------- SHOP SEEN STATE (LOCAL) -----------------

    private void LoadSeenShopItems()
    {
        _seenShopItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!PlayerPrefs.HasKey(SHOP_SEEN_KEY))
            return;

        var json = PlayerPrefs.GetString(SHOP_SEEN_KEY, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var wrap = JsonUtility.FromJson<SeenItemsWrapper>(json);
            if (wrap != null && wrap.ids != null)
            {
                foreach (var id in wrap.ids)
                {
                    var nid = IdUtil.NormalizeId(id);
                    if (!string.IsNullOrEmpty(nid))
                        _seenShopItemIds.Add(nid);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NotificationBadgeManager] LoadSeenShopItems parse error: {e.Message}");
        }
    }

    private void SaveSeenShopItems()
    {
        try
        {
            var wrap = new SeenItemsWrapper
            {
                ids = _seenShopItemIds.ToList()
            };
            var json = JsonUtility.ToJson(wrap, prettyPrint: false);
            PlayerPrefs.SetString(SHOP_SEEN_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NotificationBadgeManager] SaveSeenShopItems error: {e.Message}");
        }
    }
}