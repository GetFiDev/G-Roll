#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace GRoll.Editor.iOS
{
    /// <summary>
    /// Automatically adds Game Center capability and GameKit.framework to iOS builds.
    /// This ensures Game Center authentication works without manual Xcode configuration.
    /// </summary>
    public static class iOSGameCenterPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            
            UnityEngine.Debug.Log("[iOSGameCenterPostProcess] Adding Game Center capability...");
            
            // --- 1. Add GameKit.framework to Xcode project ---
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            
            var mainTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            
            // Add GameKit.framework (required = false means it's optional, but we want it required)
            project.AddFrameworkToProject(mainTarget, "GameKit.framework", false);
            project.AddFrameworkToProject(frameworkTarget, "GameKit.framework", false);
            
            UnityEngine.Debug.Log("[iOSGameCenterPostProcess] Added GameKit.framework");
            
            project.WriteToFile(projectPath);
            
            // --- 2. Add Game Center entitlement ---
            var entitlementsFileName = "Unity-iPhone.entitlements";
            var entitlementsPath = Path.Combine(path, "Unity-iPhone", entitlementsFileName);
            
            // Create directory if needed
            var entitlementsDir = Path.GetDirectoryName(entitlementsPath);
            if (!Directory.Exists(entitlementsDir))
            {
                Directory.CreateDirectory(entitlementsDir);
            }
            
            // Read existing or create new entitlements file
            var entitlements = new PlistDocument();
            if (File.Exists(entitlementsPath))
            {
                entitlements.ReadFromFile(entitlementsPath);
            }
            
            // Add Game Center entitlement
            entitlements.root.SetBoolean("com.apple.developer.game-center", true);
            entitlements.WriteToFile(entitlementsPath);
            
            UnityEngine.Debug.Log("[iOSGameCenterPostProcess] Added Game Center entitlement");
            
            // --- 3. Update project to use entitlements file ---
            project.ReadFromFile(projectPath);
            project.SetBuildProperty(mainTarget, "CODE_SIGN_ENTITLEMENTS", "Unity-iPhone/" + entitlementsFileName);
            project.WriteToFile(projectPath);
            
            UnityEngine.Debug.Log("[iOSGameCenterPostProcess] ✅ Game Center setup complete!");
        }
    }
}
#endif
