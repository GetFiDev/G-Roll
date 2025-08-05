using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plugins.Tools.Editor
{
    internal abstract class EditorStartSceneSetter
    {
        private const string BootScenePath = "Assets/_Game Assets/Scenes/Boot Scene.unity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void OnPlayModeStateChanged()
        {
            if (IsSceneEnabled(BootScenePath))
                SceneManager.LoadScene("Boot Scene");
        }

        private static bool IsSceneEnabled(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;

            return (from scene in scenes where scene.path == scenePath select scene.enabled).FirstOrDefault();
        }

        public static void SetBootScene(bool state)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.AddRange(EditorBuildSettings.scenes.ToList());
            scenes.FirstOrDefault(x => x.path == BootScenePath)!.enabled = state;
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}