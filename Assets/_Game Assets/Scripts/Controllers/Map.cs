// Map.cs (simplified, no reflection, no fail-safe extras)
using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

// These types are expected to already exist in your project:
/// BuildDatabase: holds a list of BuildableItem and/or exposes GetById(string)
/// BuildableItem: has string id; GameObject prefab;
/// MapGridCellUtility: has Vector3 GridToWorld(int x, int y);

public class Map : MonoBehaviour
{
    [Title("Input JSON")]
    [Tooltip("Map JSON string (exactly what comes from backend).")]
    [TextArea(6, 20)]
    public string mapJson;

    [Title("Dependencies (required)")]
    [SerializeField] private BuildDatabase buildDatabase;
    [SerializeField] private MapGridCellUtility gridUtility;
    [SerializeField] private Transform contentParent;

    [Title("Options")]
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool renameInstancesWithIds = true;

    [Title("Background")]
    [Tooltip("Exactly 3 mesh renderers that will receive the selected background material.")]
    [SerializeField] private MeshRenderer[] backgroundRenderers = new MeshRenderer[3];

    [Tooltip("Background materials list. Index is 'backgroundMaterialId' from JSON.")]
    [SerializeField] private List<Material> backgroundMaterials = new List<Material>();

    // One-click build from Inspector
    [Button("Build From JSON"), GUIColor(0.2f, 0.7f, 1f)]
    public void Initialize()
    {
        if (buildDatabase == null) { Debug.LogError("[Map] BuildDatabase is null."); return; }
        if (gridUtility   == null) { Debug.LogError("[Map] MapGridCellUtility is null."); return; }
        if (string.IsNullOrWhiteSpace(mapJson)) { Debug.LogError("[Map] mapJson is empty."); return; }

        MapSaveData data;
        try
        {
            data = JsonUtility.FromJson<MapSaveData>(mapJson);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Map] JSON parse error: {e.Message}");
            return;
        }

        if (data == null || data.items == null || data.items.Count == 0)
        {
            Debug.LogError("[Map] Parsed data has no items.");
            return;
        }

        var parent = contentParent != null ? contentParent : transform;
        if (clearBeforeBuild) ClearChildren(parent);

        // Apply background material (simple index lookup)
        if (backgroundMaterials != null && backgroundMaterials.Count > 0)
        {
            var idx = Mathf.Clamp(data.backgroundMaterialId, 0, backgroundMaterials.Count - 1);
            var mat = backgroundMaterials[idx];
            if (mat != null && backgroundRenderers != null)
            {
                for (int i = 0; i < backgroundRenderers.Length; i++)
                {
                    var mr = backgroundRenderers[i];
                    if (mr == null) continue;
                    mr.material = mat; // instance per renderer; use sharedMaterial if you want to avoid instancing
                }
            }
        }

        int spawned = 0;
        foreach (var it in data.items)
        {
            // DisplayName in JSON is the same as BuildableItem.id (as per your note)
            var def = buildDatabase.GetById(it.displayName);
            if (def == null)
            {
                Debug.LogError($"[Map] Buildable not found: '{it.displayName}'");
                continue;
            }
            if (def.prefab == null)
            {
                Debug.LogError($"[Map] Prefab missing on Buildable '{def.displayName}'");
                continue;
            }

            Vector3 pos = gridUtility.GridToWorld(it.gridX, it.gridY);
            var go = Instantiate(def.prefab, pos, Quaternion.identity, parent);
            if (renameInstancesWithIds && !string.IsNullOrEmpty(it.displayName))
                go.name = $"{it.displayName} ({it.gridX},{it.gridY})";

            spawned++;
        }

        Debug.Log($"[Map] Build complete. Spawned={spawned} " +
                  $"difficulty={data.difficultyTag} bgMatId={data.backgroundMaterialId}");
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
    #if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(c.gameObject);
            else Destroy(c.gameObject);
    #else
            Destroy(c.gameObject);
    #endif
        }
    }

    // --- Data models (must match backend JSON) ---
    [Serializable]
    private class MapSaveData
    {
        public string savedAtIso;
        public string mapName;
        public string mapDisplayName;
        public int difficultyTag;
        public int backgroundMaterialId;
        public List<MapItem> items;
    }

    [Serializable]
    private class MapItem
    {
        public string displayName;
        public int gridX;
        public int gridY;
        public int linkedPortalId;
    }
}