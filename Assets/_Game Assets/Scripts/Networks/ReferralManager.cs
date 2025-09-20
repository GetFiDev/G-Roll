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

    public async Task RefreshCacheAsync(int limit = 100, bool includeEarnings = true)
    {
        if (userDB == null) return;

        var list  = await userDB.ListMyReferredUsersAsync(limit, includeEarnings);
        var total = await userDB.FetchMyReferralEarningsAsync();
        var code  = await userDB.GetReferralKeyAsync();

        Cached = list ?? new List<ReferredUser>();
        TotalEarned = total;
        MyReferralKey = string.IsNullOrWhiteSpace(code) ? "-" : code;

        OnCacheUpdated?.Invoke();
    }
}