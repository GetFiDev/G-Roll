// Map.cs (using shared data types from MapDataTypes.cs)
using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using MapDesignerTool; // for IMapConfigurable

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

    // ---- Portals (collected during build, linked after build) ----
    private class SpawnedPortal
    {
        public Teleport teleport;
        public int linkId;
    }
    private readonly List<SpawnedPortal> _spawnedPortals = new List<SpawnedPortal>();

    // ---- Button-Door pairs (collected during build, linked after build) ----
    private class SpawnedButtonDoor
    {
        public TriggerPushButton button;
        public TriggerableDoor door;
        public int linkId;
    }
    private readonly List<SpawnedButtonDoor> _spawnedButtonDoors = new List<SpawnedButtonDoor>();

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
            Debug.Log("[Warning] Parsed data has no items.");
            return;
        }

        var parent = contentParent != null ? contentParent : transform;
        if (clearBeforeBuild) ClearChildren(parent);
        _spawnedPortals.Clear();
        _spawnedButtonDoors.Clear();

        // Apply background material (simple index lookup)
        if (backgroundMaterials != null && backgroundMaterials.Count > 0)
        {
            // data.backgroundMaterialId is 1-based (1..N)
            var idx = Mathf.Clamp(data.backgroundMaterialId, 1, backgroundMaterials.Count);
            var mat = backgroundMaterials[idx-1]; // 0-based index
            if (mat != null && backgroundRenderers != null)
            {
                for (int i = 0; i < backgroundRenderers.Length; i++)
                {
                    var mr = backgroundRenderers[i];
                    if (mr == null) continue;
                    mr.material = mat; 
                }
            }
        }

        int spawned = 0;
        foreach (var it in data.items)
        {
            // ID lookup
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

            // Calculate Position
            Vector3 pos = gridUtility.GridToWorld(it.gridX, it.gridY);
            
            // Calculate Rotation from Index (0=0, 1=90, 2=180, 3=270)
            Quaternion rot = Quaternion.Euler(0, 90f * it.rotationIndex, 0);

            var go = Instantiate(def.prefab, pos, rot, parent);
            
            if (renameInstancesWithIds && !string.IsNullOrEmpty(it.displayName))
                go.name = $"{it.displayName} ({it.gridX},{it.gridY})";

            // --- CONFIG APPLICATION ---
            // 1. If PlacedItemData exists (it likely doesn't on clean prefabs, but if MapEditor uses same prefabs as Game, check it)
            // Typically "Game" prefabs might differ from "Editor Helper" components.
            // But usually we want to apply configs to the Game Logic scripts (Movement, Rotation, etc).
            // We look for IMapConfigurable interfaces on the spawned object.
            
            var configurables = go.GetComponentsInChildren<IMapConfigurable>(true);
            if (configurables != null && configurables.Length > 0 && it.config != null && it.config.Count > 0)
            {
                // Convert List<ConfigPair> to Dictionary
                var dict = new Dictionary<string, string>();
                foreach (var pair in it.config)
                {
                    if (!string.IsNullOrEmpty(pair.key))
                    {
                        dict[pair.key] = pair.value;
                    }
                }

                // Apply to all IMapConfigurables found
                foreach (var cfg in configurables)
                {
                    cfg.ApplyConfig(dict);
                }
            }
            
            // Also, if we are in Editor/Runtime that uses PlacedItemData for logic (e.g. Map Editor Load),
            // we might want to populate it back.
            // But this Map.cs seems to be for the "Gameplay" scene logic (Loader).
            // PlacedItemData is an EDITOR tool component usually.
            // If the prefab has it, we can populate it, but usually invalid in Gameplay.
            // We ignore PlacedItemData population here since ApplyConfig does the job for Logic scripts.


            // If this item is a Portal, strip runtime link components and collect for linking
            if (string.Equals(it.displayName, "Portal", StringComparison.OrdinalIgnoreCase))
            {
                // Destroy TeleportLinkRuntime components on any children of this portal instance
                var linkRuntimes = go.GetComponentsInChildren<TeleportLinkRuntime>(true);
                for (int k = 0; k < linkRuntimes.Length; k++)
                {
                    DestroyImmediate(linkRuntimes[k]);
                }

                // Find Teleport component (on a child) to link later
                var tele = go.GetComponentInChildren<Teleport>(true);
                if (tele != null)
                {
                    _spawnedPortals.Add(new SpawnedPortal
                    {
                        teleport = tele,
                        linkId = it.linkedPortalId
                    });
                }
                else
                {
                    Debug.LogWarning("[Map] Spawned Portal has no Teleport component in children.");
                }
            }

            // If this item is a Button or Door with pairing, collect for linking
            if (it.linkedButtonDoorId > 0)
            {
                // Destroy ButtonDoorLinkRuntime components (editor-only)
                var bdLinks = go.GetComponentsInChildren<ButtonDoorLinkRuntime>(true);
                for (int k = 0; k < bdLinks.Length; k++)
                {
                    DestroyImmediate(bdLinks[k]);
                }

                var btn = go.GetComponentInChildren<TriggerPushButton>(true);
                var door = go.GetComponentInChildren<TriggerableDoor>(true);
                
                if (btn != null)
                {
                    _spawnedButtonDoors.Add(new SpawnedButtonDoor
                    {
                        button = btn,
                        door = null,
                        linkId = it.linkedButtonDoorId
                    });
                }
                else if (door != null)
                {
                    _spawnedButtonDoors.Add(new SpawnedButtonDoor
                    {
                        button = null,
                        door = door,
                        linkId = it.linkedButtonDoorId
                    });
                }
            }

            spawned++;
        }

        // Link portals after all items are spawned
        LinkSpawnedPortals();
        
        // Link Button-Door pairs
        LinkSpawnedButtonDoors();

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

    private void LinkSpawnedPortals()
    {
        if (_spawnedPortals.Count == 0) return;

        // Group portals by their linkedPortalId
        var groups = new Dictionary<int, List<Teleport>>();
        for (int i = 0; i < _spawnedPortals.Count; i++)
        {
            var sp = _spawnedPortals[i];
            if (!groups.TryGetValue(sp.linkId, out var list))
            {
                list = new List<Teleport>();
                groups[sp.linkId] = list;
            }
            if (sp.teleport != null) list.Add(sp.teleport);
        }

        // For each group, assign otherPortal references
        foreach (var kv in groups)
        {
            var list = kv.Value;
            if (list == null || list.Count < 2) continue; // needs at least a pair

            if (list.Count == 2)
            {
                // Standard pair: A <-> B
                list[0].otherPortal = list[1];
                list[1].otherPortal = list[0];
            }
            else
            {
                // More than 2 with the same link id: link in a ring (A->B->C->A)
                for (int i = 0; i < list.Count; i++)
                {
                    var next = list[(i + 1) % list.Count];
                    list[i].otherPortal = next;
                }
            }
        }
    }

    private void LinkSpawnedButtonDoors()
    {
        if (_spawnedButtonDoors.Count == 0) return;

        // Group by linkId, then pair buttons with doors
        var groups = new Dictionary<int, (List<TriggerPushButton> buttons, List<TriggerableDoor> doors)>();
        
        foreach (var sbd in _spawnedButtonDoors)
        {
            if (!groups.TryGetValue(sbd.linkId, out var group))
            {
                group = (new List<TriggerPushButton>(), new List<TriggerableDoor>());
                groups[sbd.linkId] = group;
            }
            
            if (sbd.button != null) group.buttons.Add(sbd.button);
            if (sbd.door != null) group.doors.Add(sbd.door);
        }

        // For each group, assign button.targetDoor
        foreach (var kv in groups)
        {
            var (buttons, doors) = kv.Value;
            if (buttons.Count == 0 || doors.Count == 0) continue;

            // Simple 1:1 pairing (first button to first door)
            // If multiple buttons/doors share same ID, they all link to same door / all doors get triggered
            foreach (var btn in buttons)
            {
                btn.targetDoor = doors[0]; // Primary door
            }
            
            Debug.Log($"[Map] Linked {buttons.Count} button(s) to door (ID={kv.Key})");
        }
    }
}