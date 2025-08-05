using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Plugins.Tools.Editor
{
    internal class MyCustomBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            EditorStartSceneSetter.SetBootScene(true);
        }
    }
}