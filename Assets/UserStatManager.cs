using UnityEngine;
using System;
using System.Threading.Tasks;

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
    /// </summary>
    public async Task<UserData> RefreshAllAsync()
    {
        if (manager == null)
        {
            Debug.LogWarning("[UserStatManager] manager ref yok");
            return null;
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
        return data?.currency ?? 0f;
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
