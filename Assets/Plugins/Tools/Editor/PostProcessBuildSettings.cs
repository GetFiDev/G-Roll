#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Plugins.Tools.Editor
{
    public static class PostProcessBuildSettings
    {
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuildProject)
        {
            if (buildTarget != BuildTarget.iOS)
                return;
            
            var pbxPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(pbxPath);

            //Disabling Bitcode on all targets
            //Main
            pbxProject.SetBuildProperty(pbxProject.GetUnityMainTargetGuid(), "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(pbxProject.GetUnityMainTargetGuid(), "ITSAppUsesNonExemptEncryption", "NO");
            //pbxProject.SetBuildProperty(pbxProject.GetUnityMainTargetGuid(), "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            //Unity Tests
            pbxProject.SetBuildProperty(pbxProject.TargetGuidByName(PBXProject.GetUnityTestTargetName()),
                "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(pbxProject.TargetGuidByName(PBXProject.GetUnityTestTargetName()),
                "ITSAppUsesNonExemptEncryption", "NO");
            //pbxProject.SetBuildProperty(pbxProject.TargetGuidByName(PBXProject.GetUnityTestTargetName()), "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            //Unity Framework
            pbxProject.SetBuildProperty(pbxProject.GetUnityFrameworkTargetGuid(), "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(pbxProject.GetUnityFrameworkTargetGuid(), "ITSAppUsesNonExemptEncryption",
                "NO");
            pbxProject.SetBuildProperty(pbxProject.GetUnityFrameworkTargetGuid(),
                "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            pbxProject.WriteToFile(pbxPath);

            var guid = pbxProject.GetUnityMainTargetGuid();
            var idArray = Application.identifier.Split('.');
            var entitlementsPath = $"Unity-iPhone/{idArray[^1]}.entitlements";
            var capManager = new ProjectCapabilityManager(pbxPath, entitlementsPath, null, guid);

            // Add Push Notifications Capability to Project
            capManager.AddPushNotifications(false);

            capManager.WriteToFile();
        }
    }

    public class ExcemptFromEncryption : IPostprocessBuildWithReport // Will execute after XCode project is built
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) // Check if the build is for iOS 
                return;

            var plistPath = report.summary.outputPath + "/Info.plist";

            var plist = new PlistDocument(); // Read Info.plist file into memory
            plist.ReadFromString(File.ReadAllText(plistPath));

            var rootDict = plist.root;
            rootDict.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            File.WriteAllText(plistPath, plist.WriteToString()); // Override Info.plist
        }
    }
}
#endif