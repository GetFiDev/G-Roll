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
    public string MyRankText { get; private set; } = ""; // ilk 50’de değilse ""
    public int    MyScore    { get; private set; } = 0;

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

        // ➋ await ile UserData çek
        UserData me = await userDB.LoadUserData();
        MyScore = me?.score ?? 0;

        // ➌ await ile TopN fetch
        List<LBEntry> top = await userDB.FetchLeaderboardTopAsync(topN);
        TopCached = top ?? new List<LBEntry>();

        // ➍ rank hesapla (ilk 50’de değilse boş metin)
        MyRankText = "";
        var myUid = userDB.currentLoggedUserID;
        if (!string.IsNullOrEmpty(myUid))
        {
            int idx = TopCached.FindIndex(e => e.uid == myUid);
            if (idx >= 0) MyRankText = (idx + 1).ToString();
        }

        OnCacheUpdated?.Invoke();
    }

    public void ManualRefresh()
    {
        _ = RefreshCacheAsync();
    }
}
