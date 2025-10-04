using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NetworkingData; // <-- LBEntry ve UserData burada

public class LeaderboardManager : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager userDB;

    [Header("Options")]
    [Range(1, 100)] public int topN = 50;

    // Cache (sadece bu sınıf yazar)
    public List<LBEntry> TopCached { get; private set; } = new();
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

        // 1) Kendi verin
        UserData me = await userDB.LoadUserData();
        MyUsername = string.IsNullOrWhiteSpace(me?.username) ? "Guest" : me.username;
        MyScore    = me?.score ?? 0;

        // 2) TopN
        List<LBEntry> top = await userDB.FetchLeaderboardTopAsync(topN);
        // İsim olmayan veya "Guest" olanları liste dışı bırak
        var filtered = (top ?? new List<LBEntry>()).FindAll(e =>
            !string.IsNullOrWhiteSpace(e.username) &&
            !string.Equals(e.username, "Guest", StringComparison.OrdinalIgnoreCase)
        );
        TopCached = filtered;

        // 3) Sıralama (ilk 50’de değilse boş kalsın)
        MyRankText = "";
        var myUid = userDB.currentLoggedUserID;
        if (!string.IsNullOrEmpty(myUid))
        {
            int idx = TopCached.FindIndex(e => e.uid == myUid);
            if (idx >= 0) MyRankText = (idx + 1).ToString(); // 1-based
        }

        OnCacheUpdated?.Invoke();
    }


    public void ManualRefresh()
    {
        _ = RefreshCacheAsync();
    }
}
