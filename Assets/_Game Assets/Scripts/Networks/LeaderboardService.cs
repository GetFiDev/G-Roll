using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

public static class LeaderboardService
{
    // ----- Public DTO’lar -----
    [Serializable]
    public struct Item
    {
        public string Uid;
        public string Username;
        public int Score;
        public long ElitePassExpiresAtMillis; // yoksa 0

        public bool HasElite(long nowMillis)
            => ElitePassExpiresAtMillis > 0 && ElitePassExpiresAtMillis > nowMillis;
    }

    [Serializable]
    public struct Result
    {
        public string Season;
        public bool HasMore;
        public double? NextStartAfterScore;   // sayfalama için
        public List<Item> Items;
        public Item? Me;                      // katılımcının kendi satırı (opsiyonel)
        public long ServerNowMillis;          // yoksa client UtcNow kullanılır
        public int RankOffset;                // ekranda rank = RankOffset + index + 1
    }

    // ----- Public API -----
    public static async Task<Result> FetchAsync(
        int limit = 100,
        double? startAfterScore = null,
        bool includeSelf = true,
        CancellationToken ct = default)
    {
        var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable("getLeaderboardsSnapshot");

        var payload = new Dictionary<string, object>
        {
            ["limit"] = limit,
            ["includeSelf"] = includeSelf
        };
        if (startAfterScore.HasValue)
            payload["startAfterScore"] = startAfterScore.Value;

        var raw = await CallWithCancellation(callable, payload, ct);
        var obj = raw?.Data;
        var dict = AsMap(obj);
        if (dict == null)
        {
            var typeName = obj?.GetType().FullName ?? "<null>";
            Debug.LogWarning($"[LeaderboardService] Unexpected result type (map): {typeName}. Raw ToString: {obj}");
            throw new InvalidOperationException("getLeaderboardsSnapshot returned no data");
        }

        // serverNowMillis opsiyonel – yoksa client zamanı
        long serverNowMs = ToLong(dict, "serverNowMillis",
            fallback: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var season = ToString(dict, "season", "current");
        var hasMore = ToBool(dict, "hasMore", false);
        double? nextStartAfterScore = null;
        if (dict.TryGetValue("next", out var nextObj))
        {
            var nextMap = AsMap(nextObj);
            if (nextMap != null && nextMap.TryGetValue("startAfterScore", out var s)
                && double.TryParse(Convert.ToString(s), out var dbl))
            {
                nextStartAfterScore = dbl;
            }
        }

        // items[]
        var items = new List<Item>();
        if (dict.TryGetValue("items", out var itemsObj) && itemsObj is IList arr)
        {
            foreach (var row in arr)
            {
                var m = AsMap(row);
                if (m != null) items.Add(ParseItem(m));
            }
        }

        // me (opsiyonel)
        Item? me = null;
        if (dict.TryGetValue("me", out var meObj))
        {
            var meMap = AsMap(meObj);
            if (meMap != null) me = ParseItem(meMap);
        }

        // RankOffset: ilk sayfa için 0; sonraki sayfalarda dışarıdan beslenebilir
        var res = new Result
        {
            Season = season,
            HasMore = hasMore,
            NextStartAfterScore = nextStartAfterScore,
            Items = items,
            Me = me,
            ServerNowMillis = serverNowMs,
            RankOffset = 0
        };

        return res;
    }

    private static async Task<HttpsCallableResult> CallWithCancellation(HttpsCallableReference callable, object payload, CancellationToken ct)
    {
        // Some Firebase Unity SDK versions don't expose CallAsync(payload, CancellationToken)
        // so we emulate cancellation by racing the call with a Task.Delay bound to the token.
        var callTask = callable.CallAsync(payload);
        if (!ct.CanBeCanceled)
            return await callTask;

        var cancelTask = Task.Delay(System.Threading.Timeout.Infinite, ct);
        var finished = await Task.WhenAny(callTask, cancelTask);
        if (finished == callTask)
            return await callTask; // propagate result/exception

        // Cancellation won
        throw new OperationCanceledException(ct);
    }

    private static IDictionary<string, object> AsMap(object o)
    {
        if (o == null) return null;
        if (o is IDictionary<string, object> dictSO) return dictSO;
        if (o is IDictionary raw)
        {
            var res = new Dictionary<string, object>();
            foreach (DictionaryEntry de in raw)
            {
                var key = Convert.ToString(de.Key);
                if (string.IsNullOrEmpty(key)) continue;
                res[key] = de.Value;
            }
            return res;
        }
        return null;
    }

    // ----- Parsers & helpers -----
    private static Item ParseItem(IDictionary<string, object> m)
    {
        var uid = ToString(m, "uid", "");
        var username = ToString(m, "username", "Guest");
        var score = ToInt(m, "score", 0);

        // Sunucu eklediyse kullan; yoksa 0
        var eliteMs = ToLong(m, "elitePassExpiresAtMillis", 0);

        return new Item
        {
            Uid = uid,
            Username = username,
            Score = score,
            ElitePassExpiresAtMillis = eliteMs
        };
    }

    private static string ToString(IDictionary<string, object> map, string key, string fallback = "")
    {
        return map.TryGetValue(key, out var v) ? Convert.ToString(v) ?? fallback : fallback;
    }

    private static int ToInt(IDictionary<string, object> map, string key, int fallback = 0)
    {
        if (!map.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is double d) return (int)Math.Round(d);
        if (int.TryParse(Convert.ToString(v), out var p)) return p;
        return fallback;
    }

    private static long ToLong(IDictionary<string, object> map, string key, long fallback = 0)
    {
        if (!map.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is long l) return l;
        if (v is int i) return i;
        if (v is double d) return (long)Math.Round(d);
        if (long.TryParse(Convert.ToString(v), out var p)) return p;
        return fallback;
    }

    private static bool ToBool(IDictionary<string, object> map, string key, bool fallback = false)
    {
        if (!map.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is bool b) return b;
        if (bool.TryParse(Convert.ToString(v), out var p)) return p;
        if (v is int i) return i != 0;
        if (v is long l) return l != 0;
        return fallback;
    }
}