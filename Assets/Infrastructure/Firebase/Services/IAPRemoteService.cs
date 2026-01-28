using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase.Interfaces;
using UnityEngine;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// IAP remote service implementation.
    /// Firebase Functions ile satin alma dogrulama.
    /// </summary>
    public class IAPRemoteService : IIAPRemoteService
    {
        private readonly IGRollLogger _logger;
        private FirebaseFunctions _functions;

        private const string Region = "us-central1";

        [Inject]
        public IAPRemoteService(IGRollLogger logger)
        {
            _logger = logger;
        }

        private FirebaseFunctions Functions =>
            _functions ??= FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, Region);

        public async UniTask<IAPVerifyResponse> VerifyPurchaseAsync(string productId, string receipt)
        {
            try
            {
                _logger.Log($"[IAPRemoteService] Verifying purchase: {productId}");

                var callable = Functions.GetHttpsCallable("verifyPurchase");

                var data = new Dictionary<string, object>
                {
                    { "productId", productId },
                    { "receipt", receipt },
                    { "deviceId", SystemInfo.deviceUniqueIdentifier },
                    { "platform", Application.platform.ToString() }
                };

                var result = await callable.CallAsync(data);

                var dict = NormalizeToStringKeyDict(result.Data);
                if (dict == null)
                {
                    return IAPVerifyResponse.Failed("Empty response from server");
                }

                bool success = GetAs<bool>(dict, "success", false);
                string message = GetAs<string>(dict, "message", "No message");

                // Extract rewards if any
                Dictionary<string, object> rewards = null;
                if (dict.TryGetValue("rewards", out var r) && r is IDictionary<string, object> rDict)
                {
                    rewards = new Dictionary<string, object>(rDict);
                }
                else if (dict.TryGetValue("rewards", out var r2) && r2 is IDictionary rIDict)
                {
                    rewards = new Dictionary<string, object>();
                    foreach (DictionaryEntry kv in rIDict)
                    {
                        if (kv.Key != null)
                            rewards[kv.Key.ToString()] = kv.Value;
                    }
                }

                if (success)
                {
                    _logger.Log($"[IAPRemoteService] Purchase verified: {message}");
                    return IAPVerifyResponse.Succeeded(message, rewards);
                }
                else
                {
                    _logger.LogWarning($"[IAPRemoteService] Verification failed: {message}");
                    return IAPVerifyResponse.Failed(message);
                }
            }
            catch (FunctionsException fex)
            {
                _logger.LogError($"[IAPRemoteService] Firebase error: {fex.ErrorCode} - {fex.Message}");
                return IAPVerifyResponse.Failed(fex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[IAPRemoteService] Error: {ex.Message}");
                return IAPVerifyResponse.Failed(ex.Message);
            }
        }

        #region Helpers

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
                    if (!string.IsNullOrEmpty(key))
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
            catch
            {
                return fallback;
            }
        }

        #endregion
    }
}
