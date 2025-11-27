#nullable enable
using System.Globalization;
using Firebase.Functions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Firebase.Extensions;
public static class AutopilotService
{
    private const string Region = "us-central1";

    private const string FN_STATUS = "getAutopilotStatus";
    private const string FN_TOGGLE = "toggleAutopilot";
    private const string FN_CLAIM  = "claimAutopilot";

    private static FirebaseFunctions? _functions;
    private static FirebaseFunctions Fns => _functions ??= FirebaseFunctions.GetInstance(Region);

    public static void UseFunctionsInstance(FirebaseFunctions custom)
    {
        _functions = custom;
    }

    // ----------------------------------------------------
    // DTOs
    // ----------------------------------------------------

    [Serializable]
    public sealed class Status
    {
        public bool ok;
        public long serverNowMillis;

        public bool isElite;
        public bool isAutopilotOn;
        public double autopilotWallet;
        public long currency;

        public double normalUserEarningPerHour;
        public double eliteUserEarningPerHour;
        public double normalUserMaxAutopilotDurationInHours;

        public long? autopilotActivationDateMillis;
        public long autopilotLastClaimedAtMillis;

        public long? timeToCapSeconds;
        public bool isClaimReady;

        public DateTime ServerNowUtc => FromMillisUtc(serverNowMillis);

        public double CapGainNormal =>
            normalUserEarningPerHour * normalUserMaxAutopilotDurationInHours;

        public static Status From(IDictionary<string, object> m)
        {
            return new Status
            {
                ok = GetBool(m, "ok"),
                serverNowMillis = GetLong(m, "serverNowMillis"),

                isElite = GetBool(m, "isElite"),
                isAutopilotOn = GetBool(m, "isAutopilotOn"),
                autopilotWallet = GetDouble(m, "autopilotWallet"),
                currency = GetLong(m, "currency"),

                normalUserEarningPerHour = GetDouble(m, "normalUserEarningPerHour"),
                eliteUserEarningPerHour  = GetDouble(m, "eliteUserEarningPerHour"),
                normalUserMaxAutopilotDurationInHours = GetDouble(m, "normalUserMaxAutopilotDurationInHours"),

                autopilotActivationDateMillis = GetNullableLong(m, "autopilotActivationDateMillis"),
                autopilotLastClaimedAtMillis = GetLong(m, "autopilotLastClaimedAtMillis"),

                timeToCapSeconds = GetNullableLong(m, "timeToCapSeconds"),
                isClaimReady = GetBool(m, "isClaimReady"),
            };
        }
    }

    [Serializable]
    public sealed class ToggleResult
    {
        public bool ok;
        public bool isAutopilotOn;

        public static ToggleResult From(IDictionary<string, object> m) => new ToggleResult
        {
            ok = GetBool(m, "ok"),
            isAutopilotOn = GetBool(m, "isAutopilotOn"),
        };
    }

    [Serializable]
    public sealed class ClaimResult
    {
        public bool ok;
        public long claimed;
        public long currencyAfter;

        public static ClaimResult From(IDictionary<string, object> m) => new ClaimResult
        {
            ok = GetBool(m, "ok"),
            claimed = GetLong(m, "claimed"),
            currencyAfter = GetLong(m, "currencyAfter"),
        };
    }

    // ----------------------------------------------------
    // PUBLIC API (UI sadece bunları kullanacak)
    // ----------------------------------------------------

    public static async Task<Status> GetStatusAsync(CancellationToken ct = default)
    {
        var callable = Fns.GetHttpsCallable(FN_STATUS);
        try
        {
            var res = await callable.CallAsync(null).WithCancellation(ct).ConfigureAwait(false);

            // Coerce payload into a string-keyed dictionary
            var dict = ToStringObjectDict(res?.Data);
            if (dict == null)
                throw new InvalidOperationException($"getAutopilotStatus payload type {res?.Data?.GetType().FullName ?? "<null>"} is not an object");

            // Some backends wrap with a 'status' field; unwrap if present
            if (dict.TryGetValue("status", out var statusObj))
            {
                var inner = ToStringObjectDict(statusObj);
                if (inner != null)
                    dict = inner;
            }

            return Status.From(dict);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FunctionsException fex)
        {
            throw new Exception($"GetStatusAsync firebase error: {fex.Message}", fex);
        }
        catch (Exception ex)
        {
            throw new Exception($"GetStatusAsync failed: {ex.Message}", ex);
        }
    }

    public static async Task<ToggleResult> ToggleAsync(bool on, CancellationToken ct = default)
    {
        var callable = Fns.GetHttpsCallable(FN_TOGGLE);
        var payload = new Dictionary<string, object> { ["on"] = on };

        try
        {
            var res = await callable.CallAsync(payload).WithCancellation(ct).ConfigureAwait(false);

            // Coerce payload into a string-keyed dictionary
            var dict = ToStringObjectDict(res?.Data);
            if (dict == null)
                throw new InvalidOperationException($"toggleAutopilot payload type {res?.Data?.GetType().FullName ?? "<null>"} is not an object");

            // Optional unwrap
            if (dict.TryGetValue("result", out var resultObj))
            {
                var inner = ToStringObjectDict(resultObj);
                if (inner != null)
                    dict = inner;
            }

            return ToggleResult.From(dict);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FunctionsException fex)
        {
            throw new AutopilotRemoteException(fex.ErrorCode.ToString(), fex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception($"ToggleAsync failed: {ex.Message}", ex);
        }
    }

    public static async Task<ClaimResult> ClaimAsync(CancellationToken ct = default)
    {
        var callable = Fns.GetHttpsCallable(FN_CLAIM);

        try
        {
            var res = await callable.CallAsync(null).WithCancellation(ct).ConfigureAwait(false);

            // Coerce payload into a string-keyed dictionary
            var dict = ToStringObjectDict(res?.Data);
            if (dict == null)
                throw new InvalidOperationException($"claimAutopilot payload type {res?.Data?.GetType().FullName ?? "<null>"} is not an object");

            // Optional unwrap
            if (dict.TryGetValue("result", out var resultObj))
            {
                var inner = ToStringObjectDict(resultObj);
                if (inner != null)
                    dict = inner;
            }

            return ClaimResult.From(dict);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FunctionsException fex)
        {
            // Map specific precondition to domain exception
            var code = fex.ErrorCode.ToString();
            var msg  = fex.Message ?? "Remote error";
            if (fex.ErrorCode == FunctionsErrorCode.FailedPrecondition &&
                msg.IndexOf("Not ready to claim", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new NotReadyToClaimException(msg);
            }
            throw new AutopilotRemoteException(code, msg);
        }
        catch (Exception ex)
        {
            throw new Exception($"ClaimAsync failed: {ex.Message}", ex);
        }
    }

    // ----------------------------------------------------
    // Custom Exceptions
    // ----------------------------------------------------

    public sealed class NotReadyToClaimException : Exception
    {
        public NotReadyToClaimException(string message) : base(message) {}
    }

    public sealed class AutopilotRemoteException : Exception
    {
        public string Code { get; }
        public AutopilotRemoteException(string code, string msg) : base(msg) => Code = code;
        public override string ToString() => $"[{Code}] {Message}";
    }

    // ----------------------------------------------------
    // Helpers
    // ----------------------------------------------------

    private static IDictionary<string, object>? ToStringObjectDict(object? data)
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
                    conv[key!] = kv.Value!;
            }
            return conv;
        }

        if (data is IEnumerable<KeyValuePair<string, object>> pairs)
        {
            var conv = new Dictionary<string, object>();
            foreach (var kv in pairs)
                conv[kv.Key] = kv.Value!;
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
            float f => (double)f,
            long l => (double)l,
            int i => (double)i,
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

    private static DateTime FromMillisUtc(long ms)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
    }

    private static (string code, string message) ParseFunctionsException(AggregateException? ex)
    {
        var msg = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown error";
        string code = "internal";

        foreach (var c in new[]
        {
            "invalid-argument","unauthenticated","permission-denied",
            "failed-precondition","not-found","already-exists",
            "aborted","out-of-range","unimplemented","internal",
            "unavailable","data-loss","deadline-exceeded"
        })
        {
            if (msg.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                code = c;
                break;
            }
        }

        return (code, msg);
    }
    // ----------------------------------------------------
    // Task Extensions for Cancellation Support
    // ----------------------------------------------------

}

internal static class TaskExtensions
{
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (ct.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                throw new TaskCanceledException();
        }
        return await task.ConfigureAwait(false);
    }
}