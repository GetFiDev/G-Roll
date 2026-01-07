using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ranking panel: Listen to LeaderboardManager, show list.
/// Supports All-Time / Season tabs.
/// </summary>
public class UIRankingPanel : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private UILeaderboardDisplay rowPrefab;
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private UILeaderboardDisplay selfDisplay; 
    
    [Header("Tabs")]
    [SerializeField] private UnityEngine.UI.Button allTimeTabBtn;
    [SerializeField] private UnityEngine.UI.Button seasonTabBtn;

    [Header("Season Info")]
    [SerializeField] private TextMeshProUGUI seasonNameText;
    [SerializeField] private TextMeshProUGUI seasonDescriptionText;

    [Header("Tab Visuals")]
    [SerializeField] private Image allTimeTabImage;
    [SerializeField] private Image seasonTabImage;
    [SerializeField] private Sprite tabViewingSprite; // Currently Viewing
    [SerializeField] private Sprite tabNotViewingSprite; // Not Viewing
    [SerializeField] private Sprite tabDisabledSprite; // Season Not Active

    [Header("Sticky Entry")]
    [SerializeField] private UIStickyLeaderboardEntry stickyEntry;
    [SerializeField] private RectTransform scrollViewport; // Scroll Viewport
    [SerializeField] private RectTransform contentPanel; // Content

    // State
    private float stickyCheckTimer = 0f;
    private int myRankIndexInList = -1;

    private void Start()
    {
        if (allTimeTabBtn) allTimeTabBtn.onClick.AddListener(() => OnTabClicked(LeaderboardType.AllTime));
        if (seasonTabBtn) seasonTabBtn.onClick.AddListener(() => OnTabClicked(LeaderboardType.Season));
    }

    private void OnEnable()
    {
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnCacheUpdated += RefreshUI;
            
            // Initial State Logic
            if (LeaderboardManager.Instance.IsSeasonActive)
            {
                // Default: All Time Viewing, Season Not Viewing
                LeaderboardManager.Instance.SwitchTab(LeaderboardType.AllTime);
            }
            else
            {
                // Default: All Time Viewing, Season Disabled
                LeaderboardManager.Instance.SwitchTab(LeaderboardType.AllTime);
            }

             LeaderboardManager.Instance.ManualRefresh();
             RefreshUI(); 
        }
        UpdateTabVisuals();
    }

    private void Update()
    {
        // 1. Countdown for Scene Not Active
        if (LeaderboardManager.Instance != null && !LeaderboardManager.Instance.IsSeasonActive)
        {
            if (seasonDescriptionText)
            {
                var nextStart = LeaderboardManager.Instance.NextSeasonStartDate;
                if (nextStart.HasValue)
                {
                    TimeSpan remaining = nextStart.Value - System.DateTime.Now;
                    if (remaining.TotalSeconds > 0)
                        seasonDescriptionText.text = $"Starts in: {remaining.Days}d {remaining.Hours}h {remaining.Minutes}m";
                    else
                        seasonDescriptionText.text = "Season starting soon...";
                }
                else
                {
                    seasonDescriptionText.text = "Season Closed";
                }
            }
        }

        // 2. Sticky Logic Update (Throttle checks)
        stickyCheckTimer += Time.deltaTime;
        if (stickyCheckTimer > 0.1f)
        {
            stickyCheckTimer = 0f;
            UpdateStickyVisibility();
        }
    }

    private void OnDisable()
    {
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnCacheUpdated -= RefreshUI;
        }
    }

    private void OnTabClicked(LeaderboardType type)
    {
        if (LeaderboardManager.Instance == null) return;
        LeaderboardManager.Instance.SwitchTab(type);
        UpdateTabVisuals();
    }

    private void UpdateTabVisuals()
    {
        var lm = LeaderboardManager.Instance;
        if (lm == null) return;
        
        var current = lm.CurrentType;
        bool isSeasonActive = lm.IsSeasonActive;

        // 1. All Time State (Viewing vs Not Viewing)
        if (allTimeTabImage)
        {
            allTimeTabImage.sprite = (current == LeaderboardType.AllTime) ? tabViewingSprite : tabNotViewingSprite;
        }

        // 2. Season State (Viewing, NotViewing, Disabled)
        if (seasonTabImage)
        {
            if (!isSeasonActive)
            {
                seasonTabImage.sprite = tabDisabledSprite; // Season Not Active
                if (seasonTabBtn) seasonTabBtn.interactable = false; // Disable click
            }
            else
            {
                seasonTabImage.sprite = (current == LeaderboardType.Season) ? tabViewingSprite : tabNotViewingSprite;
                if (seasonTabBtn) seasonTabBtn.interactable = true;
            }
        }

        // 3. Info Text logic
        if (lm.IsSeasonActive)
        {
            // Active Season: Show Name & static Description
            if (seasonNameText) seasonNameText.text = lm.ActiveSeasonName;
            
            // Only show description if Season Tab is SELECTED? User said "when season tab clicked... show description".
            // But if season is inactive, user says it shows "Season closed" ON THE BUTTON?
            // "bu statedeyken bu butona basılamaz... üzerindeki season name, season description yazan alanda şunlar yazar"
            // So text is always visible? or visible when selected?
            // "Seasona basarsam aşağıda sezonun leaderboardını görürüm."
            // If inactive, button disabled. Does that mean we can't see the text?
            // Probably text is outside the button, in a header?
            // Let's assume text is always updated but visibility depends on context.
            // Actually, if button disabled, maybe we see "Closed" on the button text?
            // Or the panel header?
            // Let's follow "üzerindeki season name..." implies text is ON the button or linked to it.
            // I will update the texts regardless.
            
            if (seasonDescriptionText)
            {
                seasonDescriptionText.text = lm.ActiveSeasonDescription;
            }
        }
        else
        {
            // Inactive Season
            if (seasonNameText) seasonNameText.text = "Season Closed";
            // Description is handled in Update() for countdown.
        }
        
        // Visibility of texts:
        // Generally usually only visible if Season tab is active OR if we want to show status.
        // User requirements: "season butonu seasonnotactive stateine geçer... seasonnametmp: season closed"
        // This implies the texts are visible even if button is disabled (maybe unrelated to selection).
        // I will force them visible if Season is inactive so user knows why.
        if (seasonNameText) seasonNameText.gameObject.SetActive(true);
        if (seasonDescriptionText) seasonDescriptionText.gameObject.SetActive(true);
    }

    private void RefreshUI()
    {
        if (!rowsRoot || rowPrefab == null) return;
        
        var lm = LeaderboardManager.Instance;

        // Clean
        foreach (Transform child in rowsRoot) Destroy(child.gameObject);

        var list = lm.TopCached;
        if (list == null) return;

        int count = (list.Count < 100) ? list.Count : 100;
        for (int i = 0; i < count; i++)
        {
            var item = list[i];
            var go = Instantiate(rowPrefab, rowsRoot);
            var ui = go.GetComponent<UILeaderboardDisplay>();

            int rank = i + 1; // 1-based index
            ui.SetData(rank, item.username, item.score, item.hasElitePass, item.photoUrl);
        }

        // Self display (Top Header)
        if (selfDisplay)
        {
            // Header stats
            selfDisplay.SetData(lm.MyRank, lm.MyUsername, lm.MyScore, lm.MyIsElite, lm.MyPhotoUrl);
        }
        
        // 5. Find my index for sticky logic
        myRankIndexInList = -1;
        string myUid = lm.userDB.currentLoggedUserID;
        if (!string.IsNullOrEmpty(myUid))
        {
            myRankIndexInList = list.FindIndex(x => x.uid == myUid);
        }

        // Sticky visual update
        if (stickyEntry) 
        {
            stickyEntry.Refresh();
            // Initial check immediate
            UpdateStickyVisibility();
        }
    }

    private void UpdateStickyVisibility()
    {
        if (!stickyEntry || !scrollViewport || !contentPanel || !rowsRoot) return;

        var lm = LeaderboardManager.Instance;
        if (lm == null || lm.MyRank <= 0)
        {
            stickyEntry.gameObject.SetActive(false);
            return;
        }

        // If I am NOT in the top list at all
        if (myRankIndexInList == -1)
        {
            // I have a rank (checked above) but I am not in the cached top list -> Show docked bottom
            stickyEntry.gameObject.SetActive(true);
            stickyEntry.DockBottom();
            return;
        }

        // I am in the list at index 'myRankIndexInList'.
        // Check if that row is visible.
        if (myRankIndexInList >= rowsRoot.childCount) return; // Safety

        RectTransform rowRect = rowsRoot.GetChild(myRankIndexInList) as RectTransform;
        if (rowRect == null) return;

        // Check if row is within viewport
        if (IsRectVisible(rowRect))
        {
            // Visible -> Hide sticky
            stickyEntry.gameObject.SetActive(false);
        }
        else
        {
            // Not visible -> Show sticky
            stickyEntry.gameObject.SetActive(true);

            // Determine Top vs Bottom
            // Compare world positions Y
            if (rowRect.position.y > scrollViewport.position.y) 
            {
                // Item is ABOVE viewport -> Dock TOP
                 stickyEntry.DockTop();
            }
            else
            {
                // Item is BELOW viewport -> Dock BOTTOM
                 stickyEntry.DockBottom();
            }
        }
    }

    private bool IsRectVisible(RectTransform rect)
    {
        // Simple overlap check
        Vector3[] objectCorners = new Vector3[4];
        rect.GetWorldCorners(objectCorners);
        
        Vector3[] viewportCorners = new Vector3[4];
        scrollViewport.GetWorldCorners(viewportCorners);

        // Check if overlaps Y
        float objMaxY = objectCorners[1].y; // Top Left y
        float objMinY = objectCorners[0].y; // Bottom Left y
        
        float viewMaxY = viewportCorners[1].y;
        float viewMinY = viewportCorners[0].y;

        // Totally outside check
        if (objMinY > viewMaxY) return false; // Entirely above
        if (objMaxY < viewMinY) return false; // Entirely below

        return true; 
    }
}