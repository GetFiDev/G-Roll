using UnityEngine;
using System;
using System.Threading.Tasks;
using NetworkingData;

public class UserStatManager : MonoBehaviour
{
    [Header("Refs")]
    public UserDatabaseManager manager;

    // Son bilinen veriyi isteyen HUD’ler için
    public UserData Last { get; private set; }

    // Tüm veriler tazelendiğinde yayınlanır (HUD’ler bağlanabilir)
    public event Action<UserData> OnStatsRefreshed;

    private void OnEnable()
    {
        if (manager == null)
        {
            Debug.LogError("userdbmanager not found");
        }

        if (manager != null)
        {
            manager.OnLoginSucceeded += HandleLoginSucceeded;
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnLoginSucceeded -= HandleLoginSucceeded;
        }
    }

    private async void HandleLoginSucceeded()
    {
        // Login olur olmaz tüm veriyi çek
        await RefreshAllAsync();
    }

    /// <summary>
    /// Firestore'dan tüm UserData'yı çeker ve Last'i günceller.
    /// If useCachedIfAvailable is true and UserDatabaseManager has recent cached data,
    /// it will use that instead of fetching fresh data (optimization for boot sequence).
    /// </summary>
    public async Task<UserData> RefreshAllAsync(bool useCachedIfAvailable = true)
    {
        if (manager == null)
        {
            Debug.LogWarning("[UserStatManager] manager ref yok");
            return null;
        }

        // Optimization: If cached data is available and recent, use it
        // This avoids duplicate Firestore reads during boot sequence
        if (useCachedIfAvailable && manager.currentUserData != null)
        {
            Last = manager.currentUserData;
            OnStatsRefreshed?.Invoke(Last);
            Debug.Log("[UserStatManager] Using cached UserData (skipping fetch)");
            return Last;
        }

        var data = await manager.LoadUserData(); // Firestore -> POCO
        Last = data;
        OnStatsRefreshed?.Invoke(Last);
        return data;
    }

    // ---- Tekil getter'lar: her çağrıda Firestore'dan canlı veri çeker ----

    public async Task<float> GetCurrencyAsync()
    {
        var data = await manager.LoadUserData(); // her çağrıda fetch
        Last = data;
        OnStatsRefreshed?.Invoke(Last);
        return (float)(data?.currency ?? 0f);
    }

    public async Task<string> GetUsernameAsync()
    {
        var data = await manager.LoadUserData();
        Last = data;
        OnStatsRefreshed?.Invoke(Last);
        return data?.username ?? "";
    }

    public async Task<string> GetMailAsync()
    {
        var data = await manager.LoadUserData();
        Last = data;
        OnStatsRefreshed?.Invoke(Last);
        return data?.mail ?? "";
    }

    public async Task<bool> GetHasElitePassAsync()
    {
        var data = await manager.LoadUserData();
        Last = data;
        OnStatsRefreshed?.Invoke(Last);
        return data?.hasElitePass ?? false;
    }
}
