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

        public async Task Initialize()
        {
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
                // Ask the gateway for sequenced maps (no Firebase calls here).
                var seed = string.IsNullOrWhiteSpace(seedOverride) ? null : seedOverride.Trim();
                var resp = await remoteService.GetSequencedMapsAsync(count, seed);

                if (resp == null)
                {
                    Debug.LogError("[MapManager] GetSequencedMapsAsync returned null.");
                    return;
                }
                if (!resp.ok || resp.entries == null)
                {
                    Debug.LogWarning("[MapManager] Response not ok or entries missing.");
                    return;
                }

                // Fill local state in the exact order returned
                foreach (var e in resp.entries)
                {
                    if (e == null) continue;
                    mapIds.Add(e.mapId ?? "");
                    difficulties.Add(e.difficultyTag);
                    mapJsonList.Add(e.json ?? "");
                }

                isReady = true;
                Debug.Log($"[MapManager] Ready. Loaded {mapJsonList.Count} maps.");
                // Auto-build a row of maps using the fetched JSONs
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

        private void BuildSequenceFromJsons()
        {
            if (mapPrefab == null)
            {
                Debug.LogError("[MapManager] mapPrefab is not assigned.");
                return;
            }
  
            var parent = mapsParent != null ? mapsParent : this.transform;
            if (clearBeforeBuild) ClearBuiltMaps();
  
            var pos = startPosition;
            int built = 0;
  
            // 0) Starter map (opsiyonel ama öncelikli)
            if (useStarterMap && !string.IsNullOrWhiteSpace(starterMapJson))
            {
                var inst = Instantiate(mapPrefab, pos, Quaternion.identity, parent);
                var map = inst.GetComponent<Map>();
  
                // İsim: "01_name"
                string mapName = null;
                try { mapName = JsonUtility.FromJson<MapNameDto>(starterMapJson)?.mapName; }
                catch { /* ignore parse errors */ }
                string fallback = string.IsNullOrWhiteSpace(starterMapDisplayName) ? "starter" : starterMapDisplayName;
                string safe = ToSafeName(string.IsNullOrWhiteSpace(mapName) ? fallback : mapName);
                inst.name = $"{(built + 1):00}_{safe}";
  
                if (map == null)
                {
                    Debug.LogError("[MapManager] mapPrefab has no Map component (starter).");
                    Destroy(inst);
                    return;
                }
  
                map.mapJson = starterMapJson;
                map.Initialize();
  
                pos += step;
                built++;
            }
  
            // 1) Fetched sequence
            for (int i = 0; i < mapJsonList.Count; i++)
            {
                var json = mapJsonList[i] ?? string.Empty;
                var inst = Instantiate(mapPrefab, pos, Quaternion.identity, parent);
                var map = inst.GetComponent<Map>();
  
                // İsim: "index_mapname" (starter varsa index starter'dan sonra devam eder)
                string mapName = null;
                try { mapName = JsonUtility.FromJson<MapNameDto>(json)?.mapName; }
                catch { /* ignore parse errors */ }
                string safe = ToSafeName(mapName);
                inst.name = $"{(built + 1):00}_{safe}";
  
                if (map == null)
                {
                    Debug.LogError("[MapManager] mapPrefab has no Map component.");
                    Destroy(inst);
                    return;
                }
  
                map.mapJson = json;
                map.Initialize(); // build immediately
  
                pos += step;
                built++;
            }
  
            Debug.Log($"[MapManager] Built {built} maps in sequence. (Starter first: {useStarterMap && !string.IsNullOrWhiteSpace(starterMapJson)})");
        }

        /// <summary>
        /// Destroys all built map instances and resets runtime state.
        /// Call this when the run ends to return to meta scene cleanly.
        /// </summary>
        public void DeinitializeMap()
        {
            // Remove any instantiated maps from hierarchy
            ClearBuiltMaps();
  
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
