#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;

namespace RemoteApp
{
    /// <summary>
    /// MapManager holds ordered map JSONs for runtime generation.
    /// It does NOT talk to Firebase directly. All remote access is delegated
    /// to RemoteAppDataService (single gateway for Firestore/Functions).
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        [Title("Dependencies")]
        [SerializeField] private RemoteAppDataService remoteService;

        [Title("Fetch Settings")]
        [Range(1, 50)] public int count = 24;
        [Tooltip("Optional seed. Leave empty to let the service decide.")]
        public string seedOverride = "";

        [Title("Runtime State"), ReadOnly] public bool isReady;
        [ReadOnly] public List<string> mapJsonList = new();
        [ReadOnly] public List<string> mapIds = new();
        [ReadOnly] public List<int> difficulties = new();

        [Title("Build Settings")]
        [SerializeField] private GameObject mapPrefab; // must have Map component
        [SerializeField] private Transform mapsParent; // optional parent for all maps
        [SerializeField] private Vector3 startPosition = Vector3.zero; // first map origin
        [SerializeField] private Vector3 step = new Vector3(0f, 0f, 50f); // delta per map
        [SerializeField] private bool clearBeforeBuild = true;

        [Title("Starter Map (Always First)")]
        [Tooltip("Eğer doluysa, fetch sonucu ne olursa olsun oyun bu JSON'lu map ile başlar.")]
        [SerializeField, TextArea(4, 12)] private string starterMapJson = "";
        [SerializeField, Tooltip("Starter map için opsiyonel görünen isim. Boşsa JSON içindeki mapName kullanılır.")]
        private string starterMapDisplayName = "starter";
        [SerializeField, Tooltip("Starter map'i zorla ilk sıraya koy")]
        private bool useStarterMap = true;

        public event Action OnReady;
        public event Action OnDeinitialized;

        private GameMode currentMode;

        public async Task Initialize(GameMode mode)
        {
            currentMode = mode;
            isReady = false;
            mapJsonList.Clear();
            mapIds.Clear();
            difficulties.Clear();

            if (remoteService == null)
            {
                Debug.LogError("[MapManager] RemoteAppDataService is not assigned.");
                return;
            }

            try
            {
                if (mode == GameMode.Chapter)
                {
                    // --- CHAPTER MODE FETCH ---
                    int chapterIndex = 1;
                    if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
                    {
                        chapterIndex = UserDatabaseManager.Instance.currentUserData.chapterProgress;
                    }

                    Debug.Log($"[MapManager] Fetching Chapter {chapterIndex}...");
                    
                    // Fetch directly using the helper we added to UserDatabaseManager
                    string json = null;
                    if (UserDatabaseManager.Instance != null)
                    {
                        json = await UserDatabaseManager.Instance.FetchChapterMapJsonAsync(chapterIndex);
                    }
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        mapJsonList.Add(json);
                        mapIds.Add($"chapter_{chapterIndex}");
                        difficulties.Add(1);
                    }
                    else
                    {
                        Debug.LogError($"[MapManager] Chapter {chapterIndex} map not found!");
                    }
                }
                else
                {
                    // --- ENDLESS MODE FETCH (Existing Logic) ---
                    var seed = string.IsNullOrWhiteSpace(seedOverride) ? null : seedOverride.Trim();
                    int fetchCount = 24; // Keep standard count or increase for endless pool
                    var resp = await remoteService.GetSequencedMapsAsync(fetchCount, seed);

                    if (resp != null && resp.ok && resp.entries != null)
                    {
                        foreach (var e in resp.entries)
                        {
                            if (e == null) continue;
                            mapIds.Add(e.mapId ?? "");
                            difficulties.Add(e.difficultyTag);
                            mapJsonList.Add(e.json ?? "");
                        }
                    }
                }

                isReady = true;
                Debug.Log($"[MapManager] Ready. Loaded {mapJsonList.Count} maps for mode {mode}.");
                
                // Build maps
                BuildSequenceFromJsons();
                OnReady?.Invoke();

            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapManager] Initialize error: {ex.Message}\n{ex}");
            }
        }

        [Serializable]
        private class MapNameDto { public string mapName; }

        private static string ToSafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "map";
            var chars = new char[s.Length];
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    chars[n++] = c;
                }
                else if (c == ' ')
                {
                    chars[n++] = '_';
                }
                else
                {
                    // collapse other symbols to '-'
                    chars[n++] = '-';
                }
            }
            return new string(chars, 0, n);
        }

        private void ClearBuiltMaps()
        {
            var parent = mapsParent != null ? mapsParent : this.transform;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                    UnityEditor.Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
                return;
            }
#endif
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private Transform playerTransform;

        private void Update()
        {
            if (!isReady || currentMode != GameMode.Endless) return;
            
            // Allow player ref to be found lazily
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player) playerTransform = player.transform;
                else return;
            }

            // Endless Spawning Logic
            float playerZ = playerTransform.position.z;
            
            // Despawn behind
            RecycleMapsBehind(playerZ);

            // Spawn ahead (ensure we always have maps ahead)
            // Example: Keep 3 maps ahead (~150m if step is 50)
            // Check the Z of the last spawned map
            // mapIds/mapJsonList are just the *pool* or *fetched list*
            // We need to track *instantiated* maps to know where the "end" is.
        }

        // Track active map instances to manage the world window
        private LinkedList<Map> activeMaps = new LinkedList<Map>();
        private Vector3 nextSpawnPos;

        private void BuildSequenceFromJsons()
        {
            if (mapPrefab == null)
            {
                Debug.LogError("[MapManager] mapPrefab is not assigned.");
                return;
            }
  
            var parent = mapsParent != null ? mapsParent : this.transform;
            if (clearBeforeBuild) ClearBuiltMaps();
            
            activeMaps.Clear();
            nextSpawnPos = startPosition;

            // Common Starter Map Logic (applies to both, or only Endless?)
            // Usually Chapter 1 starts directly with the Chapter map.
            // Endless starts with a generic starter.
            // Let's adhere to: Chapter -> Direct Chapter Map. Endless -> Starter + Pool.
            if (currentMode == GameMode.Endless && useStarterMap && !string.IsNullOrWhiteSpace(starterMapJson))
            {
                SpawnMap(starterMapJson, "starter");
            }

            // 1) Build Initial Set
            if (currentMode == GameMode.Chapter)
            {
                // Build all fetched (should be just 1 for chapter)
                foreach (var json in mapJsonList)
                {
                    if (string.IsNullOrWhiteSpace(json)) continue;
                    SpawnMap(json, "chapter_map");
                }
            }
            else // Endless
            {
                // Build initial buffer (e.g. 2 maps from pool)
                for (int i = 0; i < 3; i++)
                {
                    SpawnRandomFromPool();
                }
            }
  
            Debug.Log($"[MapManager] Built {activeMaps.Count} maps. Mode: {currentMode}");
        }

        private void SpawnRandomFromPool()
        {
            if (mapJsonList.Count == 0) return;
            int idx = UnityEngine.Random.Range(0, mapJsonList.Count);
            SpawnMap(mapJsonList[idx], $"endless_{idx}");
        }

        private void SpawnMap(string json, string nameContext)
        {
             var parent = mapsParent != null ? mapsParent : this.transform;
             var inst = Instantiate(mapPrefab, nextSpawnPos, Quaternion.identity, parent);
             var map = inst.GetComponent<Map>();
             
             // Name
             string mapName = null;
             try { mapName = JsonUtility.FromJson<MapNameDto>(json)?.mapName; } catch {}
             string safe = ToSafeName(mapName ?? nameContext);
             inst.name = $"{activeMaps.Count:00}_{safe}";

             if (map)
             {
                 map.mapJson = json;
                 map.Initialize();
                 activeMaps.AddLast(map);
             }
             else Destroy(inst);

             nextSpawnPos += step;
        }

        private void RecycleMapsBehind(float playerZ)
        {
            // Destroy maps that are sufficiently behind the player
            // Step is 50. Let's keep 1 map behind (safe zone).
            // So if map.Z + 50 < playerZ - 100 ...
            
            while (activeMaps.Count > 0)
            {
                var first = activeMaps.First.Value;
                // Assuming pivot is at Z start of map, and map length is roughly 'step.z'
                // Or safely: if player is > mapStart + 150
                if (first == null) 
                {
                    activeMaps.RemoveFirst(); 
                    continue; 
                }

                float mapEndZ = first.transform.position.z + step.z;
                if (playerZ > mapEndZ + 20f) // 20m buffer behind
                {
                    // Despawn
                    Destroy(first.gameObject);
                    activeMaps.RemoveFirst();
                }
                else
                {
                    break; // Oldest map is still close enough
                }
            }
            
            // Ensure we maintain lookahead
            // If last map start Z is < playerZ + 200, spawn more
            if (activeMaps.Count > 0)
            {
                var last = activeMaps.Last.Value;
                if (last.transform.position.z < playerZ + 200f)
                {
                    SpawnRandomFromPool();
                }
            }
            else if (currentMode == GameMode.Endless)
            {
                 // Failsafe: if no maps, spawn one at next
                 SpawnRandomFromPool();
            }
        }
        
        /// <summary>
        /// Destroys all built map instances and resets runtime state.
        /// Call this when the run ends to return to meta scene cleanly.
        /// </summary>
        public void DeinitializeMap()
        {
            // Remove any instantiated maps from hierarchy
            ClearBuiltMaps();
            activeMaps.Clear();
            playerTransform = null;
  
            // Reset runtime state
            isReady = false;
            mapJsonList.Clear();
            mapIds.Clear();
            difficulties.Clear();
  
            OnDeinitialized?.Invoke();
            Debug.Log("[MapManager] Deinitialized. Maps cleared and runtime state reset.");
        }
    }

    // -------- DTOs expected from RemoteAppDataService --------

    [Serializable]
    public class SequencedMapEntry
    {
        public string mapId;
        public int difficultyTag;
        public string json;
    }

    [Serializable]
    public class SequencedMapsResponse
    {
        public bool ok;
        public int count;
        public List<int> pattern;
        public List<SequencedMapEntry> entries;
    }
}
