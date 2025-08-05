using System;
using UnityEditor;
using UnityEngine;

namespace _Game_Assets.Scripts.Utility.Editor
{
    [InitializeOnLoad]
    public abstract class DevTools
    {
        //To create a hotkey you can use the following special characters:
        //% (ctrl on Windows and Linux, cmd on macOS),
        //^ (ctrl on Windows, Linux, and macOS),
        //# (shift), & (alt).
        //If no special modifier key combinations are required the key can be given after an underscore.

        [MenuItem("Tools/Dev Tools/Clear All PlayerPrefs " + "&c", false, 100)]
        public static void ClearAllPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [MenuItem("Tools/Dev Tools/Next Level " + "&n", false, 100)]
        public static void NextLevel()
        {
            if (!Application.isPlaying)
                return;

            DataManager.IsTutorial = false;
            DataManager.CurrentLevelIndex++;
            LevelManager.ReloadScene();
        }
        
        [MenuItem("Tools/Dev Tools/Previous Level " + "&#n", false, 100)]
        public static void PreviousLevel()
        {
            if (!Application.isPlaying)
                return;

            DataManager.CurrentLevelIndex = DataManager.CurrentLevelIndex > 0 ? DataManager.CurrentLevelIndex - 1 : 0;
            DataManager.IsTutorial = DataManager.CurrentLevelIndex == 0;
            LevelManager.ReloadScene();
        }

        [MenuItem("Tools/Dev Tools/Add Money " + "&m", false, 100)]
        public static void AddMoney()
        {
            DataManager.Currency += 10000;
        }

        [MenuItem("Tools/Dev Tools/Clear Money " + "&#m", false, 100)]
        public static void ClearMoney()
        {
            DataManager.Currency = 1;
        }


        [MenuItem("Tools/Dev Tools/Reload Domain " + "&r", false, 100)]
        public static void ReloadDomain()
        {
            EditorUtility.RequestScriptReload();
        }

        static DevTools()
        {
            EditorApplication.update += ControlTimeScale;
        }

        private static void ControlTimeScale()
        {
            Time.timeScale = Application.isPlaying switch
            {
                true when Input.GetKey(KeyCode.S) => Input.GetKey(KeyCode.LeftShift) ? 7.5f : 2.5f,
                true when Input.GetKey(KeyCode.D) => Input.GetKey(KeyCode.LeftShift) ? .1f : .25f,
                _ => 1f
            };
        }
    }
}