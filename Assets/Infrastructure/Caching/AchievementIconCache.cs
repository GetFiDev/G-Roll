using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GRoll.Infrastructure.Caching
{
    /// <summary>
    /// In-memory cache for achievement icons loaded from URLs.
    /// Uses TTL to prevent stale icons.
    /// </summary>
    public static class AchievementIconCache
    {
        // Cache with 24-hour TTL, max 200 icons
        private static readonly MemoryCache<string, Sprite> _cache = new(
            defaultTtl: TimeSpan.FromHours(24),
            maxSize: 200,
            cleanupIntervalSeconds: 300 // Clean every 5 minutes
        );

        /// <summary>
        /// Load sprite from URL, using cache if available.
        /// </summary>
        public static async UniTask<Sprite> LoadSpriteAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            return await _cache.GetOrAddAsync(url, async () =>
            {
                try
                {
                    using var req = UnityWebRequestTexture.GetTexture(url);
                    await req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[AchievementIconCache] Failed to load: {url} - {req.error}");
                        return null;
                    }

                    var tex = DownloadHandlerTexture.GetContent(req);
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AchievementIconCache] Error loading {url}: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Check if a URL is cached.
        /// </summary>
        public static bool IsCached(string url)
        {
            return !string.IsNullOrWhiteSpace(url) && _cache.ContainsKey(url);
        }

        /// <summary>
        /// Get current cache size.
        /// </summary>
        public static int Count => _cache.Count;
    }
}
