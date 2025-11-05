using System.Collections.Generic;
using UnityEngine;

public class UIRankingPanel : MonoBehaviour
{
    [Header("Refs")]
    public LeaderboardManager leaderboard;     // sahnedeki LeaderboardManager
    public UILeaderboardDisplay selfDisplay;   // EKRANIN ÜSTÜNDEKİ sabit kart (her zaman var)
    public GameObject loadingPanel;            // "Loading..." overlay
    public Transform listRoot;                 // ScrollView -> Content (VerticalLayoutGroup)
    public UILeaderboardDisplay rowPrefab;     // Top50 satır prefab'ı

    [Header("Options")]
    [Tooltip(">0 ise LeaderboardManager.topN bununla override edilir")]
    public int topNOverride = 50;

    // İç: üretilen satırların cache’i
    private readonly List<UILeaderboardDisplay> _rows = new();

    private void OnEnable()
    {
        if (leaderboard == null)
        {
            Debug.LogWarning("[UIRankingPanel] LeaderboardManager ref missing.");
            return;
        }

        leaderboard.OnCacheUpdated += HandleCacheUpdated;

        if (topNOverride > 0)
            leaderboard.topN = topNOverride;
        if (topNOverride > 0) leaderboard.fetchAll = false;

        ShowLoading(true);
        leaderboard.ManualRefresh(); // Fetch + Cache tetikle
    }

    private void OnDisable()
    {
        if (leaderboard != null)
            leaderboard.OnCacheUpdated -= HandleCacheUpdated;

        ClearList(); // panel kapanınca listeyi temizle (isteğe bağlı)
    }

    // Cache hazır olduğunda tetiklenir
    private void HandleCacheUpdated()
    {
        // 1) KENDİ KARTINI güncelle (her zaman görünür)
        if (selfDisplay != null)
        {
            // İlk 50’de değilse rank boş geliyordu; placeholder olarak "—" kullanalım
            string rank = string.IsNullOrEmpty(leaderboard.MyRankText) ? "—" : leaderboard.MyRankText;
            selfDisplay.SetData(rank, leaderboard.MyUsername, leaderboard.MyScore);
        }

        // 2) TOP LISTESİNİ yeniden kur (Top50’yi—kendin varsa listede yine görünmeye devam)
        RebuildList(leaderboard.TopCached);

        // 3) Loading'i kapat
        ShowLoading(false);
    }

    private void RebuildList(List<UserDatabaseManager.LBEntry> entries)
    {
        ClearList();

        if (entries == null || entries.Count == 0)
            return;

        if (rowPrefab == null || listRoot == null)
        {
            Debug.LogWarning("[UIRankingPanel] rowPrefab veya listRoot eksik.");
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var row = Instantiate(rowPrefab, listRoot);
            _rows.Add(row);

            int rk = e.rank;
            string rankText = rk > 0 ? rk.ToString() : (i + 1).ToString();
            string username = string.IsNullOrWhiteSpace(e.username) ? "Guest" : e.username;
            row.SetData(rankText, username, e.score);
        }
    }

    private void ClearList()
    {
        // Önce izlediğimiz satırları yok et
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i]) Destroy(_rows[i].gameObject);
        }
        _rows.Clear();

        // Güvence için: Content altında kalmış çocukları da temizle
        if (listRoot != null)
        {
            for (int i = listRoot.childCount - 1; i >= 0; i--)
                Destroy(listRoot.GetChild(i).gameObject);
        }
    }

    private void ShowLoading(bool on)
    {
        if (loadingPanel != null) loadingPanel.SetActive(on);
    }

    // İstersen UI butonundan çağırabileceğin manuel yenileme
    public void RefreshNow()
    {
        if (leaderboard == null) return;
        ShowLoading(true);
        leaderboard.ManualRefresh();
    }
}
