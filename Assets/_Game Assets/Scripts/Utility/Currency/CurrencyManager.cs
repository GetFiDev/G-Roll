using System;
using System.Collections.Generic;
using UnityEngine;

public static class CurrencyManager
{
    private static readonly Lazy<Dictionary<CurrencyType, CurrencyData>> LazyList = new(LoadFromResources);

    private static Dictionary<CurrencyType, CurrencyData> CurrencyDataList => LazyList.Value;


    private static Dictionary<CurrencyType, CurrencyData> LoadFromResources()
    {
        var allResources = Resources.LoadAll<CurrencyData>(ResourcePaths.CurrencyData);

        var dict = new Dictionary<CurrencyType, CurrencyData>();

        foreach (var resource in allResources)
        {
            if (!dict.TryAdd(resource.CurrencyType, resource))
            {
                throw new Exception($"Duplicate CurrencyType found: {resource.CurrencyType} in CurrencyData assets.");
            }
        }

        return dict;
    }

    public static CurrencyData GetData(CurrencyType type)
    {
        if (CurrencyDataList.TryGetValue(type, out var data))
            return data;

        throw new Exception($"No CurrencyData found for {type} on {ResourcePaths.CurrencyData}");
    }

    public static int Get(CurrencyType type)
    {
        if (CurrencyDataList.TryGetValue(type, out var data))
            return data.Value;

        throw new Exception($"No CurrencyData found for {type}on {ResourcePaths.CurrencyData}");
    }

    public static void Set(CurrencyType type, int amount)
    {
        if (CurrencyDataList.TryGetValue(type, out var data))
            data.Value = amount;
        else
            Debug.LogError($"Currency of type {type} not found.");
    }

    public static void Add(CurrencyType type, int amount)
    {
        if (CurrencyDataList.TryGetValue(type, out var data))
            data.Value += amount;
        else
            Debug.LogError($"Currency of type {type} not found.");
    }

    public static void Subtract(CurrencyType type, int amount)
    {
        if (CurrencyDataList.TryGetValue(type, out var data))
            data.Value = Mathf.Max(0, data.Value - amount);
        else
            Debug.LogError($"Currency of type {type} not found.");
    }
}