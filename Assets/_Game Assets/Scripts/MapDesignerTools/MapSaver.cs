using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Firebase.Functions;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

// NOTE: Shared types (MapSaveData, MapItemData, ConfigPair) are in MapDataTypes.cs

public class MapSaver : MonoBehaviour
{
    [Header("Toast (optional)")]
    [SerializeField] private TextMeshProUGUI saveToast;
    [SerializeField, Min(0.1f)] private float toastDuration = 3f;

    private void Awake()
    {
        if (saveToast != null) saveToast.gameObject.SetActive(false);
    }
    
    // === Cloud Functions ===
    public async Task<string> SaveMapToCloud(MapSaveData data, bool forceOverwrite)
    {
        var functions = FirebaseFunctions.DefaultInstance;
        var func = functions.GetHttpsCallable("saveMap");

        var payload = new Dictionary<string, object>
        {
            { "mapName", data.mapName },
            { "mapDisplayName", data.mapDisplayName },
            { "mapType", data.mapType },
            { "mapOrder", data.mapOrder },
            { "mapLength", data.mapLength },
            { "difficultyTag", data.difficultyTag },
            { "force", forceOverwrite },
            { "json", JsonUtility.ToJson(data) }
        };

        try
        {
            var result = await func.CallAsync(payload);
            var resultData = result.Data as Dictionary<object, object>;
            
            if (resultData != null && resultData.ContainsKey("status"))
            {
                string status = resultData["status"].ToString();
                return status; // "success" or "exists"
            }
            return "error";
        }
        catch (FunctionsException ex)
        {
            Debug.LogError($"Cloud Function Error: {ex.ErrorCode} - {ex.Message}");
            return "error"; 
        }
        catch (Exception e)
        {
            Debug.LogError($"Save Error: {e.Message}");
            return "error";
        }
    }

    // === Collect ===
    public MapSaveData Collect(string name, string displayName, string type, int order, int difficulty, int length, int bgIndex)
    {
        // Calculate chunk count
        int chunkCount = Mathf.CeilToInt((float)length / MapGridCellUtility.GRID_CELLS_PER_CHUNK);
        
        var data = new MapSaveData 
        { 
            savedAtIso = DateTime.Now.ToString("o"),
            mapName = name,
            mapDisplayName = displayName,
            mapType = type,
            mapOrder = order,
            difficultyTag = difficulty,
            mapLength = length,
            chunkCount = chunkCount,
            backgroundMaterialId = bgIndex
        };

        // Items
        var placed = GetSceneComponents<PlacedItemData>();
        foreach (var p in placed)
        {
            if (!p) continue;

            string nameField = p.item ? (string.IsNullOrWhiteSpace(p.item.displayName) ? p.item.name : p.item.displayName) : p.gameObject.name;
            
            var itemData = new MapItemData
            {
                displayName   = nameField,
                gridX         = p.gridX,
                gridY         = p.gridY,
                rotationIndex = p.rotationIndex,
                linkedPortalId= p.linkedPortalId,
                linkedButtonDoorId = p.linkedButtonDoorId
            };
            
            p.SaveConfigFromRuntime(); 
            
            if (p.savedConfig != null)
            {
                itemData.config = new List<ConfigPair>(p.savedConfig);
            }
            
            data.items.Add(itemData);
        }

        return data;
    }

    private List<T> GetSceneComponents<T>() where T : Component
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<T>(FindObjectsSortMode.None).ToList();
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true).ToList();
#endif
    }
}