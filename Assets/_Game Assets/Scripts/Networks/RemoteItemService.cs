using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Functions;
using UnityEngine;

public static class RemoteItemService
{
    [Serializable]
    public class ItemData
    {
        public string itemName;
        public string itemDescription;
        public string itemIconUrl;
        public double itemDollarPrice;
        public double itemGetPrice;
        public bool itemIsConsumable;
        public bool itemIsRewardedAd;
        public int itemReferralThreshold;
        public double itemstat_coinMultiplierPercent;
        public double itemstat_comboPower;
        public double itemstat_gameplaySpeedMultiplierPercent;
        public double itemstat_magnetPowerPercent;
        public double itemstat_playerAcceleration;
        public double itemstat_playerSizePercent;
        public double itemstat_playerSpeed;
    }

    private static FirebaseFunctions Fn => FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, "us-central1");

    public static async Task<Dictionary<string, ItemData>> FetchAllItemsAsync()
    {
        var call = Fn.GetHttpsCallable("getAllItems");
        var res = await call.CallAsync(new Dictionary<string, object>());

        var root = NormalizeToStringKeyDict(res.Data);
        if (root == null) throw new Exception("[RemoteItemService] Invalid response root");

        var dict = new Dictionary<string, ItemData>();
        if (!root.TryGetValue("items", out var itemsObj) || itemsObj == null)
            return dict;

        var items = NormalizeToStringKeyDict(itemsObj);
        if (items == null) return dict;

        foreach (var kv in items)
        {
            string id = kv.Key;
            var fields = NormalizeToStringKeyDict(kv.Value);
            if (fields == null) continue;

            var data = new ItemData
            {
                itemName        = GetAs<string>(fields, "itemName", string.Empty),
                itemDescription = GetAs<string>(fields, "itemDescription", string.Empty),
                itemIconUrl     = GetAs<string>(fields, "itemIconUrl", string.Empty),
                itemDollarPrice = GetAs<double>(fields, "itemDollarPrice", 0d),
                itemGetPrice    = GetAs<double>(fields, "itemGetPrice", 0d),
                itemIsConsumable    = GetAs<bool>(fields, "itemIsConsumable", false),
                itemIsRewardedAd    = GetAs<bool>(fields, "itemIsRewardedAd", false),
                itemReferralThreshold= GetAs<int>(fields, "itemReferralThreshold", 0),
                itemstat_coinMultiplierPercent            = GetAs<double>(fields, "itemstat_coinMultiplierPercent", 0d),
                itemstat_comboPower                        = GetAs<double>(fields, "itemstat_comboPower", 0d),
                itemstat_gameplaySpeedMultiplierPercent    = GetAs<double>(fields, "itemstat_gameplaySpeedMultiplierPercent", 0d),
                itemstat_magnetPowerPercent                = GetAs<double>(fields, "itemstat_magnetPowerPercent", 0d),
                itemstat_playerAcceleration                = GetAs<double>(fields, "itemstat_playerAcceleration", 0d),
                itemstat_playerSizePercent                 = GetAs<double>(fields, "itemstat_playerSizePercent", 0d),
                itemstat_playerSpeed                       = GetAs<double>(fields, "itemstat_playerSpeed", 0d),
            };

            dict[id] = data;
        }

        return dict;
    }


    private static Dictionary<string, object> NormalizeToStringKeyDict(object dataObj)
    {
        if (dataObj == null) return null;

        if (dataObj is IDictionary<string, object> dso)
            return new Dictionary<string, object>(dso);

        if (dataObj is IDictionary dio)
        {
            var outDict = new Dictionary<string, object>();
            foreach (DictionaryEntry kv in dio)
            {
                var key = kv.Key?.ToString();
                if (!string.IsNullOrEmpty(key)) outDict[key] = kv.Value;
            }
            return outDict;
        }

        return null;
    }

    private static T GetAs<T>(IDictionary<string, object> dict, string key, T fallback = default)
    {
        if (dict == null) return fallback;
        if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
        try
        {
            if (typeof(T) == typeof(string))  return (T)(object)v.ToString();
            if (typeof(T) == typeof(bool))    return (T)(object)Convert.ToBoolean(v);
            if (typeof(T) == typeof(int))     return (T)(object)Convert.ToInt32(v);
            if (typeof(T) == typeof(long))    return (T)(object)Convert.ToInt64(v);
            if (typeof(T) == typeof(float))   return (T)(object)Convert.ToSingle(v);
            if (typeof(T) == typeof(double))  return (T)(object)Convert.ToDouble(v);
            return (T)v;
        }
        catch { return fallback; }
    }
}