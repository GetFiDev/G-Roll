using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ItemLocalDatabase
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "items.json");

    // Bellek-içi cache
    private static Dictionary<string, RemoteItemService.ItemData> _cache;

    [Serializable]
    private class Wrapper
    {
        public List<string> keys = new();
        public List<RemoteItemService.ItemData> values = new();
    }

    public static Dictionary<string, RemoteItemService.ItemData> Load()
    {
        if (_cache != null) return _cache;

        if (!File.Exists(FilePath))
        {
            _cache = new Dictionary<string, RemoteItemService.ItemData>();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var wrap = JsonUtility.FromJson<Wrapper>(json);
            var dict = new Dictionary<string, RemoteItemService.ItemData>();

            if (wrap != null && wrap.keys != null && wrap.values != null)
            {
                for (int i = 0; i < Mathf.Min(wrap.keys.Count, wrap.values.Count); i++)
                    dict[wrap.keys[i]] = wrap.values[i];
            }

            _cache = dict;
        }
        catch
        {
            _cache = new Dictionary<string, RemoteItemService.ItemData>();
        }

        return _cache;
    }

    public static void Save(Dictionary<string, RemoteItemService.ItemData> data)
    {
        _cache = data ?? new Dictionary<string, RemoteItemService.ItemData>();
        var wrap = new Wrapper();
        foreach (var kv in _cache)
        {
            wrap.keys.Add(kv.Key);
            wrap.values.Add(kv.Value);
        }

        var json = JsonUtility.ToJson(wrap, true);
        File.WriteAllText(FilePath, json);
    }
}