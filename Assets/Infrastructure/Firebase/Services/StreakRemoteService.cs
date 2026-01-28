using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Streak remote service implementation.
    /// Firebase Functions ile günlük giriş serisi yönetimi.
    /// </summary>
    public class StreakRemoteService : IStreakRemoteService
    {
        private readonly IGRollLogger _logger;
        private FirebaseFunctions _functions;

        private const string FnGetStreakStatus = "getStreakStatus";
        private const string FnClaimStreak = "claimStreak";

        [Inject]
        public StreakRemoteService(IGRollLogger logger)
        {
            _logger = logger;
        }

        private FirebaseFunctions Functions => _functions ??= FirebaseFunctions.DefaultInstance;

        public async UniTask<StreakSnapshot> FetchAsync()
        {
            try
            {
                _logger.Log("[StreakRemoteService] Fetching streak status");

                var callable = Functions.GetHttpsCallable(FnGetStreakStatus);
                var result = await callable.CallAsync(null);

                var dict = CoerceToStringObjectDict(result?.Data);
                if (dict == null)
                {
                    return new StreakSnapshot { Ok = false, Error = "Empty response" };
                }

                return MapStatusToSnapshot(dict);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StreakRemoteService] Fetch error: {ex.Message}");
                return new StreakSnapshot { Ok = false, Error = ex.Message };
            }
        }

        public async UniTask<StreakClaimResult> ClaimAsync()
        {
            try
            {
                _logger.Log("[StreakRemoteService] Claiming streak rewards");

                var callable = Functions.GetHttpsCallable(FnClaimStreak);
                var claimResp = await callable.CallAsync(null);

                var dict = CoerceToStringObjectDict(claimResp?.Data);
                if (dict == null)
                {
                    return new StreakClaimResult { Ok = false, Error = "Empty response" };
                }

                var result = new StreakClaimResult
                {
                    Ok = GetBool(dict, "ok", true),
                    Granted = GetDouble(dict, "granted", 0),
                    RewardPerDay = GetDouble(dict, "rewardPerDay", 0),
                    UnclaimedDaysAfter = GetInt(dict, "unclaimedDays", 0),
                    NewCurrency = GetDouble(dict, "newCurrency", 0)
                };

                // Fetch fresh status after claim
                var statusCallable = Functions.GetHttpsCallable(FnGetStreakStatus);
                var statusResp = await statusCallable.CallAsync(null);
                var statusDict = CoerceToStringObjectDict(statusResp?.Data);
                if (statusDict != null)
                {
                    result.FreshStatus = MapStatusToSnapshot(statusDict);
                }

                _logger.Log($"[StreakRemoteService] Claimed: {result.Granted}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StreakRemoteService] Claim error: {ex.Message}");
                return new StreakClaimResult { Ok = false, Error = ex.Message };
            }
        }

        private StreakSnapshot MapStatusToSnapshot(IDictionary<string, object> status)
        {
            var ok = GetBool(status, "ok", true);
            var serverNow = GetLong(status, "serverNowMillis", NowMillis());
            var nextMid = GetLong(status, "nextUtcMidnightMillis", 0);

            var deviceNowAtFetch = NowMillis();
            var offset = serverNow - deviceNowAtFetch;

            return new StreakSnapshot
            {
                Ok = ok,
                Error = ok ? null : "not-ok",
                ServerNowMillis = serverNow,
                NextUtcMidnightMillis = nextMid,
                TotalDays = GetInt(status, "totalDays", 0),
                UnclaimedDays = GetInt(status, "unclaimedDays", 0),
                RewardPerDay = GetDouble(status, "rewardPerDay", 0),
                PendingTotalReward = GetDouble(status, "pendingTotalReward", 0),
                ClaimAvailable = GetBool(status, "claimAvailable", false),
                TodayCounted = GetBool(status, "todayCounted", false),
                ServerOffsetMs = offset
            };
        }

        #region Helpers

        private static long NowMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private static IDictionary<string, object> CoerceToStringObjectDict(object data)
        {
            if (data == null) return null;

            if (data is IDictionary<string, object> sdict)
                return sdict;

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

            return null;
        }

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
            return v switch
            {
                double dbl => dbl,
                float f => f,
                long l => l,
                int i => i,
                string s when double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var ps) => ps,
                _ => def
            };
        }

        #endregion
    }
}
