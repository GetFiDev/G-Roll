using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NetworkingData; // <-- LBEntry ve UserData burada
using System.Linq;

public class LeaderboardManager : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager userDB;

    [Header("Options")]
    [Range(1, 100)] public int topN = 50; // number of top entries to fetch when not fetching all
    public bool fetchAll = true;

    // Cache (sadece bu sınıf yazar)
    public List<UserDatabaseManager.LBEntry> TopCached { get; private set; } = new();
    public string MyUsername { get; private set; } = "Guest";
    public int    MyScore    { get; private set; } = 0;
    public string MyRankText { get; private set; } = "";   // ilk 50’de değilse boş kalsın

    public event Action OnCacheUpdated;

    private void OnEnable()
    {
        if (userDB != null)
        {
            userDB.OnLoginSucceeded += HandleLoginSucceeded;
            userDB.OnUserDataSaved  += HandleUserDataSaved;
        }
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
        _ = RefreshCacheAsync(); // fire-and-forget
    }

    private void HandleUserDataSaved(UserData data)
    {
        _ = RefreshCacheAsync();
    }

    // ➊ async Task olmalı (void değil)
    public async Task RefreshCacheAsync()
    {
        if (userDB == null) return;

        // 1) Sunucudan snapshot (callable)
        UserDatabaseManager.LeaderboardPage page = null;
        try
        {
            int limit = fetchAll ? 500 : Mathf.Clamp(topN, 1, 500);
            page = await userDB.GetLeaderboardsSnapshotAsync(limit: limit, startAfterScore: null, includeSelf: true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LeaderboardManager] Snapshot fetch failed: {ex.Message}");
        }

        page ??= new UserDatabaseManager.LeaderboardPage();

        // 2) Kendi verin (callable döner; yoksa fallback olarak user data)
        if (page.me != null)
        {
            MyUsername = string.IsNullOrWhiteSpace(page.me.username) ? "Guest" : page.me.username;
            MyScore    = page.me.score;
        }
        else
        {
            var me = await userDB.LoadUserData();
            MyUsername = string.IsNullOrWhiteSpace(me?.username) ? "Guest" : me.username;
            MyScore    = (int)(me?.maxScore ?? 0);
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
            MyRankText = (idx >= 0) ? (idx + 1).ToString() : "-";
        }
        else
        {
            MyRankText = "-";
        }

        OnCacheUpdated?.Invoke();
    }


    public void ManualRefresh()
    {
        _ = RefreshCacheAsync();
    }
}
