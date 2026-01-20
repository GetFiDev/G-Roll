using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Robust, event-driven notification badge manager.
/// - Achievement badge: Lights up when there are claimable rewards. Updates INSTANTLY on claim.
/// - Shop badge: Lights up when new items exist. Clears INSTANTLY when shop is opened.
///
/// Modern game design principles:
///  1. Optimistic UI: Update badges immediately without waiting for server
///  2. Event-driven: Components can subscribe to badge state changes
///  3. Local state tracking: Maintain local claim state for instant feedback
///
/// Usage:
///  1) Add this script to a GameObject in the scene
///  2) In Inspector, assign:
///     - achievementTabBadge (red dot on Tasks/Achievements button)
///     - shopTabBadge (red dot on Shop button)
///     - achievementRowBadges (optional: per-achievement row badges)
///  3) Call OnAchievementClaimed(typeId, level) after claiming - badge updates instantly
///  4) Call MarkAllShopItemsSeen() when shop opens - badge clears instantly
/// </summary>
public class NotificationBadgeManager : MonoBehaviour
{
    public static NotificationBadgeManager Instance { get; private set; }

    [Header("Achievement Badges")]
    [Tooltip("Bottom panelde Tasks/Achievements sekmesini açan butonun köşesindeki kırmızı nokta.")]
    public GameObject achievementTabBadge;

    [Tooltip("İsteğe bağlı: Her bir achievement satırı için typeId + row üzerindeki badge GameObject'i.")]
    public List<AchievementRowBinding> achievementRowBadges = new List<AchievementRowBinding>();

    [Header("Shop Badges")]
    [Tooltip("Bottom panelde Shop sekmesini açan butonun köşesindeki kırmızı nokta.")]
    public GameObject shopTabBadge;

    [Header("Config")]
    [Tooltip("Oyun açılışında otomatik olarak achievement & shop badge refresh yapılsın mı?")]
    public bool autoRefreshOnStart = true;

    // --- Events for external listeners ---
    /// <summary>Fired when achievement badge visibility changes. Parameter: hasClaimable</summary>
    public event Action<bool> OnAchievementBadgeChanged;

    /// <summary>Fired when shop badge visibility changes. Parameter: hasNewItems</summary>
    public event Action<bool> OnShopBadgeChanged;

    // --- internal state ---
    private AchSnapshot _lastAchSnapshot;
    private bool _isRefreshingAchievements;

    // Local tracking of claimed levels (for instant UI updates before server confirms)
    private HashSet<string> _locallyClaimedKeys = new HashSet<string>();

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

        // IMPORTANT: Start with badges OFF until we confirm there's something to show
        // This prevents badges showing incorrectly before data is loaded
        if (achievementTabBadge != null)
            achievementTabBadge.SetActive(false);
        if (shopTabBadge != null)
            shopTabBadge.SetActive(false);

        LoadSeenShopItems();
    }

    private async void Start()
    {
        if (!autoRefreshOnStart) return;

        // Ensure we are authenticated before trying to fetch remote data
        if (UserDatabaseManager.Instance == null || !UserDatabaseManager.Instance.IsAuthenticated())
        {
            return;
        }

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

            // Server'dan gelen veri ile local tracking'i senkronize et
            SyncLocalClaimedWithSnapshot(snap);

            bool anyClaimable = HasAnyClaimableAchievement(snap);

            // Bottom tab badge
            SetAchievementBadgeState(anyClaimable);

            // Row bazlı badge'ler
            UpdateAchievementRowBadges(snap);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NotificationBadgeManager] RefreshAchievementBadges error: {e.Message}");
            // Hata durumunda tüm achievement badge'lerini kapatmak daha güvenli
            SetAchievementBadgeState(false);
            UpdateAchievementRowBadges(null);
        }
        finally
        {
            _isRefreshingAchievements = false;
        }
    }

    /// <summary>
    /// INSTANT UPDATE: Call this immediately after claiming an achievement reward.
    /// Updates badges without waiting for server refresh.
    /// </summary>
    /// <param name="typeId">The achievement type ID that was claimed</param>
    /// <param name="level">The level that was claimed</param>
    public void OnAchievementClaimed(string typeId, int level)
    {
        // Track locally for instant UI feedback
        string key = $"{typeId}:{level}";
        _locallyClaimedKeys.Add(key);

        // Update local snapshot if available
        if (_lastAchSnapshot != null)
        {
            var state = _lastAchSnapshot.StateOf(typeId);
            if (state != null)
            {
                state.claimedLevels ??= new List<int>();
                if (!state.claimedLevels.Contains(level))
                {
                    state.claimedLevels.Add(level);
                }
            }

            // Immediately recalculate badge state
            bool anyClaimable = HasAnyClaimableAchievement(_lastAchSnapshot);
            SetAchievementBadgeState(anyClaimable);
            UpdateAchievementRowBadges(_lastAchSnapshot);
        }

        Debug.Log($"[NotificationBadgeManager] Achievement claimed instantly: {typeId} lv{level}");
    }

    /// <summary>
    /// INSTANT UPDATE: Call this after claiming ALL eligible levels for an achievement.
    /// Updates badges without waiting for server refresh.
    /// </summary>
    /// <param name="typeId">The achievement type ID</param>
    /// <param name="claimedLevels">List of levels that were claimed</param>
    public void OnAchievementClaimedMultiple(string typeId, List<int> claimedLevels)
    {
        if (claimedLevels == null || claimedLevels.Count == 0) return;

        foreach (var level in claimedLevels)
        {
            string key = $"{typeId}:{level}";
            _locallyClaimedKeys.Add(key);
        }

        // Update local snapshot
        if (_lastAchSnapshot != null)
        {
            var state = _lastAchSnapshot.StateOf(typeId);
            if (state != null)
            {
                state.claimedLevels ??= new List<int>();
                foreach (var level in claimedLevels)
                {
                    if (!state.claimedLevels.Contains(level))
                    {
                        state.claimedLevels.Add(level);
                    }
                }
            }

            // Immediately recalculate badge state
            bool anyClaimable = HasAnyClaimableAchievement(_lastAchSnapshot);
            SetAchievementBadgeState(anyClaimable);
            UpdateAchievementRowBadges(_lastAchSnapshot);
        }

        Debug.Log($"[NotificationBadgeManager] Multiple achievements claimed instantly: {typeId} x{claimedLevels.Count}");
    }

    /// <summary>
    /// Şu anki snapshot üzerinden claim edilebilir achievement var mı?
    /// UI'da başka yerlerde kullanmak istersen erişilebilsin diye property yaptım.
    /// </summary>
    public bool HasClaimableAchievements =>
        _lastAchSnapshot != null && HasAnyClaimableAchievement(_lastAchSnapshot);

    /// <summary>
    /// Helper to set achievement badge state and fire event
    /// </summary>
    private void SetAchievementBadgeState(bool hasClaimable)
    {
        if (achievementTabBadge != null)
            achievementTabBadge.SetActive(hasClaimable);

        // Fire event for any listeners
        OnAchievementBadgeChanged?.Invoke(hasClaimable);
    }

    /// <summary>
    /// Sync local claimed keys with server snapshot (clears local tracking for items server confirms)
    /// </summary>
    private void SyncLocalClaimedWithSnapshot(AchSnapshot snap)
    {
        if (snap?.states == null) return;

        // Clear local keys that are now confirmed by server
        var keysToRemove = new List<string>();
        foreach (var key in _locallyClaimedKeys)
        {
            var parts = key.Split(':');
            if (parts.Length != 2) continue;

            var typeId = parts[0];
            if (!int.TryParse(parts[1], out int level)) continue;

            var state = snap.StateOf(typeId);
            if (state?.claimedLevels != null && state.claimedLevels.Contains(level))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _locallyClaimedKeys.Remove(key);
        }
    }

    /// <summary>
    /// Shop için "yeni item" kontrolü yapar ve shopTabBadge durumunu günceller.
    /// </summary>
    public void RefreshShopBadge()
    {
        // ItemDatabaseManager henüz hazır değilse badge'i kapatmak güvenli.
        if (!ItemDatabaseManager.IsReady)
        {
            SetShopBadgeState(false);
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
            SetShopBadgeState(false);
            return;
        }

        // Seen listemizde olmayan herhangi bir id var mı?
        bool hasNew = allIds.Any(id => !_seenShopItemIds.Contains(id));

        SetShopBadgeState(hasNew);
    }

    /// <summary>
    /// INSTANT UPDATE: Call when shop panel is opened.
    /// Immediately clears the badge and marks all items as seen.
    /// </summary>
    public void MarkAllShopItemsSeen()
    {
        // INSTANT: Clear badge immediately, before any async operations
        SetShopBadgeState(false);

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

        Debug.Log("[NotificationBadgeManager] Shop badge cleared instantly - all items marked as seen");
    }

    /// <summary>
    /// Helper to set shop badge state and fire event
    /// </summary>
    private void SetShopBadgeState(bool hasNewItems)
    {
        if (shopTabBadge != null)
            shopTabBadge.SetActive(hasNewItems);

        // Fire event for any listeners
        OnShopBadgeChanged?.Invoke(hasNewItems);
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
    /// Also considers locally claimed items that haven't been confirmed by server yet.
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
    /// Considers both server-confirmed claims AND local optimistic claims.
    /// </summary>
    private bool IsTypeClaimable(AchDef def, AchState st)
    {
        if (def == null || st == null) return false;

        // Combine server-confirmed claimed levels with locally claimed levels
        var claimed = new HashSet<int>(st.claimedLevels ?? new List<int>());

        // Also add locally claimed levels (optimistic)
        foreach (var key in _locallyClaimedKeys)
        {
            var parts = key.Split(':');
            if (parts.Length == 2 && parts[0] == st.typeId && int.TryParse(parts[1], out int localLevel))
            {
                claimed.Add(localLevel);
            }
        }

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
    /// Check if a specific achievement type has any claimable levels.
    /// Public accessor for UI components.
    /// </summary>
    public bool IsAchievementClaimable(string typeId)
    {
        if (_lastAchSnapshot == null) return false;

        var def = _lastAchSnapshot.DefOf(typeId);
        var st = _lastAchSnapshot.StateOf(typeId);

        return IsTypeClaimable(def, st);
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