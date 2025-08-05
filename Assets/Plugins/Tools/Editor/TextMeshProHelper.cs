using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Plugins.Tools.Editor
{
    public class TextMeshProHelper : AssetPostprocessor
    {
        private void OnPostprocessPrefab(GameObject newPrefab)
        {
            var textMeshProList = newPrefab.GetComponentsInChildren<TextMeshPro>(true).ToList();
            
            if (textMeshProList.Count > 0)
            {
                foreach (var _ in textMeshProList.Where(textMeshPro => !textMeshPro.font.name.Contains("3D")))
                {
                    Debug.LogError("This prefab has an 3D TextMeshPro with non 3D font Asset: " + newPrefab.name.LogColor(Color.cyan));
                }
            }
            
            var textMeshProUGUIList = newPrefab.GetComponentsInChildren<TextMeshProUGUI>(true).ToList();
            
            if (textMeshProUGUIList.Count > 0)
            {
                foreach (var _ in textMeshProUGUIList.Where(textMeshPro => textMeshPro.font.name.Contains("3D")))
                {
                    Debug.LogError("This prefab has an TextMeshProUGUI with 3D font Asset: " + newPrefab.name.LogColor(Color.cyan));
                }
            }
        }
    }
}