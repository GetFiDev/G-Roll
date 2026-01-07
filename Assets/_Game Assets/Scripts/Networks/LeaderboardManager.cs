using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NetworkingData; // <-- LBEntry ve UserData burada
using System.Linq;
using NetworkingData;

public enum LeaderboardType
{
    AllTime,
    Season
}

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Header("Refs")]
    public UserDatabaseManager userDB;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (userDB == null)
        {
            userDB = FindObjectOfType<UserDatabaseManager>();
            if (userDB == null) userDB = UserDatabaseManager.Instance;
            if (userDB == null) Debug.LogError("[LeaderboardManager] UserDatabaseManager Missing!");
        }
    }

    [Header("Options")]
    [Range(1, 100)] public int topN = 50; // number of top entries to fetch when not fetching all
    public bool fetchAll = true;

    // Cache (sadece bu sınıf yazar)
    // Cache (sadece bu sınıf yazar)
    public List<UserDatabaseManager.LBEntry> TopCached { get; private set; } = new();
    public string MyUsername { get; private set; } = "Guest";
    public int    MyScore    { get; private set; } = 0;
    public int    MyRank     { get; private set; } = 0; // Gerçek sayısal rank
    public string MyRankText { get; private set; } = "";   // UI Metni
    public bool   MyIsElite  { get; private set; } = false;
    public string MyPhotoUrl { get; private set; } = "";

    // State
    public LeaderboardType CurrentType { get; private set; } = LeaderboardType.AllTime;
    public string ActiveSeasonId { get; private set; } = "season_1"; // Default or fetched
    public string ActiveSeasonName { get; private set; } = "Season 1";
    public string ActiveSeasonDescription { get; private set; } = "Loading season info...";
    public DateTime? NextSeasonStartDate { get; private set; }
    
    // Helper to determine if season is active
    public bool IsSeasonActive => !string.IsNullOrEmpty(ActiveSeasonId) && ActiveSeasonId != "none";

    private bool _isFetching = false;

    public event Action OnCacheUpdated;

    private void OnEnable()
    {
        if (userDB != null)
        {
            userDB.OnLoginSucceeded += HandleLoginSucceeded;
            userDB.OnUserDataSaved  += HandleUserDataSaved;
        }
        // Fetch season ID on start?
        _ = FetchSeasonConfig();
    }

    private void OnDisable()
    {
        if (userDB != null)
        {
            userDB.OnLoginSucceeded -= HandleLoginSucceeded;
            userDB.OnUserDataSaved  -= HandleUserDataSaved;
        }
    }

    private void HandleLoginSucceeded()
    {
        _ = FetchSeasonConfig(); // Fetch season first (needs auth?)
        _ = RefreshCacheAsync(); // fire-and-forget
    }

    private void HandleUserDataSaved(UserData data)
    {
        _ = RefreshCacheAsync();
    }

    // ➊ async Task olmalı (void değil)
    public async Task RefreshCacheAsync()
    {
        if (userDB == null) 
        {
             Debug.LogError("[LeaderboardManager] Cannot refresh: UserDB is null.");
             return;
        }

        Debug.Log($"[LeaderboardManager] Refreshing cache... Mode: {CurrentType}");

        // 1) Sunucudan snapshot (callable)
        UserDatabaseManager.LeaderboardPage page = null;
        try
        {
            int limit = fetchAll ? 100 : Mathf.Clamp(topN, 1, 100);
            
            string lbId = "all_time";
            if (CurrentType == LeaderboardType.Season)
            {
                 // Eğer season ID boşsa veya fetched değilse fallback all_time olabilir
                 // veya boş liste dönebiliriz. Şimdilik lbId'yi seasonId yapalım.
                 if (!string.IsNullOrEmpty(ActiveSeasonId)) lbId = ActiveSeasonId;
            }

            Debug.Log($"[LeaderboardManager] Fetching Snapshot for ID: {lbId} Limit: {limit}");
            page = await userDB.GetLeaderboardsSnapshotAsync(leaderboardId: lbId, limit: limit, startAfterScore: null, includeSelf: true);
            Debug.Log($"[LeaderboardManager] Fetch Success. Items: {page?.items?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LeaderboardManager] Snapshot fetch failed: {ex}");
        }

        page ??= new UserDatabaseManager.LeaderboardPage();

        // 2) Kendi verin (callable döner; yoksa fallback olarak user data)
        // 2) Kendi verin (callable döner; yoksa fallback olarak user data)
        if (page.me != null)
        {
            MyUsername = string.IsNullOrWhiteSpace(page.me.username) ? "Guest" : page.me.username;
            MyScore    = page.me.score;
            MyIsElite  = page.me.hasElitePass;
            MyPhotoUrl = page.me.photoUrl;
            MyRank     = page.me.rank;
        }
        else
        {
            // Fallback: Use local UserData for basic profile info
            var me = await userDB.LoadUserData();
            MyUsername = string.IsNullOrWhiteSpace(me?.username) ? "Guest" : me.username;
            MyIsElite  = (me?.hasElitePass ?? false);
            MyPhotoUrl = me?.photoUrl ?? "";

            // CRITICAL: Only use local UserData's score/rank if we are in AllTime mode.
            // If we are in Season mode and page.me is null, it means we haven't played this season yet!
            if (CurrentType == LeaderboardType.AllTime)
            {
                MyScore = (int)(me?.maxScore ?? 0);
                MyRank  = me?.rank ?? 0;
            }
            else
            {
                // Season mode and no entry returned -> No score/rank for this season
                MyScore = 0;
                MyRank  = 0;
            }
        }

        // 3) Listeyi sırala (server zaten score desc gönderiyor; yine de garantile)
        var ordered = (page.items ?? new List<UserDatabaseManager.LBEntry>())
            .Where(e => !string.IsNullOrWhiteSpace(e.username) && !string.Equals(e.username, "Guest", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.score)
            .ThenBy(e => e.username)
            .ToList();

        TopCached = ordered;

        // 4) Local rank (global rank yazmıyoruz; listede bulunuyorsa index+1)
        var myUid = userDB.currentLoggedUserID;
        if (!string.IsNullOrEmpty(myUid))
        {
            int idx = TopCached.FindIndex(e => e.uid == myUid);
            // Eğer listede varsak oradaki rank (sıra numarası index+1),
            // listede yoksak serverdan gelen MyRank.
            int finalRank = (idx >= 0) ? (idx + 1) : MyRank;
            
            MyRankText = (finalRank > 0) ? finalRank.ToString() : "-";
        }
        else
        {
            MyRankText = "-";
        }

        OnCacheUpdated?.Invoke();
    }




    public void SwitchTab(LeaderboardType type)
    {
        if (CurrentType == type) return;
        CurrentType = type;
        ManualRefresh();
    }

    private async Task FetchSeasonConfig()
    {
        if (userDB == null) return;

        Debug.Log("[LeaderboardManager] Fetching Season Config from Firestore...");
        var seasons = await userDB.GetSeasonsAsync();
        
        Debug.Log($"[LeaderboardManager] Fetched {seasons.Count} seasons.");
        
        var now = DateTime.UtcNow; // Use UTC for comparison with Firestore Timestamps
        Debug.Log($"[LeaderboardManager] Current Time (UTC): {now}");

        // Find active season
        // Criteria: isActive == true AND now is between start and end
        var active = seasons.FirstOrDefault(s => 
        {
            bool timeOK = now >= s.startDate && now <= s.endDate;
            Debug.Log($"[Check Season] ID:{s.id} Name:{s.name} Active:{s.isActive}"); 
            Debug.Log($"   -> Start:{s.startDate} End:{s.endDate}");
            Debug.Log($"   -> TimeOK: {timeOK} (IsActive && TimeOK: {s.isActive && timeOK})");
            return s.isActive && timeOK;
        });

        if (active != null)
        {
            ActiveSeasonId = active.id;
            ActiveSeasonName = active.name;
            ActiveSeasonDescription = active.description;
            NextSeasonStartDate = null;
            Debug.Log($"[LeaderboardManager] Active Season Found: {active.name} ({active.id})");
        }
        else
        {
            ActiveSeasonId = null;
            ActiveSeasonName = "Season Closed";
            ActiveSeasonDescription = "Season Closed"; // UI handles countdown
            
            // Find NEXT season (isActive doesn't matter for future?)
            // Usually next season is 'active=true' but start date is in future?
            // Or just find any season with start > now
            var next = seasons.Where(s => s.startDate > now).OrderBy(s => s.startDate).FirstOrDefault();
            if (next != null)
            {
                NextSeasonStartDate = next.startDate;
                Debug.Log($"[LeaderboardManager] No Active Season. Next Season: {next.name} starts {next.startDate}");
            }
            else
            {
                NextSeasonStartDate = null;
                Debug.Log("[LeaderboardManager] No Active Season and No Next Season found.");
            }
        }
        
        // Refresh the list now that we have the correct Season ID
        if (CurrentType == LeaderboardType.Season)
        {
            _ = RefreshCacheAsync();
        }
        // Also fire event to update UI strings (Name/Desc)
        OnCacheUpdated?.Invoke();
    }


    public void ManualRefresh()
    {
        _ = RefreshCacheAsync();
    }
}
