using System;
using System.Collections.Generic;
using UnityEngine;

public static class CurrencyManager
{
    private static Dictionary<CurrencyType, CurrencyData> _currencyDatas;
    private static Dictionary<CurrencyType, CurrencyData> CurrencyDatas => _currencyDatas ??= LoadFromResources();

    private static Dictionary<CurrencyType, CurrencyData> LoadFromResources()
    {
        var allResources = Resources.LoadAll<CurrencyData>(ResourcePaths.CurrencyData);

        var dict = new Dictionary<CurrencyType, CurrencyData>();

        foreach (var resource in allResources)
        {
            if (dict.ContainsKey(resource.CurrencyType))
            {
                throw new Exception($"Duplicate CurrencyType found: {resource.CurrencyType} in CurrencyData assets.");
            }

            dict[resource.CurrencyType] = resource;
        }

        return dict;
    }
    
    public static CurrencyData GetData(CurrencyType type)
    {
        if (CurrencyDatas.TryGetValue(type, out var data))
            return data;
        
        throw new Exception($"No CurrencyData found for {type}");
    }

    public static int Get(CurrencyType type)
    {
        if (CurrencyDatas.TryGetValue(type, out var data))
            return data.Value;

        throw new Exception($"No CurrencyData found for {type}");
    }

    public static void Set(CurrencyType type, int amount)
    {
        if (CurrencyDatas.TryGetValue(type, out var data))
            data.Value = amount;
        else
            Debug.LogError($"Currency of type {type} not found.");
    }

    public static void Add(CurrencyType type, int amount)
    {
        if (CurrencyDatas.TryGetValue(type, out var data))
            data.Value += amount;
        else
            Debug.LogError($"Currency of type {type} not found.");
    }

    public static void Subtract(CurrencyType type, int amount)
    {
        if (CurrencyDatas.TryGetValue(type, out var data))
            data.Value = Mathf.Max(0, data.Value - amount);
        else
            Debug.LogError($"Currency of type {type} not found.");
    }
}