using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Autopilot remote service implementation.
    /// Firebase Functions ile otomatik kazanç sistemi yönetimi.
    /// </summary>
    public class AutopilotRemoteService : IAutopilotRemoteService
    {
        private readonly IGRollLogger _logger;
        private FirebaseFunctions _functions;

        private const string Region = "us-central1";
        private const string FnStatus = "getAutopilotStatus";
        private const string FnToggle = "toggleAutopilot";
        private const string FnClaim = "claimAutopilot";

        [Inject]
        public AutopilotRemoteService(IGRollLogger logger)
        {
            _logger = logger;
        }

        private FirebaseFunctions Functions => _functions ??= FirebaseFunctions.GetInstance(Region);

        public async UniTask<AutopilotStatus> GetStatusAsync()
        {
            try
            {
                _logger.Log("[AutopilotRemoteService] Getting status");

                var callable = Functions.GetHttpsCallable(FnStatus);
                var result = await callable.CallAsync(null);

                var dict = ToStringObjectDict(result?.Data);
                if (dict == null)
                {
                    return new AutopilotStatus { Ok = false, Error = "Empty response" };
                }

                // Some backends wrap with a 'status' field; unwrap if present
                if (dict.TryGetValue("status", out var statusObj))
                {
                    var inner = ToStringObjectDict(statusObj);
                    if (inner != null)
                        dict = inner;
                }

                return ParseStatus(dict);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AutopilotRemoteService] GetStatus error: {ex.Message}");
                return new AutopilotStatus { Ok = false, Error = ex.Message };
            }
        }

        public async UniTask<AutopilotToggleResult> ToggleAsync(bool on)
        {
            try
            {
                _logger.Log($"[AutopilotRemoteService] Toggle autopilot: {on}");

                var callable = Functions.GetHttpsCallable(FnToggle);
                var payload = new Dictionary<string, object> { ["on"] = on };
                var result = await callable.CallAsync(payload);

                var dict = ToStringObjectDict(result?.Data);
                if (dict == null)
                {
                    return new AutopilotToggleResult { Ok = false, Error = "Empty response" };
                }

                // Optional unwrap
                if (dict.TryGetValue("result", out var resultObj))
                {
                    var inner = ToStringObjectDict(resultObj);
                    if (inner != null)
                        dict = inner;
                }

                return new AutopilotToggleResult
                {
                    Ok = GetBool(dict, "ok"),
                    IsAutopilotOn = GetBool(dict, "isAutopilotOn")
                };
            }
            catch (FunctionsException fex)
            {
                _logger.LogError($"[AutopilotRemoteService] Toggle error: {fex.Message}");
                return new AutopilotToggleResult { Ok = false, Error = fex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AutopilotRemoteService] Toggle error: {ex.Message}");
                return new AutopilotToggleResult { Ok = false, Error = ex.Message };
            }
        }

        public async UniTask<AutopilotClaimResult> ClaimAsync()
        {
            try
            {
                _logger.Log("[AutopilotRemoteService] Claiming autopilot earnings");

                var callable = Functions.GetHttpsCallable(FnClaim);
                var result = await callable.CallAsync(null);

                var dict = ToStringObjectDict(result?.Data);
                if (dict == null)
                {
                    return new AutopilotClaimResult { Ok = false, Error = "Empty response" };
                }

                // Optional unwrap
                if (dict.TryGetValue("result", out var resultObj))
                {
                    var inner = ToStringObjectDict(resultObj);
                    if (inner != null)
                        dict = inner;
                }

                var claimResult = new AutopilotClaimResult
                {
                    Ok = GetBool(dict, "ok"),
                    Claimed = GetLong(dict, "claimed"),
                    CurrencyAfter = GetLong(dict, "currencyAfter")
                };

                _logger.Log($"[AutopilotRemoteService] Claimed: {claimResult.Claimed}");
                return claimResult;
            }
            catch (FunctionsException fex)
            {
                // Map specific precondition to domain error
                if (fex.ErrorCode == FunctionsErrorCode.FailedPrecondition &&
                    fex.Message?.IndexOf("Not ready to claim", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new AutopilotClaimResult { Ok = false, Error = "NOT_READY_TO_CLAIM" };
                }

                _logger.LogError($"[AutopilotRemoteService] Claim error: {fex.Message}");
                return new AutopilotClaimResult { Ok = false, Error = fex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AutopilotRemoteService] Claim error: {ex.Message}");
                return new AutopilotClaimResult { Ok = false, Error = ex.Message };
            }
        }

        #region Helpers

        private static AutopilotStatus ParseStatus(IDictionary<string, object> m)
        {
            return new AutopilotStatus
            {
                Ok = GetBool(m, "ok"),
                ServerNowMillis = GetLong(m, "serverNowMillis"),
                IsElite = GetBool(m, "isElite"),
                IsAutopilotOn = GetBool(m, "isAutopilotOn"),
                AutopilotWallet = GetDouble(m, "autopilotWallet"),
                Currency = GetLong(m, "currency"),
                NormalUserEarningPerHour = GetDouble(m, "normalUserEarningPerHour"),
                EliteUserEarningPerHour = GetDouble(m, "eliteUserEarningPerHour"),
                NormalUserMaxAutopilotDurationInHours = GetDouble(m, "normalUserMaxAutopilotDurationInHours"),
                AutopilotActivationDateMillis = GetNullableLong(m, "autopilotActivationDateMillis"),
                AutopilotLastClaimedAtMillis = GetLong(m, "autopilotLastClaimedAtMillis"),
                TimeToCapSeconds = GetNullableLong(m, "timeToCapSeconds"),
                IsClaimReady = GetBool(m, "isClaimReady")
            };
        }

        private static IDictionary<string, object> ToStringObjectDict(object data)
        {
            if (data == null) return null;

            if (data is IDictionary<string, object> sdict)
                return sdict;

            if (data is Dictionary<string, object> sdict2)
                return sdict2;

            if (data is IDictionary<object, object> odict)
            {
                var conv = new Dictionary<string, object>();
                foreach (var kv in odict)
                {
                    var key = kv.Key?.ToString();
                    if (!string.IsNullOrEmpty(key))
                        conv[key] = kv.Value;
                }
                return conv;
            }

            if (data is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                var conv = new Dictionary<string, object>();
                foreach (var kv in pairs)
                    conv[kv.Key] = kv.Value;
                return conv;
            }

            return null;
        }

        private static bool GetBool(IDictionary<string, object> m, string k)
        {
            if (!m.TryGetValue(k, out var v) || v == null) return false;
            return v switch
            {
                bool b => b,
                string s => bool.TryParse(s, out var p) && p,
                int i => i != 0,
                long l => l != 0,
                _ => false
            };
        }

        private static long GetLong(IDictionary<string, object> m, string k, long def = 0)
        {
            if (!m.TryGetValue(k, out var v) || v == null) return def;
            return v switch
            {
                long l => l,
                int i => i,
                double d => (long)Math.Floor(d),
                float f => (long)Math.Floor(f),
                string s when long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
                _ => def
            };
        }

        private static double GetDouble(IDictionary<string, object> m, string k, double def = 0)
        {
            if (!m.TryGetValue(k, out var v) || v == null) return def;
            return v switch
            {
                double d => d,
                float f => f,
                long l => l,
                int i => i,
                string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
                _ => def
            };
        }

        private static long? GetNullableLong(IDictionary<string, object> m, string k)
        {
            if (!m.TryGetValue(k, out var v) || v == null) return null;
            return v switch
            {
                long l => l,
                int i => i,
                double d => (long)Math.Floor(d),
                float f => (long)Math.Floor(f),
                string s when long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
                _ => null
            };
        }

        #endregion
    }
}
