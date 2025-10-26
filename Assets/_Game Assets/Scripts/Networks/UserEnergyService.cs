using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Functions;
using UnityEngine;

/// <summary>
/// Cloud Functions üzerinden enerji snapshot'ını çeken basit gateway.
/// MonoBehaviour DEĞİL; herhangi bir GameObject'e eklenmez.
/// </summary>
public static class UserEnergyService
{
    // --- DTO ---
    [Serializable]
    public struct EnergySnapshot
    {
        public int current;             // energyCurrent
        public int max;                 // energyMax
        public int regenPeriodSec;      // energyRegenPeriodSec
        public long nextEnergyAtMillis; // nextEnergyAtMillis (ms epoch) - yoksa 0
    }

    // Functions region (sunucuda us-central1)
    private static FirebaseFunctions Fn =>
        FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, "us-central1");

    /// <summary>
    /// getEnergySnapshot callable'ına gider; server tarafında lazy regen çalışır ve güncel enerji döner.
    /// </summary>
    public static async Task<EnergySnapshot> FetchSnapshotAsync()
    {
        EnsureSignedIn(); // guard

        var call = Fn.GetHttpsCallable("getEnergySnapshot");

        // IL2CPP/AOT’ta anonim tip kullanma; boş Dictionary gönder.
        var res = await call.CallAsync(new Dictionary<string, object>());

        // Unity SDK, res.Data'yı Dictionary<object,object> olarak döndürebilir → normalize et
        var dict = NormalizeToStringKeyDict(res.Data);
        if (dict == null) throw new Exception("[UserEnergyService] Null payload");

        var ok = GetAs<bool>(dict, "ok", false);
        if (!ok) throw new Exception("[UserEnergyService] ok=false");

        return new EnergySnapshot
        {
            current           = GetAs<int>(dict, "energyCurrent", 0),
            max               = GetAs<int>(dict, "energyMax", 6),
            regenPeriodSec    = GetAs<int>(dict, "regenPeriodSec", 14400),
            nextEnergyAtMillis= GetAs<long>(dict, "nextEnergyAtMillis", 0L),
        };
    }

    // --------- Helpers ---------

    private static void EnsureSignedIn()
    {
        var auth = FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser == null)
            throw new Exception("[UserEnergyService] Not signed-in");
    }

    // res.Data bazen IDictionary<object,object> olur → string key'e çevir
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
                var k = kv.Key?.ToString();
                if (!string.IsNullOrEmpty(k)) outDict[k] = kv.Value;
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
        catch
        {
            return fallback;
        }
    }
}