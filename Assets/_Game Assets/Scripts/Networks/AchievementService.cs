using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

[Serializable]
public class AchDef  // server "defs" elemanları
{
    public string typeId;
    public string displayName;
    public string description;
    public string iconUrl;
    public int    order;
    public int    maxLevel;
    public List<double> thresholds; // uzunluk == maxLevel
    public List<int>    rewards;    // uzunluk == maxLevel
}

[Serializable]
public class AchState // server "states" elemanları
{
    public string typeId;
    public double progress;
    public int    level;            // 0..maxLevel
    public List<int> claimedLevels; // örn [1,2]
    public double? nextThreshold;   // null olabilir
}

[Serializable]
public class AchSnapshot // birleşik payload
{
    public List<AchDef>   defs = new();
    public List<AchState> states = new();

    public AchDef DefOf(string typeId)   => defs.Find(d => d.typeId == typeId);
    public AchState StateOf(string typeId)=> states.Find(s => s.typeId == typeId);
}

public static class AchievementService
{
    public enum ClaimResultCode
    {
        Ok,
        AlreadyClaimed,     // server: already-exists
        LevelNotReached,    // server: failed-precondition
        InvalidLevel,       // server: invalid-argument
        NetworkError,
    }

    public struct ClaimResult
    {
        public ClaimResultCode code;
        public string message;
    }

    static HttpsCallableReference Fn(string name)
        => FirebaseFunctions.GetInstance("us-central1").GetHttpsCallable(name);

    // IDictionary parse helper’ları (server’dan gelen boxed türler için güvenli dönüştürme)
    static double ToDouble(object v, double def = 0) {
        if (v is double d) return d;
        if (v is float f) return f;
        if (v is long l) return l;
        if (v is int  i) return i;
        if (v is string s && double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var p)) return p;
        return def;
    }
    static int ToInt(object v, int def = 0) {
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is double d) return (int)d;
        if (v is float f) return (int)f;
        if (v is string s && int.TryParse(s, out var p)) return p;
        return def;
    }

    public static async Task<AchSnapshot> GetSnapshotAsync()
    {
        var snap = new AchSnapshot();
        try
        {
            var resp = await Fn("getAchievementsSnapshot").CallAsync(new Dictionary<string, object>());
            if (resp?.Data is IDictionary root)
            {
                // --- defs ---
                if (root.Contains("defs") && root["defs"] is IList defsList)
                {
                    foreach (var it in defsList)
                    {
                        if (it is IDictionary d)
                        {
                            var def = new AchDef
                            {
                                typeId      = d["typeId"] as string ?? "",
                                displayName = d["displayName"] as string ?? (d["typeId"] as string ?? ""),
                                description = d["description"] as string ?? "",
                                iconUrl     = d["iconUrl"] as string ?? "",
                                order       = ToInt(d["order"] ?? 0),
                                maxLevel    = ToInt(d["maxLevel"] ?? 0),
                                thresholds  = new List<double>(),
                                rewards     = new List<int>(),
                            };
                            if (d["thresholds"] is IList th)
                                foreach (var x in th) def.thresholds.Add(ToDouble(x,0));
                            if (d["rewards"] is IList rw)
                                foreach (var x in rw) def.rewards.Add(ToInt(x,0));
                            snap.defs.Add(def);
                        }
                    }
                }

                // --- states ---
                if (root.Contains("states") && root["states"] is IList statesList)
                {
                    foreach (var it in statesList)
                    {
                        if (it is IDictionary d)
                        {
                            var st = new AchState
                            {
                                typeId        = d["typeId"] as string ?? "",
                                progress      = ToDouble(d["progress"] ?? 0),
                                level         = ToInt(d["level"] ?? 0),
                                claimedLevels = new List<int>(),
                                nextThreshold = d.Contains("nextThreshold")
                                    ? ToDouble(d["nextThreshold"] ?? 0) : (double?)null,
                            };
                            if (d["claimedLevels"] is IList arr)
                                foreach (var x in arr) st.claimedLevels.Add(ToInt(x,0));

                            snap.states.Add(st);
                        }
                    }
                }
            }
            Debug.Log($"[AchievementService] defs={snap.defs.Count} states={snap.states.Count}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AchievementService] GetSnapshotAsync error: {e.Message}");
        }
        return snap;
    }

    // Rich-result version (preferred by UI)
    public static async Task<ClaimResult> ClaimAsyncEx(string typeId, int level)
    {
        try
        {
            await Fn("claimAchievementReward").CallAsync(new Dictionary<string, object> {
                {"typeId", typeId}, {"level", level}
            });
            return new ClaimResult { code = ClaimResultCode.Ok, message = "ok" };
        }
        catch (Exception e)
        {
            var msg = e.Message ?? string.Empty;
            var lower = msg.ToLowerInvariant();

            if (lower.Contains("already-exists"))
                return new ClaimResult { code = ClaimResultCode.AlreadyClaimed, message = msg };
            if (lower.Contains("already claimed"))
                return new ClaimResult { code = ClaimResultCode.AlreadyClaimed, message = msg };

            if (lower.Contains("failed-precondition"))
                return new ClaimResult { code = ClaimResultCode.LevelNotReached, message = msg };

            if (lower.Contains("invalid-argument") || lower.Contains("invalid level"))
                return new ClaimResult { code = ClaimResultCode.InvalidLevel, message = msg };

            Debug.LogWarning($"[AchievementService] ClaimAsyncEx network error: {msg}");
            return new ClaimResult { code = ClaimResultCode.NetworkError, message = msg };
        }
    }

    // Backward-compatible wrapper: treats Ok/AlreadyClaimed as success
    public static async Task<bool> ClaimAsync(string typeId, int level)
    {
        var res = await ClaimAsyncEx(typeId, level);
        return res.code == ClaimResultCode.Ok || res.code == ClaimResultCode.AlreadyClaimed;
    }

    public static async Task<int> ClaimAllEligibleAsync(AchDef def, AchState st)
    {
        if (def == null || st == null) return 0;
        int claimed = 0;
        var have = new HashSet<int>(st.claimedLevels ?? new List<int>());
        for (int lv = 1; lv <= st.level; lv++)
        {
            if (have.Contains(lv)) continue;
            var res = await ClaimAsyncEx(st.typeId, lv);
            if (res.code == ClaimResultCode.Ok || res.code == ClaimResultCode.AlreadyClaimed)
                claimed++;
            // LevelNotReached/InvalidLevel/NetworkError -> ignore; UI will refresh snapshot anyway
        }
        UITopPanel.Instance.Initialize();
        return claimed;
    }

    /// <summary>
    /// Optimistic UI version: Immediately adds reward locally, fires server request in background.
    /// Returns the total reward amount that was optimistically granted.
    /// </summary>
    public static int ClaimAllEligibleOptimistic(AchDef def, AchState st)
    {
        if (def == null || st == null) return 0;

        int totalReward = 0;
        var have = new HashSet<int>(st.claimedLevels ?? new List<int>());
        var levelsToClaim = new List<int>();

        // Calculate total reward and collect levels to claim
        for (int lv = 1; lv <= st.level; lv++)
        {
            if (have.Contains(lv)) continue;

            int reward = (lv - 1) < def.rewards.Count ? def.rewards[lv - 1] : 0;
            totalReward += reward;
            levelsToClaim.Add(lv);

            // Mark as claimed locally to prevent double-claiming
            st.claimedLevels ??= new List<int>();
            st.claimedLevels.Add(lv);
        }

        if (totalReward > 0)
        {
            // Immediately add currency locally - update ALL caches for consistency
            CurrencyManager.Add(CurrencyType.SoftCurrency, totalReward);

            // Also update UserDatabaseManager cache and PlayerPrefs
            if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
            {
                var ud = UserDatabaseManager.Instance.currentUserData;
                ud.currency += totalReward;

                // Sync PlayerPrefs cache so UserStatsDisplayer shows correct value
                PlayerPrefs.SetString("UserStatsDisplayer.LastCurrency", ud.currency.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }

            Debug.Log($"[AchievementService] Optimistic: Added {totalReward} coins locally (all caches synced)");
        }

        // Fire server requests in background (fire-and-forget)
        if (levelsToClaim.Count > 0)
        {
            _ = ClaimLevelsInBackgroundAsync(st.typeId, levelsToClaim);
        }

        return totalReward;
    }

    private static async Task ClaimLevelsInBackgroundAsync(string typeId, List<int> levels)
    {
        foreach (var lv in levels)
        {
            try
            {
                var res = await ClaimAsyncEx(typeId, lv);
                if (res.code != ClaimResultCode.Ok && res.code != ClaimResultCode.AlreadyClaimed)
                {
                    Debug.LogWarning($"[AchievementService] Background claim failed for {typeId} lv{lv}: {res.message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AchievementService] Background claim exception for {typeId} lv{lv}: {e.Message}");
            }
        }
    }
}