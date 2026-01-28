using System;
using System.Collections.Generic;
using System.IO;
using GRoll.Infrastructure.Firebase.Interfaces;
using UnityEngine;

namespace GRoll.Infrastructure.Persistence
{
    /// <summary>
    /// Local database for caching item definitions.
    /// Stores items to disk and loads them into memory.
    /// </summary>
    public static class ItemLocalDatabase
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "items.json");
        private static string IconsPath => Path.Combine(Application.persistentDataPath, "item_icons");

        // In-memory cache
        private static Dictionary<string, RemoteItemData> _cache;

        private static Dictionary<string, RemoteItemData> NewDict() =>
            new Dictionary<string, RemoteItemData>(StringComparer.OrdinalIgnoreCase);

        [Serializable]
        private class Wrapper
        {
            public List<string> keys = new();
            public List<SerializableItemData> values = new();
        }

        [Serializable]
        private class SerializableItemData
        {
            public string itemName;
            public string itemDescription;
            public string itemIconUrl;
            public double itemPremiumPrice;
            public double itemGetPrice;
            public bool itemIsConsumable;
            public bool itemIsRewardedAd;
            public int itemReferralThreshold;
            public double coinMultiplierPercent;
            public double comboPower;
            public double gameplaySpeedMultiplierPercent;
            public double magnetPowerPercent;
            public double playerAcceleration;
            public double playerSizePercent;
            public double playerSpeed;
        }

        /// <summary>
        /// Returns the in-memory dictionary (lazy-loaded). Keys are normalized (case-insensitive).
        /// </summary>
        private static Dictionary<string, RemoteItemData> Db => Load();

        /// <summary>
        /// Try get item definition by id (any case). Returns false if not found.
        /// </summary>
        public static bool TryGet(string itemId, out RemoteItemData data)
        {
            data = null;
            if (string.IsNullOrEmpty(itemId)) return false;
            var nid = NormalizeId(itemId);
            return Db.TryGetValue(nid, out data);
        }

        /// <summary>
        /// Get item definition or null if missing.
        /// </summary>
        public static RemoteItemData GetOrNull(string itemId)
        {
            return TryGet(itemId, out var d) ? d : null;
        }

        /// <summary>
        /// Enumerate all cached items (read-only snapshot semantics for callers).
        /// </summary>
        public static IEnumerable<KeyValuePair<string, RemoteItemData>> All()
        {
            var dict = Db;
            foreach (var kv in dict)
                yield return kv;
        }

        public static Dictionary<string, RemoteItemData> Load()
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
                        var key = NormalizeId(rawKey);
                        var serializable = wrap.values[i];

                        var itemData = ConvertFromSerializable(serializable);

                        // Load icon sprite if exists
                        var iconPathNorm = Path.Combine(IconsPath, key + ".png");
                        var iconPathLegacy = Path.Combine(IconsPath, rawKey + ".png");
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
                                    itemData.IconSprite = CreateSprite(tex);
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

        public static void Save(Dictionary<string, RemoteItemData> data)
        {
            var src = data ?? NewDict();
            _cache = NewDict();

            foreach (var kv in src)
            {
                var nid = NormalizeId(kv.Key);
                _cache[nid] = kv.Value;
            }

            if (!Directory.Exists(IconsPath))
            {
                Directory.CreateDirectory(IconsPath);
            }

            var wrap = new Wrapper();
            foreach (var kv in _cache)
            {
                var nid = NormalizeId(kv.Key);
                wrap.keys.Add(nid);
                wrap.values.Add(ConvertToSerializable(kv.Value));

                var item = kv.Value;
                if (item.IconSprite != null && item.IconSprite.texture != null)
                {
                    try
                    {
                        var tex = item.IconSprite.texture;
                        byte[] pngData = tex.EncodeToPNG();
                        if (pngData != null)
                        {
                            var iconFilePath = Path.Combine(IconsPath, nid + ".png");
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

        /// <summary>
        /// Clears only the in-memory cache. Next Load() will re-read from disk.
        /// </summary>
        public static void ClearMemoryCache()
        {
            _cache = null;
        }

        #region Helpers

        private static string NormalizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            return id.Trim().ToLowerInvariant();
        }

        private static Sprite CreateSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static RemoteItemData ConvertFromSerializable(SerializableItemData s)
        {
            return new RemoteItemData
            {
                ItemName = s.itemName,
                ItemDescription = s.itemDescription,
                ItemIconUrl = s.itemIconUrl,
                ItemPremiumPrice = s.itemPremiumPrice,
                ItemGetPrice = s.itemGetPrice,
                ItemIsConsumable = s.itemIsConsumable,
                ItemIsRewardedAd = s.itemIsRewardedAd,
                ItemReferralThreshold = s.itemReferralThreshold,
                CoinMultiplierPercent = s.coinMultiplierPercent,
                ComboPower = s.comboPower,
                GameplaySpeedMultiplierPercent = s.gameplaySpeedMultiplierPercent,
                MagnetPowerPercent = s.magnetPowerPercent,
                PlayerAcceleration = s.playerAcceleration,
                PlayerSizePercent = s.playerSizePercent,
                PlayerSpeed = s.playerSpeed
            };
        }

        private static SerializableItemData ConvertToSerializable(RemoteItemData d)
        {
            return new SerializableItemData
            {
                itemName = d.ItemName,
                itemDescription = d.ItemDescription,
                itemIconUrl = d.ItemIconUrl,
                itemPremiumPrice = d.ItemPremiumPrice,
                itemGetPrice = d.ItemGetPrice,
                itemIsConsumable = d.ItemIsConsumable,
                itemIsRewardedAd = d.ItemIsRewardedAd,
                itemReferralThreshold = d.ItemReferralThreshold,
                coinMultiplierPercent = d.CoinMultiplierPercent,
                comboPower = d.ComboPower,
                gameplaySpeedMultiplierPercent = d.GameplaySpeedMultiplierPercent,
                magnetPowerPercent = d.MagnetPowerPercent,
                playerAcceleration = d.PlayerAcceleration,
                playerSizePercent = d.PlayerSizePercent,
                playerSpeed = d.PlayerSpeed
            };
        }

        #endregion
    }
}
