using System.Collections;
using UnityEngine;

namespace RemoteApp
{
    public class MapLoaderJsonAdapter : MonoBehaviour, IMapLoader
    {
        [SerializeField] private MapManager mapManager;

        public bool IsReady { get; private set; }
        public event System.Action OnReady;

        private Coroutine initRoutine;

        private void Awake()
        {
            if (mapManager == null)
                mapManager = GetComponent<MapManager>();
        }

        public void Load()
        {
            if (initRoutine != null) StopCoroutine(initRoutine);
            initRoutine = StartCoroutine(Co_Load());
        }

        private IEnumerator Co_Load()
        {
            IsReady = false;

            void HandleReady()       { IsReady = true; OnReady?.Invoke(); }
            void HandleDeinitialized(){ IsReady = false; }

            mapManager.OnReady         += HandleReady;
            mapManager.OnDeinitialized += HandleDeinitialized;

            if (mapManager == null)
            {
                Debug.LogError("[MapLoaderJsonAdapter] MapManager ref missing!");
                yield break;
            }

            // Pass the current game mode to the map manager
            var mode = GameMode.Endless;
            if (GameplayManager.Instance != null)
            {
                mode = GameplayManager.Instance.CurrentMode;
            }

            var task = mapManager.Initialize(mode);
            yield return new WaitUntil(() => task.IsCompleted);
            
            IsReady = true;
        }

        public void Unload()
        {
            if (initRoutine != null)
            {
                StopCoroutine(initRoutine);
                initRoutine = null;
            }

            if (mapManager != null)
                mapManager.DeinitializeMap();

            IsReady = false;
        }
    }
}