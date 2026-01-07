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
    public float PendingTotal { get; private set; }
    public int GlobalReferralCount { get; private set; }
    public bool IsPendingLoaded { get; private set; }
    public string MyReferralKey { get; private set; } = "-";

    // Use the central constant from UserDatabaseManager
    private string PREF_KEY_REFERRAL_CODE => UserDatabaseManager.PREF_KEY_REFERRAL_CODE;

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
            // Fallback: If PlayerPrefs was empty, but UserDatabaseManager holds fresh data in memory (from Login)
            else if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
            {
                var memoryCode = UserDatabaseManager.Instance.currentUserData.referralKey;
                if (!string.IsNullOrWhiteSpace(memoryCode) && memoryCode != "-")
                {
                    MyReferralKey = memoryCode;
                    // Also sync to prefs now
                    PlayerPrefs.SetString(PREF_KEY_REFERRAL_CODE, memoryCode);
                    OnCacheUpdated?.Invoke();
                }
            }
        }

        // Parallelize fetching to speed up loading and prevent hanging on one slow request
        var tList = userDB.ListMyReferredUsersAsync(limit, includeEarnings);
        var tTotal = userDB.FetchMyReferralEarningsAsync();
        var tCode = userDB.GetReferralKeyAsync();
        var tCount = userDB.GetReferralCountAsync();
        var tPending = RefreshPendingAsync();

        await Task.WhenAll(tList, tTotal, tCode, tCount, tPending);

        var list = tList.Result;
        var total = tTotal.Result;
        var code = tCode.Result;
        int count = tCount.Result;
        // PendingTotal is updated inside RefreshPendingAsync, so tPending result is void/Task.

        Cached = list ?? new List<ReferredUser>();
        TotalEarned = total;
        GlobalReferralCount = count;
        
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
        PendingTotal = 0;
        GlobalReferralCount = 0;
        IsPendingLoaded = false;
        MyReferralKey = "-";
        OnCacheUpdated?.Invoke();
    }

    public async Task RefreshPendingAsync()
    {
        if (userDB == null) return;
        var (has, total, items) = await userDB.GetPendingReferralsAsync();
        PendingTotal = total;
        IsPendingLoaded = true;
        
        // If we want to store detailed items, we can add a list property later.
        // For now, UI only asked for total.
    }

    public async Task<float> ClaimEarningsAsync()
    {
         if (userDB == null) return 0f;
         float claimed = await userDB.ClaimReferralEarningsAsync();
         
         if (claimed > 0)
         {
             // Update local state immediately
             TotalEarned += claimed;
             PendingTotal = Mathf.Max(0, PendingTotal - claimed);
             OnCacheUpdated?.Invoke();
         }
         return claimed;
    }
}