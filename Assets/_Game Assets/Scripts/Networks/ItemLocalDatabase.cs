using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public static class ItemLocalDatabase
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "items.json");
    private static string _iconsPath => Path.Combine(Application.persistentDataPath, "item_icons");

    // Bellek-içi cache
    private static Dictionary<string, RemoteItemService.ItemData> _cache;
    private static Dictionary<string, RemoteItemService.ItemData> NewDict() =>
        new Dictionary<string, RemoteItemService.ItemData>(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    private class Wrapper
    {
        public List<string> keys = new();
        public List<RemoteItemService.ItemData> values = new();
    }

    /// <summary>
    /// Returns the in-memory dictionary (lazy-loaded). Keys are normalized (case-insensitive).
    /// </summary>
    private static Dictionary<string, RemoteItemService.ItemData> Db => Load();

    /// <summary>
    /// Try get item definition by id (any case). Returns false if not found.
    /// </summary>
    public static bool TryGet(string itemId, out RemoteItemService.ItemData data)
    {
        data = null;
        if (string.IsNullOrEmpty(itemId)) return false;
        var nid = IdUtil.NormalizeId(itemId);
        return Db.TryGetValue(nid, out data);
    }

    /// <summary>
    /// Get item definition or null if missing.
    /// </summary>
    public static RemoteItemService.ItemData GetOrNull(string itemId)
    {
        return TryGet(itemId, out var d) ? d : null;
    }

    /// <summary>
    /// Enumerate all cached items (read-only snapshot semantics for callers).
    /// </summary>
    public static IEnumerable<KeyValuePair<string, RemoteItemService.ItemData>> All()
    {
        // Ensure loaded and return a shallow copy to protect internal state
        var dict = Db;
        foreach (var kv in dict)
            yield return kv;
    }

    public static Dictionary<string, RemoteItemService.ItemData> Load()
    {
        if (_cache != null) return _cache;

        if (!File.Exists(FilePath))
        {
            _cache = NewDict();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var wrap = JsonUtility.FromJson<Wrapper>(json);
            var dict = NewDict();

            if (wrap != null && wrap.keys != null && wrap.values != null)
            {
                for (int i = 0; i < Mathf.Min(wrap.keys.Count, wrap.values.Count); i++)
                {
                    var rawKey = wrap.keys[i];
                    var key = IdUtil.NormalizeId(rawKey);
                    var itemData = wrap.values[i];

                    // Load icon sprite if exists (try normalized, then legacy filename)
                    var iconPathNorm = Path.Combine(_iconsPath, key + ".png");
                    var iconPathLegacy = Path.Combine(_iconsPath, rawKey + ".png");
                    var iconPath = File.Exists(iconPathNorm) ? iconPathNorm :
                                   (File.Exists(iconPathLegacy) ? iconPathLegacy : null);
                    if (!string.IsNullOrEmpty(iconPath))
                    {
                        try
                        {
                            byte[] fileData = File.ReadAllBytes(iconPath);
                            Texture2D tex = new Texture2D(2, 2);
                            if (tex.LoadImage(fileData))
                            {
                                itemData.iconSprite = CreateSprite(tex);
                            }
                        }
                        catch
                        {
                            // Ignore icon loading errors
                        }
                    }

                    dict[key] = itemData;
                }
            }

            _cache = dict;
        }
        catch
        {
            _cache = NewDict();
        }

        return _cache;
    }

    public static void Save(Dictionary<string, RemoteItemService.ItemData> data)
    {
        // Rebuild into a fresh case-insensitive dictionary with normalized keys
        var src = data ?? NewDict();
        _cache = NewDict();
        foreach (var kv in src)
        {
            var nid = IdUtil.NormalizeId(kv.Key);
            _cache[nid] = kv.Value;
        }

        if (!Directory.Exists(_iconsPath))
        {
            Directory.CreateDirectory(_iconsPath);
        }

        var wrap = new Wrapper();
        foreach (var kv in _cache)
        {
            var nid = IdUtil.NormalizeId(kv.Key);
            wrap.keys.Add(nid);
            wrap.values.Add(kv.Value);

            var item = kv.Value;
            if (item.iconSprite != null && item.iconSprite.texture != null)
            {
                try
                {
                    var tex = item.iconSprite.texture;
                    byte[] pngData = tex.EncodeToPNG();
                    if (pngData != null)
                    {
                        var iconFilePath = Path.Combine(_iconsPath, nid + ".png");
                        File.WriteAllBytes(iconFilePath, pngData);
                    }
                }
                catch
                {
                    // Ignore icon saving errors
                }
            }
        }

        var json = JsonUtility.ToJson(wrap, true);
        File.WriteAllText(FilePath, json);
    }

    private static Sprite CreateSprite(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Clears only the in-memory cache. Next Load() will re-read from disk.
    /// </summary>
    public static void ClearMemoryCache()
    {
        _cache = null;
    }
}