using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using Firebase;
using UnityEngine;

public static class IAPRemoteService
{
    private static FirebaseFunctions Fn => FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, "us-central1");

    [Serializable]
    public class VerifyPurchaseResponse
    {
        public bool success;
        public string message;
        public Dictionary<string, object> rewards; // e.g. "diamonds": 500
    }

    /// <summary>
    /// Sends the purchase receipt to the server for verification and entitlement granting.
    /// </summary>
    /// <param name="productId">The product ID purchased.</param>
    /// <param name="receipt">The full receipt string from Unity IAP.</param>
    /// <returns>VerifyPurchaseResponse with success status and rewards.</returns>
    public static async Task<VerifyPurchaseResponse> VerifyPurchaseAsync(string productId, string receipt)
    {
        try
        {
            Debug.Log($"[IAPRemoteService] VerifyPurchaseAsync -> productId={productId}");
            
            // Note: The cloud function name 'verifyPurchase' is assumed.
            // Ensure this matches your backend deployment.
            var call = Fn.GetHttpsCallable("verifyPurchase");
            
            var data = new Dictionary<string, object>
            {
                { "productId", productId },
                { "receipt", receipt }, // Send the full receipt; backend parses it.
                { "deviceId", SystemInfo.deviceUniqueIdentifier },
                { "platform", Application.platform.ToString() }
            };

            var res = await call.CallAsync(data);
            
            var dataObj = res.Data;
            var dict = NormalizeToStringKeyDict(dataObj);

            if (dict == null) return new VerifyPurchaseResponse { success = false, message = "Empty response from server" };

            bool success = GetAs<bool>(dict, "success", false);
            string message = GetAs<string>(dict, "message", "No message");
            
            // Extract rewards if any
            Dictionary<string, object> rewards = null;
            if (dict.TryGetValue("rewards", out var r) && r is IDictionary<string, object> rDict)
            {
                rewards = new Dictionary<string, object>(rDict);
            }
            else if (dict.TryGetValue("rewards", out var r2) && r2 is System.Collections.IDictionary rIDict)
            {
                 // Convert legacy IDictionary if needed
                 rewards = new Dictionary<string, object>();
                 foreach(System.Collections.DictionaryEntry kv in rIDict)
                 {
                     if(kv.Key != null) rewards[kv.Key.ToString()] = kv.Value;
                 }
            }

            return new VerifyPurchaseResponse
            {
                success = success,
                message = message,
                rewards = rewards
            };
        }
        catch (Firebase.Functions.FunctionsException fex)
        {
            Debug.LogError($"[IAPRemoteService] Verification Failed (FunctionsException): {fex.ErrorCode} - {fex.Message}");
            return new VerifyPurchaseResponse { success = false, message = fex.Message };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IAPRemoteService] Verification Failed (Exception): {ex.Message}");
            return new VerifyPurchaseResponse { success = false, message = ex.Message };
        }
    }

    // --- Helpers Copied/Adapted from SessionRemoteService ---

    private static Dictionary<string, object> NormalizeToStringKeyDict(object dataObj)
    {
        if (dataObj == null) return null;
        if (dataObj is IDictionary<string, object> dso)
            return new Dictionary<string, object>(dso);
        if (dataObj is System.Collections.IDictionary dio)
        {
            var outDict = new Dictionary<string, object>();
            foreach (System.Collections.DictionaryEntry kv in dio)
            {
                var key = kv.Key?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(key)) outDict[key] = kv.Value;
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
}
