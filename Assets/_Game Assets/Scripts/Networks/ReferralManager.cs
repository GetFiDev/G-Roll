using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkingData;
using UnityEngine;

public class ReferralManager : MonoBehaviour
{
    public UserDatabaseManager userDB;
    public event Action OnCacheUpdated;

    public List<ReferredUser> Cached = new();
    public float TotalEarned { get; private set; }
    public string MyReferralKey { get; private set; } = "-";

    private const string PREF_KEY_REFERRAL_CODE = "MyReferralCodeCache";

    public async Task RefreshCacheAsync(int limit = 100, bool includeEarnings = true)
    {
        if (userDB == null) return;

        // Try load from cache first if missing
        if (string.IsNullOrWhiteSpace(MyReferralKey) || MyReferralKey == "-")
        {
            var cachedCode = PlayerPrefs.GetString(PREF_KEY_REFERRAL_CODE, "-");
            if (!string.IsNullOrWhiteSpace(cachedCode) && cachedCode != "-")
            {
                MyReferralKey = cachedCode;
                OnCacheUpdated?.Invoke();
            }
        }

        var list  = await userDB.ListMyReferredUsersAsync(limit, includeEarnings);
        var total = await userDB.FetchMyReferralEarningsAsync();
        var code  = await userDB.GetReferralKeyAsync();

        Cached = list ?? new List<ReferredUser>();
        TotalEarned = total;
        
        if (!string.IsNullOrWhiteSpace(code) && code != "-")
        {
            MyReferralKey = code;
            // Update local cache
            PlayerPrefs.SetString(PREF_KEY_REFERRAL_CODE, code);
            PlayerPrefs.Save();
        }
        else if (string.IsNullOrWhiteSpace(MyReferralKey) || MyReferralKey == "-")
        {
             // If server returned nothing and we have nothing, set to default
             MyReferralKey = "-";
        }
        // If server returned nothing but we had something in memory (or cache), keep it? 
        // Usually server is authority. If server returns empty/null, it might mean no code exists.
        // However, GetReferralKeyAsync returns "-" on failure/empty. 
        // Let's assume if 'code' is valid, we use it. If 'code' is "-", we kept what we had? 
        // Safer: Logic says "code" from server is authority. But if it fails (returns "-"), maybe keep old?
        // Let's stick to: if code is valid, update. 
        
        OnCacheUpdated?.Invoke();
    }

    public void Reset()
    {
        Cached.Clear();
        TotalEarned = 0;
        MyReferralKey = "-";
        OnCacheUpdated?.Invoke();
    }
}