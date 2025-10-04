using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerStatsRemoteService : MonoBehaviour
{
    public static PlayerStatsRemoteService Instance { get; private set; }

    [SerializeField] private UserDatabaseManager userDB;
    private string cachedUserID = "";

    // Son bilinen stat JSON (RAM cache)
    public string LatestStatsJson { get; private set; } = string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Uygun yerde çağır: login tamamlandığında (kullanıcı id hazır olduğunda)
    public async Task PreloadOnLoginAsync(string userId)
    {
        cachedUserID = userId;
        if (userDB == null || string.IsNullOrWhiteSpace(userId))
        {
            LatestStatsJson = string.Empty;
            return;
        }

        try
        {
            // UserDB’den Firestore → JSON
            var json = await userDB.FetchPlayerStatsJsonAsync(userId);
            LatestStatsJson = string.IsNullOrWhiteSpace(json) ? string.Empty : json;

            // (İstersen lokal cache de yapabilirsin)
            // PlayerPrefs.SetString("stats_json", LatestStatsJson);
        }
        catch
        {
            // (İstersen PlayerPrefs fallback) LatestStatsJson = PlayerPrefs.GetString("stats_json", string.Empty);
            LatestStatsJson = string.Empty;
        }
    }

    public void ApplyToPlayer(GameObject player, GameplayLogicApplier logic)
    {
        if (player == null) return;

        var stat = player.GetComponent<PlayerStatHandler>();
        var mov = player.GetComponent<PlayerMovement>();
        if (stat == null || mov == null) return;

        // JSON’u ver → ApplyOnRunStart final’leri hesaplayıp uygular
        if (!string.IsNullOrEmpty(LatestStatsJson))
            stat.SetExtrasJson(LatestStatsJson);

        stat.ApplyOnRunStart(logic, mov);
    }
    
    public IEnumerator RefreshLatestCoroutine()
    {
        bool done = false;
        _ = RefreshLatestAsync(() => done = true);
        while (!done) yield return null;
    }

    public async Task RefreshLatestAsync(Action onComplete = null)
    {
        try
        {
            var db = userDB;
            if (db == null) return;
            string uid = cachedUserID;
            if (string.IsNullOrEmpty(uid)) return;

            // KENDİ projendeki gerçek çağrı ile değiştir:
            string json = await db.FetchPlayerStatsJsonAsync(uid);
            if (!string.IsNullOrEmpty(json))
                LatestStatsJson = json;
        }
        finally { onComplete?.Invoke(); }
    }
}