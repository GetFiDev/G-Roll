using System;
using System.Threading.Tasks;
using Firebase.Functions;
using Firebase;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public static class SessionRemoteService
{
    private static FirebaseFunctions Fn => FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, "us-central1");

    private static string DumpDict(Dictionary<string, object> dict)
    {
        if (dict == null) return "<null>";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("{");
        bool first = true;
        foreach (var kv in dict)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(kv.Key).Append(":").Append(kv.Value);
        }
        sb.Append("}");
        return sb.ToString();
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
                var key = kv.Key?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(key)) continue;
                outDict[key] = kv.Value;
            }
            return outDict;
        }
        return null;
    }

    private static T GetAs<T>(Dictionary<string, object> dict, string key, T fallback = default)
    {
        if (dict == null) return fallback;
        if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
        try
        {
            if (typeof(T) == typeof(string)) return (T)(object)v.ToString();
            if (typeof(T) == typeof(bool)) return (T)(object)Convert.ToBoolean(v);
            if (typeof(T) == typeof(int)) return (T)(object)Convert.ToInt32(v);
            if (typeof(T) == typeof(long)) return (T)(object)Convert.ToInt64(v);
            if (typeof(T) == typeof(float)) return (T)(object)Convert.ToSingle(v);
            if (typeof(T) == typeof(double)) return (T)(object)Convert.ToDouble(v);
            return (T)v;
        }
        catch { return fallback; }
    }

    [Serializable]
    public class RequestSessionResponse
    {
        public bool ok;
        public string sessionId;
        public int energyCurrent;
        public int energyMax;
        public int regenPeriodSec;
        public string nextEnergyAt; // ISO
        public long nextEnergyAtMillis; // optional
    }

    public static async Task<RequestSessionResponse> RequestSessionAsync()
    {
        try
        {
            Debug.Log("[SessionRemoteService] requestSession → call (region=us-central1)");
            var call = Fn.GetHttpsCallable("requestSession");
            var res = await call.CallAsync(new Dictionary<string, object>()); // <-- boş payload gönder
            var dataObj = res.Data;
            Debug.Log($"[SessionRemoteService] requestSession raw type: {(dataObj==null?"<null>":dataObj.GetType().FullName)}");
            var dict = NormalizeToStringKeyDict(dataObj);
            Debug.Log($"[SessionRemoteService] requestSession ← ok payload: {DumpDict(dict)}");
            if (dict == null) throw new Exception("Bad response: null dict");

            var ok = GetAs<bool>(dict, "ok", false);
            if (!ok) throw new Exception("ok=false from server");

            long nextMs = GetAs<long>(dict, "nextEnergyAtMillis", 0L);
            if (nextMs == 0L)
            {
                // backward compat (if server returned ISO or other shape)
                var iso = GetAs<string>(dict, "nextEnergyAt", null);
                if (iso != null && long.TryParse(iso, out var parsed)) nextMs = parsed;
            }

            return new RequestSessionResponse {
                ok = ok,
                sessionId = GetAs<string>(dict, "sessionId", null),
                energyCurrent = GetAs<int>(dict, "energyCurrent", 0),
                energyMax = GetAs<int>(dict, "energyMax", 0),
                regenPeriodSec = GetAs<int>(dict, "regenPeriodSec", 0),
                nextEnergyAt = GetAs<string>(dict, "nextEnergyAt", null),
                nextEnergyAtMillis = nextMs
            };
        }
        catch (Firebase.Functions.FunctionsException fex)
        {
            Debug.LogWarning($"[SessionRemoteService] requestSession FAILED: code={fex.ErrorCode} msg={fex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SessionRemoteService] requestSession FAILED: {ex.GetType().Name} {ex.Message}");
            throw;
        }
    }

    [Serializable]
    public class SubmitResultResponse
    {
        public bool alreadyProcessed;
        public double currency;
        public double maxScore;
    }

    public static async Task<SubmitResultResponse> SubmitResultAsync(string sessionId, double earnedCurrency, double earnedScore)
    {
        try
        {
            Debug.Log($"[SessionRemoteService] submitSessionResult → call sessionId={sessionId} currency={earnedCurrency} score={earnedScore}");
            var call = Fn.GetHttpsCallable("submitSessionResult");
            var res = await call.CallAsync(new Dictionary<string, object>
            {
                { "sessionId", sessionId },
                { "earnedCurrency", earnedCurrency },
                { "earnedScore", earnedScore }
            });
            var dict = NormalizeToStringKeyDict(res.Data);
            Debug.Log($"[SessionRemoteService] submitSessionResult ← ok payload: {DumpDict(dict)}");
            if (dict == null) throw new Exception("Bad response: null dict");

            return new SubmitResultResponse{
                alreadyProcessed = GetAs<bool>(dict, "alreadyProcessed", false),
                currency = GetAs<double>(dict, "currency", 0d),
                maxScore = GetAs<double>(dict, "maxScore", 0d)
            };
        }
        catch (Firebase.Functions.FunctionsException fex)
        {
            Debug.LogWarning($"[SessionRemoteService] submitSessionResult FAILED: code={fex.ErrorCode} msg={fex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SessionRemoteService] submitSessionResult FAILED: {ex.GetType().Name} {ex.Message}");
            throw;
        }
    }
}