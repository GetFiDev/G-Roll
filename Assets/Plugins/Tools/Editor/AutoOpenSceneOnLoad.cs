using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plugins.Tools.Editor
{
    [InitializeOnLoad]
    public static class AutoOpenSceneOnLoad
    {
        private const string GameScenePath = "Assets/_Game Assets/Scenes/Game Scene.unity";

        static AutoOpenSceneOnLoad()
        {
            if (SessionState.GetBool("FirstInitDone", false)) 
                return;
                
            SessionState.SetBool("FirstInitDone", true);
            
            EditorApplication.delayCall += CheckLoadedScene;
        }

        private static void CheckLoadedScene()
        {
            EditorApplication.delayCall -= CheckLoadedScene;
            
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                return;

            Debug.Log("No recent scenes found.".LogColor(Color.green) + " Opening scene from : " + GameScenePath);
            
            EditorSceneManager.OpenScene(GameScenePath);
        }
    }
}