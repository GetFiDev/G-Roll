using System.Collections.Generic;

namespace GRoll.Core.SceneManagement
{
    /// <summary>
    /// Sahne isimleri ve build index'leri icin merkezi registry
    /// </summary>
    public static class SceneRegistry
    {
        // Sahne isimleri (Unity'deki isimlerle eslesir)
        public const string BootScene = "Boot Scene";
        public const string AuthScene = "Auth Scene";
        public const string MetaScene = "Meta Scene";
        public const string GameplayScene = "Gameplay Scene";
        public const string LoadingScene = "Loading Scene";

        /// <summary>
        /// SceneType -> Sahne ismi mapping
        /// </summary>
        public static readonly IReadOnlyDictionary<SceneType, string> SceneNames = new Dictionary<SceneType, string>
        {
            [SceneType.Boot] = BootScene,
            [SceneType.Auth] = AuthScene,
            [SceneType.Meta] = MetaScene,
            [SceneType.Gameplay] = GameplayScene,
            [SceneType.Loading] = LoadingScene
        };

        /// <summary>
        /// SceneType -> Build Settings index mapping
        /// </summary>
        public static readonly IReadOnlyDictionary<SceneType, int> SceneIndices = new Dictionary<SceneType, int>
        {
            [SceneType.Boot] = 0,
            [SceneType.Auth] = 1,
            [SceneType.Meta] = 2,
            [SceneType.Gameplay] = 3,
            [SceneType.Loading] = 4
        };

        /// <summary>
        /// Sahne isminden SceneType'a donusturur
        /// </summary>
        public static SceneType? GetSceneType(string sceneName)
        {
            foreach (var kvp in SceneNames)
            {
                if (kvp.Value == sceneName)
                    return kvp.Key;
            }
            return null;
        }

        /// <summary>
        /// SceneType'tan sahne ismine donusturur
        /// </summary>
        public static string GetSceneName(SceneType sceneType)
        {
            return SceneNames.TryGetValue(sceneType, out var name) ? name : null;
        }
    }
}
