using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class AchievementIconCache
{
    static readonly Dictionary<string, Sprite> _cache = new();

    public static async Task<Sprite> LoadSpriteAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (_cache.TryGetValue(url, out var sp) && sp != null) return sp;

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            var op = req.SendWebRequest();
#if UNITY_2020_3_OR_NEWER
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();
#else
            while (!req.isDone) await System.Threading.Tasks.Task.Yield();
#endif
            if (req.result != UnityWebRequest.Result.Success) return null;
            var tex = DownloadHandlerTexture.GetContent(req);
            sp = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f,0.5f));
            _cache[url] = sp;
            return sp;
        }
    }
}