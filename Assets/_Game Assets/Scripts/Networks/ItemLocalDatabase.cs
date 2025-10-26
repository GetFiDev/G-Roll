using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class ItemLocalDatabase
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "items.json");
    private static string _iconsPath => Path.Combine(Application.persistentDataPath, "item_icons");

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
                {
                    var key = wrap.keys[i];
                    var itemData = wrap.values[i];

                    // Load icon sprite if exists
                    var iconPath = Path.Combine(_iconsPath, key + ".png");
                    if (File.Exists(iconPath))
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
            _cache = new Dictionary<string, RemoteItemService.ItemData>();
        }

        return _cache;
    }

    public static void Save(Dictionary<string, RemoteItemService.ItemData> data)
    {
        _cache = data ?? new Dictionary<string, RemoteItemService.ItemData>();

        if (!Directory.Exists(_iconsPath))
        {
            Directory.CreateDirectory(_iconsPath);
        }

        var wrap = new Wrapper();
        foreach (var kv in _cache)
        {
            wrap.keys.Add(kv.Key);
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
                        var iconFilePath = Path.Combine(_iconsPath, kv.Key + ".png");
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
}