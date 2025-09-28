
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

        // Odin button to trigger fetching manually (for testing in Editor)
        [Button("Initialize & Fetch"), GUIColor(0.2f, 0.7f, 1f)]
        private void OdinInitializeButton()
        {
            _ = Initialize();
        }

        /// <summary>
        /// Initializes by requesting sequenced maps from RemoteAppDataService.
        /// RemoteAppDataService is responsible for Firebase/Auth/Functions.
        /// </summary>
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapManager] Initialize error: {ex.Message}\n{ex}");
            }
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
