using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ----- DTO'lar -----
[Serializable]
public class MapSaveData
{
    public string savedAtIso;
    public List<PlacedItemSave> items = new List<PlacedItemSave>();
}

[Serializable]
public class PlacedItemSave
{
    public string displayName;
    public int gridX;
    public int gridY;
}

// ----- Kaydedici -----
public class MapSaver : MonoBehaviour
{
    [Header("Optional Search Scope")]
    [Tooltip("Boşsa tüm sahne taranır. Dilersen sadece bu parent altında arat.")]
    public Transform searchRoot;

    // UI'daki "Save Map" butonuna bağla
    public void SaveMap()
    {
        var data = Collect();
        var json = JsonUtility.ToJson(data, true);

        string dir = Path.Combine(Application.dataPath, "MapSaveJsons");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path  = Path.Combine(dir, $"{stamp}_SavedMap.json");

        File.WriteAllText(path, json);
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        Debug.Log($"✅ Map saved: {path}");
    }

    private MapSaveData Collect()
    {
        var data = new MapSaveData
        {
            savedAtIso = DateTime.Now.ToString("o")
        };

        var placed = GetSceneComponents<PlacedItemData>();
        foreach (var p in placed)
        {
            if (!p) continue;

            // BuildableItem'i değiştirmiyoruz; displayName'in orada olduğunu varsayıyoruz.
            string nameField = p.item ? p.item.displayName : "";
            if (string.IsNullOrWhiteSpace(nameField))
                nameField = p.item ? p.item.name : p.gameObject.name;

            data.items.Add(new PlacedItemSave
            {
                displayName = nameField,
                gridX = p.gridX,
                gridY = p.gridY
            });
        }

        return data;
    }

    private List<T> GetSceneComponents<T>() where T : Component
    {
        if (searchRoot) return searchRoot.GetComponentsInChildren<T>(true).ToList();
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<T>(FindObjectsSortMode.None).ToList();
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true).ToList();
#endif
    }
}