using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

/// <summary>
/// Service for handling rewarded tasks through Firebase Cloud Functions.
/// </summary>
public static class TaskService
{
    // ==== Public DTOs ====

    [Serializable]
    public struct TaskData
    {
        public string taskId;
        public string taskDisplayName;
        public string taskDisplayDescription;
        public double taskCurrencyReward;
        public string taskDirectionUrl;
        public string taskIconUrl;
    }

    [Serializable]
    public struct TaskListResponse
    {
        public bool ok;
        public List<TaskData> tasks;
        public string error;
    }

    [Serializable]
    public struct CompleteTaskResult
    {
        public bool ok;
        public string taskId;
        public bool alreadyCompleted;
        public double rewardGranted;
        public double newCurrency;
        public string error;
    }

    // ==== Function Names ====
    private const string FnGetAvailableTasks = "getAvailableTasks";
    private const string FnCompleteTask = "completeTask";

    // ========= Public API =========

    /// <summary>
    /// Fetches all available (uncompleted) tasks for the current user.
    /// </summary>
    public static async Task<TaskListResponse> FetchAvailableTasksAsync(CancellationToken ct = default)
    {
        var response = new TaskListResponse { tasks = new List<TaskData>() };

        try
        {
            var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable(FnGetAvailableTasks);
            var result = await callable.CallAsync(null);
            ct.ThrowIfCancellationRequested();

            var dict = CoerceToStringObjectDict(result?.Data);
            if (dict == null)
            {
                response.error = "Empty response from server";
                return response;
            }

            response.ok = GetBool(dict, "ok", false);

            if (dict.TryGetValue("tasks", out var tasksObj) && tasksObj is System.Collections.IList taskList)
            {
                foreach (var item in taskList)
                {
                    var taskDict = CoerceToStringObjectDict(item);
                    if (taskDict == null) continue;

                    response.tasks.Add(new TaskData
                    {
                        taskId = GetString(taskDict, "taskId", ""),
                        taskDisplayName = GetString(taskDict, "taskDisplayName", ""),
                        taskDisplayDescription = GetString(taskDict, "taskDisplayDescription", ""),
                        taskCurrencyReward = GetDouble(taskDict, "taskCurrencyReward", 0.5),
                        taskDirectionUrl = GetString(taskDict, "taskDirectionUrl", ""),
                        taskIconUrl = GetString(taskDict, "taskIconUrl", ""),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TaskService] FetchAvailableTasksAsync failed: {ex.Message}");
            response.error = ex.Message;
        }

        return response;
    }

    /// <summary>
    /// Marks a task as completed and claims the reward.
    /// </summary>
    public static async Task<CompleteTaskResult> CompleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        var result = new CompleteTaskResult { taskId = taskId };

        try
        {
            var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable(FnCompleteTask);
            var payload = new Dictionary<string, object> { { "taskId", taskId } };
            var resp = await callable.CallAsync(payload);
            ct.ThrowIfCancellationRequested();

            var dict = CoerceToStringObjectDict(resp?.Data);
            if (dict == null)
            {
                result.error = "Empty response from server";
                return result;
            }

            result.ok = GetBool(dict, "ok", false);
            result.alreadyCompleted = GetBool(dict, "alreadyCompleted", false);
            result.rewardGranted = GetDouble(dict, "rewardGranted", 0);
            result.newCurrency = GetDouble(dict, "newCurrency", 0);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TaskService] CompleteTaskAsync failed: {ex.Message}");
            result.error = ex.Message;
        }

        return result;
    }

    // ========= Helpers =========

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

    private static string GetString(IDictionary<string, object> d, string k, string def = "")
    {
        if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
        return v.ToString();
    }

    private static double GetDouble(IDictionary<string, object> d, string k, double def = 0)
    {
        if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
        switch (v)
        {
            case double dbl: return dbl;
            case float f: return f;
            case long l: return l;
            case int i: return i;
            case string s when double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var ps): return ps;
            default: return def;
        }
    }
}
