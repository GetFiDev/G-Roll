using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

// ReSharper disable InconsistentNaming
namespace Plugins.Tools.Editor
{
    /// <summary>
    /// Actions for command line build
    /// </summary>
    public class BuildActions
    {
        /// <summary>
        /// Current project source path
        /// </summary>
        public static string APP_FOLDER = Directory.GetCurrentDirectory();

        /// <summary>
        /// iOS files path
        /// </summary>
        public static string IOS_FOLDER = $"{APP_FOLDER}/Builds/iOS/";
        
        
        /// <summary>
        /// Android files path
        /// </summary>
        public static string ANDROID_FOLDER = $"{APP_FOLDER}/Builds/Android/{PlayerSettings.Android.bundleVersionCode}.aab";

        /// <summary>
        /// Get active scene list
        /// </summary>
        static string[] GetScenes()
        {
            List<string> scenes = new List<string>();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                {
                    scenes.Add(EditorBuildSettings.scenes[i].path);
                }
            }

            return scenes.ToArray();
        }
        
        /// <summary>
        /// Run iOS release build
        /// </summary>
        static void iOSRelease()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            // PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, null);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            
            BuildReport report = BuildPipeline.BuildPlayer(GetScenes(), IOS_FOLDER, BuildTarget.iOS, BuildOptions.None);
            int code = (report.summary.result == BuildResult.Succeeded) ? 0 : 1;
            EditorApplication.Exit(code);
        }

        static void AndroidRelease()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            // PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, null);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
            EditorUserBuildSettings.buildAppBundle = true;
            
            BuildReport report = BuildPipeline.BuildPlayer(GetScenes(), ANDROID_FOLDER, BuildTarget.Android, BuildOptions.None);
            int code = (report.summary.result == BuildResult.Succeeded) ? 0 : 1;
            
            EditorApplication.Exit(code);
        }
    }
}