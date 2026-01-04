using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using Firebase.Extensions;
using UnityEngine;

public static class ReferralRemoteService
{
    private static FirebaseFunctions Fn => FirebaseFunctions.GetInstance("us-central1");

    [Serializable]
    public class PendingReferralsResponse
    {
        public bool hasPending;
        public List<PendingItem> items;
        public long total;
    }

    [Serializable]
    public class PendingItem
    {
        public string childUid;
        public string childName;
        public long amount;
    }

    [Serializable]
    public class ClaimResponse
    {
        public long claimed;
        public int count;
    }

    public static async Task<PendingReferralsResponse> GetPendingReferrals()
    {
        try
        {
            var func = Fn.GetHttpsCallable("getPendingReferrals");
            var result = await func.CallAsync();
            var json = result.Data.ToString(); // Assuming result is returned as a JSON-like map or dictionary, we might need manual parsing if it's a Dictionary<string,object>
            
            // Firebase Functions .CallAsync() returns IDictionary<string, object> usually if returning an object.
            // But we need to map it to our class.
            // Let's manually map for safety or use JsonUtility if we assume result is JSON string (it is NOT usually).
            
            var data = result.Data as IDictionary<object, object>;
            if (data == null) return new PendingReferralsResponse { hasPending = false, total = 0 };

            var resp = new PendingReferralsResponse();
            resp.hasPending = GetBool(data, "hasPending");
            resp.total = GetLong(data, "total");
            resp.items = new List<PendingItem>();

            if (data.ContainsKey("items") && data["items"] is List<object> list)
            {
                foreach (var obj in list)
                {
                    if (obj is IDictionary<object, object> d)
                    {
                        resp.items.Add(new PendingItem
                        {
                            childUid = GetString(d, "childUid"),
                            childName = GetString(d, "childName"),
                            amount = GetLong(d, "amount")
                        });
                    }
                }
            }

            return resp;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ReferralService] GetPendingReferrals failed: {e.Message}");
            return new PendingReferralsResponse { hasPending = false, total = 0 };
        }
    }

    public static async Task<ClaimResponse> ClaimReferralEarnings()
    {
        try
        {
            var func = Fn.GetHttpsCallable("claimReferralEarnings");
            var result = await func.CallAsync();
            var data = result.Data as IDictionary<object, object>;
            
            if (data == null) return new ClaimResponse();

            return new ClaimResponse
            {
                claimed = GetLong(data, "claimed"),
                count = GetInt(data, "count")
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"[ReferralService] ClaimReferralEarnings failed: {e.Message}");
            throw; // Re-throw to handle in UI
        }
    }

    // Helpers
    private static bool GetBool(IDictionary<object, object> d, string key) => d.ContainsKey(key) && Convert.ToBoolean(d[key]);
    private static long GetLong(IDictionary<object, object> d, string key) => d.ContainsKey(key) ? Convert.ToInt64(d[key]) : 0;
    private static int GetInt(IDictionary<object, object> d, string key) => d.ContainsKey(key) ? Convert.ToInt32(d[key]) : 0;
    private static string GetString(IDictionary<object, object> d, string key) => d.ContainsKey(key) ? d[key]?.ToString() : "";
}
