using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

/// <summary>
/// Tek kapı (gate) yaklaşımıyla Streak durumunu yöneten servis.
/// UI yalnızca FetchAsync() ve ClaimAsync() ile çalışır.
/// </summary>
public static class StreakService
{
    // ==== Public DTO’lar ====
    [Serializable]
    public struct StreakSnapshot
    {
        public long serverNowMillis;
        public long nextUtcMidnightMillis;

        public int totalDays;
        public int unclaimedDays;
        public double rewardPerDay;
        public double pendingTotalReward;

        public bool claimAvailable;
        public bool todayCounted;

        /// <summary> Cihaz-sunucu saat farkı: serverNow - deviceNowAtFetch </summary>
        public long serverOffsetMs;

        public bool ok;
        public string error; // doluysa hata mesajı (network / functions)
    }

    [Serializable]
    public struct ClaimResult
    {
        public bool ok;
        public double granted;
        public double rewardPerDay;
        public int unclaimedDaysAfter;
        public double newCurrency;
        public string error;

        // Claim sonrası taze durum (isteğe bağlı: UI hemen yenileyebilir)
        public StreakSnapshot freshStatus;
    }

    // ==== Ayarlar ====
    private const string FnGetStreakStatus = "getStreakStatus";
    private const string FnClaimStreak     = "claimStreak";

    // ========= Tek Gate =========
    /// <summary>
    /// Panel açılışında çağır: (sunucu tarafı getStreakStatus içinde idempotent increment yapıyor).
    /// </summary>
    public static async Task<StreakSnapshot> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            // Tek kapı: Sunucuda getStreakStatus, applyDailyStreakIncrement(uid) çağırır (idempotent)
            var status = await CallNoPayloadAsync(FnGetStreakStatus, ct);
            return MapStatusToSnapshot(status);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StreakService] FetchAsync failed: {ex.Message}");
            return new StreakSnapshot { ok = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Claim akışı: claimStreak → ardından taze getStreakStatus (UI için).
    /// </summary>
    public static async Task<ClaimResult> ClaimAsync(CancellationToken ct = default)
    {
        var result = new ClaimResult();
        try
        {
            var claimResp = await CallNoPayloadAsync(FnClaimStreak, ct);
            // claim yanıtı:
            // { ok, granted, rewardPerDay, unclaimedDays, newCurrency }
            result.ok = GetBool(claimResp, "ok", true);
            result.granted = GetDouble(claimResp, "granted", 0);
            result.rewardPerDay = GetDouble(claimResp, "rewardPerDay", 0);
            result.unclaimedDaysAfter = GetInt(claimResp, "unclaimedDays", 0);
            result.newCurrency = GetDouble(claimResp, "newCurrency", 0);

            // Hemen taze durum
            var status = await CallNoPayloadAsync(FnGetStreakStatus, ct);
            result.freshStatus = MapStatusToSnapshot(status);
        }
        catch (Exception ex)
        {
            result.ok = false;
            result.error = ex.Message;
            Debug.LogWarning($"[StreakService] ClaimAsync failed: {ex.Message}");
        }
        return result;
    }

    // ========= Helpers =========
    private static async Task<IDictionary<string, object>> CallNoPayloadAsync(string fnName, CancellationToken ct)
    {
        var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable(fnName);
        var startMs = NowMillis();
        var resp = await callable.CallAsync(null);
        ct.ThrowIfCancellationRequested();

        var dict = CoerceToStringObjectDict(resp?.Data);
        if (dict == null || dict.Count == 0)
            throw new InvalidOperationException($"{fnName} returned no data");

        return dict;
    }

    /// <summary>
    /// Firebase Functions result.Data tipini güvenli biçimde IDictionary<string, object>’e dönüştürür.
    /// Bazı platformlarda Dictionary<object, object> veya generic olmayan IDictionary gelebiliyor.
    /// </summary>
    private static IDictionary<string, object> CoerceToStringObjectDict(object data)
    {
        if (data == null) return null;

        // En yaygın durum
        if (data is IDictionary<string, object> sdict)
            return sdict;

        // Generic olmayan IDictionary (DictionaryEntry)
        if (data is System.Collections.IDictionary idict)
        {
            var result = new Dictionary<string, object>();
            foreach (System.Collections.DictionaryEntry de in idict)
            {
                var key = de.Key?.ToString();
                if (string.IsNullOrEmpty(key)) continue;
                result[key] = de.Value;
            }
            return result;
        }

        // IEnumerable<KeyValuePair<,>> olarak gelirse
        if (data is System.Collections.IEnumerable enumerable)
        {
            var result = new Dictionary<string, object>();
            foreach (var item in enumerable)
            {
                var t = item?.GetType();
                if (t == null) continue;
                var isKvp = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
                if (!isKvp) continue;
                var kProp = t.GetProperty("Key");
                var vProp = t.GetProperty("Value");
                var kObj = kProp?.GetValue(item, null);
                var vObj = vProp?.GetValue(item, null);
                var key = kObj?.ToString();
                if (string.IsNullOrEmpty(key)) continue;
                result[key] = vObj;
            }
            if (result.Count > 0) return result;
        }

        // Son çare: tek başına primitive/string döndüyse bir kab’a koy
        if (data is string str)
            return new Dictionary<string, object> { { "value", str } };

        return null;
    }

    private static StreakSnapshot MapStatusToSnapshot(IDictionary<string, object> status)
    {
        // getStreakStatus server alanları:
        // ok, serverNowMillis, nextUtcMidnightMillis, totalDays, unclaimedDays,
        // lastLoginDate, claimAvailable, rewardPerDay, pendingTotalReward, todayCounted

        var ok = GetBool(status, "ok", true);
        var serverNow = GetLong(status, "serverNowMillis", NowMillis());
        var nextMid   = GetLong(status, "nextUtcMidnightMillis", 0);

        var deviceNowAtFetch = NowMillis();
        var offset = serverNow - deviceNowAtFetch;

        return new StreakSnapshot
        {
            ok = ok,
            error = ok ? null : "not-ok",
            serverNowMillis = serverNow,
            nextUtcMidnightMillis = nextMid,
            totalDays = GetInt(status, "totalDays", 0),
            unclaimedDays = GetInt(status, "unclaimedDays", 0),
            rewardPerDay = GetDouble(status, "rewardPerDay", 0),
            pendingTotalReward = GetDouble(status, "pendingTotalReward", 0),
            claimAvailable = GetBool(status, "claimAvailable", false),
            todayCounted = GetBool(status, "todayCounted", false),
            serverOffsetMs = offset
        };
    }

    private static long NowMillis() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static bool GetBool(IDictionary<string, object> d, string k, bool def = false)
    {
        if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
        if (v is bool b) return b;
        if (v is int i) return i != 0;
        if (v is long l) return l != 0L;
        if (bool.TryParse(v.ToString(), out var pb)) return pb;
        return def;
    }

    private static int GetInt(IDictionary<string, object> d, string k, int def = 0)
    {
        if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is double dbl) return (int)Math.Round(dbl);
        if (int.TryParse(v.ToString(), out var pi)) return pi;
        return def;
    }

    private static long GetLong(IDictionary<string, object> d, string k, long def = 0)
    {
        if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
        if (v is long l) return l;
        if (v is int i) return i;
        if (v is double dbl) return (long)Math.Round(dbl);
        if (long.TryParse(v.ToString(), out var pl)) return pl;
        return def;
    }

    private static double GetDouble(IDictionary<string, object> d, string k, double def = 0)
    {
        if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
        switch (v)
        {
            case double dbl:
                return dbl;
            case float f:
                return (double)f;
            case long l:
                return (double)l;
            case int i:
                return (double)i;
            case string s when double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ps):
                return ps;
            default:
                return def;
        }
    }
}