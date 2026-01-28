using System;
using System.Collections.Generic;
using GRoll.Core.Interfaces.Services;
using UnityEngine;

/// <summary>
/// Legacy facade for currency display.
/// This static class provides backward compatibility for UI components that depend on CurrencyData.
/// New code should inject ICurrencyService directly via DI.
///
/// Note: This class is only for read operations (displaying currency).
/// All write operations (add/spend) should use ICurrencyService.
/// </summary>
[Obsolete("Use ICurrencyService via DI for currency operations. This class is only for legacy UI compatibility.")]
public static class CurrencyManager
{
    private static readonly Lazy<Dictionary<CurrencyType, CurrencyData>> LazyList = new(LoadFromResources);
    private static ICurrencyService _currencyService;

    private static Dictionary<CurrencyType, CurrencyData> CurrencyDataList => LazyList.Value;

    /// <summary>
    /// Sets the CurrencyService instance for syncing. Called by DI container setup.
    /// </summary>
    public static void SetService(ICurrencyService service)
    {
        _currencyService = service;

        // Subscribe to currency changes to update CurrencyData values
        if (_currencyService != null)
        {
            _currencyService.OnCurrencyChanged += OnCurrencyChangedFromService;
            SyncWithService();
        }
    }

    private static void OnCurrencyChangedFromService(GRoll.Core.Events.Messages.CurrencyChangedMessage message)
    {
        // Map core CurrencyType to legacy CurrencyType and update CurrencyData
        var legacyType = MapCurrencyType(message.Type);
        if (CurrencyDataList.TryGetValue(legacyType, out var data))
        {
            data.Value = message.NewAmount;
        }
    }

    private static void SyncWithService()
    {
        if (_currencyService == null) return;

        // Sync soft currency
        if (CurrencyDataList.TryGetValue(CurrencyType.SoftCurrency, out var softData))
        {
            softData.Value = _currencyService.GetBalance(GRoll.Core.CurrencyType.SoftCurrency);
        }

        // Sync hard currency
        if (CurrencyDataList.TryGetValue(CurrencyType.HardCurrency, out var hardData))
        {
            hardData.Value = _currencyService.GetBalance(GRoll.Core.CurrencyType.HardCurrency);
        }
    }

    private static CurrencyType MapCurrencyType(GRoll.Core.CurrencyType coreType)
    {
        return coreType switch
        {
            GRoll.Core.CurrencyType.SoftCurrency => CurrencyType.SoftCurrency,
            GRoll.Core.CurrencyType.HardCurrency => CurrencyType.HardCurrency,
            _ => CurrencyType.SoftCurrency
        };
    }

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
        // Prefer service if available for current value
        if (_currencyService != null)
        {
            var coreType = type switch
            {
                CurrencyType.SoftCurrency => GRoll.Core.CurrencyType.SoftCurrency,
                CurrencyType.HardCurrency => GRoll.Core.CurrencyType.HardCurrency,
                _ => GRoll.Core.CurrencyType.SoftCurrency
            };
            return _currencyService.GetBalance(coreType);
        }

        if (CurrencyDataList.TryGetValue(type, out var data))
            return data.Value;

        throw new Exception($"No CurrencyData found for {type} on {ResourcePaths.CurrencyData}");
    }

    [Obsolete("Use ICurrencyService.AddCurrencyOptimisticAsync instead")]
    public static void Set(CurrencyType type, int amount)
    {
        Debug.LogWarning("[CurrencyManager] Set() is deprecated. Use ICurrencyService instead.");
        if (CurrencyDataList.TryGetValue(type, out var data))
            data.Value = amount;
        else
            Debug.LogError($"Currency of type {type} not found.");
    }

    [Obsolete("Use ICurrencyService.AddCurrencyOptimisticAsync instead")]
    public static void Add(CurrencyType type, int amount)
    {
        Debug.LogWarning("[CurrencyManager] Add() is deprecated. Use ICurrencyService.AddCurrencyOptimisticAsync instead.");
        if (CurrencyDataList.TryGetValue(type, out var data))
            data.Value += amount;
        else
            Debug.LogError($"Currency of type {type} not found.");
    }

    [Obsolete("Use ICurrencyService.SpendCurrencyOptimisticAsync instead")]
    public static void Subtract(CurrencyType type, int amount)
    {
        Debug.LogWarning("[CurrencyManager] Subtract() is deprecated. Use ICurrencyService.SpendCurrencyOptimisticAsync instead.");
        if (CurrencyDataList.TryGetValue(type, out var data))
            data.Value = Mathf.Max(0, data.Value - amount);
        else
            Debug.LogError($"Currency of type {type} not found.");
    }
}
