using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class AnalyticsManager
{
    private static int _lastPlayedLevelIndex;

    public static void Initialize()
    {
        //GA.Initialize
    }

    public static void OnLevelStart(Dictionary<string, object> extraParams = null)
    {
        var lvlIndex = DataManager.CurrentLevelIndex;

        var startType = _lastPlayedLevelIndex == lvlIndex ? StartType.Retry : StartType.First;

        _lastPlayedLevelIndex = lvlIndex;

        // AnalyticsService.Instance.CustomData("onLevelStarted", new Dictionary<string, object>()
        // {
        //     { "levelIndex", lvlIndex},
        //     { "levelId", lvlId},
        //     { "startType", startType.ToString()}
        // }); 

        Debug.Log($"AnalyticsHelper OnLevelStarted - Level index:{lvlIndex}, Start type:{startType}");
    }

    public static void OnLevelFinish(EndType endType, Dictionary<string, object> extraParams = null)
    {
        var lvlIndex = DataManager.CurrentLevelIndex;

        // AnalyticsService.Instance.CustomData("onLevelFinish", new Dictionary<string, object>()
        // {
        //     { "levelIndex", lvlIndex},
        //     { "levelId", lvlId},
        // }); 

        Debug.Log(
            $"AnalyticsHelper OnLevelEnded - Level index:{lvlIndex}, End type:{{endType}}, extra params:{{extraParams}}");
    }

    public static void OnProgress(string eventName, Dictionary<string, object> parameters = null)
    {
        var parametersString = "Null";

        if (parameters is not null)
            parametersString = parameters.Aggregate("",
                (current, param) => current + $"{System.Environment.NewLine} Key : {param.Key}, Value : {param.Value}");

        // AnalyticsService.Instance.CustomData("onProgress", parameters); 

        Debug.Log($"AnalyticsHelper OnProgress - Event Name :{eventName} , Parameters :{parametersString}");
    }
}

public enum StartType
{
    First,
    Retry,
    Continue,
}

public enum EndType
{
    Success,
    Fail,
    Quit,
    Skip,
}