using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Functions;

namespace MapDesignerTool
{
    /// <summary>
    /// Map Browser Panel for listing, editing, and deleting maps.
    /// </summary>
    public class MapBrowserPanel : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject panelRoot;
        public Button closeButton;
        public Button refreshButton;

        [Header("Chapter List")]
        public Transform chapterListParent;
        public GameObject chapterItemPrefab;

        [Header("Endless List")]
        public Transform endlessListParent;
        public GameObject endlessItemPrefab;

        [Header("Delete Confirmation Dialog")]
        public GameObject confirmDialog;
        public TextMeshProUGUI confirmMessage;
        public Button confirmYesButton;
        public Button confirmNoButton;

        [Header("Load Confirmation Dialog")]
        public GameObject loadConfirmDialog;
        public Button loadConfirmYesButton;
        public Button loadConfirmNoButton;

        [Header("Loading")]
        public GameObject loadingIndicator;

        [Header("References")]
        public MapEditorBuildMenu buildMenu;

        // Data
        private List<MapListItem> _chapters = new();
        private List<MapListItem> _endless = new();

        // Pending delete
        private string _pendingDeleteType;
        private string _pendingDeleteId;

        // Pending load
        private string _pendingLoadType;
        private string _pendingLoadId;

        [Serializable]
        public class MapListItem
        {
            public string mapId;
            public string displayName;
            public int order;      // for chapters
            public int difficulty; // for endless
            public string createdAt;
        }

        private bool _initialized = false;

        void Awake()
        {
            Initialize();
        }

        void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (confirmDialog) confirmDialog.SetActive(false);
            if (loadConfirmDialog) loadConfirmDialog.SetActive(false);

            if (closeButton) closeButton.onClick.AddListener(Close);
            if (refreshButton) refreshButton.onClick.AddListener(() => StartCoroutine(RefreshCoroutine()));
            if (confirmYesButton) confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (confirmNoButton) confirmNoButton.onClick.AddListener(() => confirmDialog.SetActive(false));
            if (loadConfirmYesButton) loadConfirmYesButton.onClick.AddListener(OnLoadConfirmYes);
            if (loadConfirmNoButton) loadConfirmNoButton.onClick.AddListener(() => loadConfirmDialog.SetActive(false));
        }

        public void Open()
        {
            Initialize(); // Ensure initialized even if Awake didn't run
            gameObject.SetActive(true);
            if (panelRoot && panelRoot != gameObject) panelRoot.SetActive(true);
        }

        /// <summary>
        /// Called from external active MonoBehaviour to refresh after opening
        /// </summary>
        public void RefreshList()
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(RefreshCoroutine());
        }

        public void Close()
        {
            if (panelRoot) panelRoot.SetActive(false);
        }

        IEnumerator RefreshCoroutine()
        {
            if (loadingIndicator) loadingIndicator.SetActive(true);

            var task = FetchMapsAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (loadingIndicator) loadingIndicator.SetActive(false);

            PopulateChapterList();
            PopulateEndlessList();
        }

        async System.Threading.Tasks.Task FetchMapsAsync()
        {
            _chapters.Clear();
            _endless.Clear();

            try
            {
                var func = FirebaseFunctions.DefaultInstance.GetHttpsCallable("listMaps");
                var result = await func.CallAsync();

                if (result?.Data is IDictionary root)
                {
                    if (root["chapters"] is IList chapterArr)
                    {
                        foreach (var item in chapterArr)
                        {
                            if (item is IDictionary d)
                            {
                                _chapters.Add(new MapListItem
                                {
                                    mapId = d.TryGetString("mapId"),
                                    displayName = d.TryGetString("displayName"),
                                    order = d.TryGetInt("order"),
                                    createdAt = d.TryGetString("createdAt")
                                });
                            }
                        }
                    }

                    if (root["endless"] is IList endlessArr)
                    {
                        foreach (var item in endlessArr)
                        {
                            if (item is IDictionary d)
                            {
                                _endless.Add(new MapListItem
                                {
                                    mapId = d.TryGetString("mapId"),
                                    displayName = d.TryGetString("displayName"),
                                    difficulty = d.TryGetInt("difficulty"),
                                    createdAt = d.TryGetString("createdAt")
                                });
                            }
                        }
                    }
                }

                Debug.Log($"[MapBrowser] Loaded {_chapters.Count} chapters, {_endless.Count} endless");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapBrowser] FetchMapsAsync error: {e.Message}");
            }
        }

        void PopulateChapterList()
        {
            // Clear
            foreach (Transform child in chapterListParent)
                Destroy(child.gameObject);

            foreach (var ch in _chapters)
            {
                var go = Instantiate(chapterItemPrefab, chapterListParent);
                go.SetActive(true);

                var item = go.GetComponent<MapBrowserItem>();
                if (item != null)
                {
                    item.SetupChapter(ch.mapId, ch.displayName, ch.order);
                    item.OnEditClicked += OnEditClicked;
                    item.OnDeleteClicked += OnDeleteClicked;
                }
                else
                {
                    // Fallback: Find by name (legacy)
                    var nameText = go.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                    if (nameText) nameText.text = $"{ch.displayName} (Order: {ch.order})";

                    var editBtn = go.transform.Find("EditButton")?.GetComponent<Button>();
                    if (editBtn)
                    {
                        string mapId = ch.mapId;
                        editBtn.onClick.AddListener(() => OnEditClicked("chapter", mapId));
                    }

                    var deleteBtn = go.transform.Find("DeleteButton")?.GetComponent<Button>();
                    if (deleteBtn)
                    {
                        string mapId = ch.mapId;
                        string name = ch.displayName;
                        deleteBtn.onClick.AddListener(() => OnDeleteClicked("chapter", mapId, name));
                    }
                }
            }
        }

        void PopulateEndlessList()
        {
            foreach (Transform child in endlessListParent)
                Destroy(child.gameObject);

            foreach (var en in _endless)
            {
                var go = Instantiate(endlessItemPrefab, endlessListParent);
                go.SetActive(true);

                var item = go.GetComponent<MapBrowserItem>();
                if (item != null)
                {
                    item.SetupEndless(en.mapId, en.displayName, en.difficulty);
                    item.OnEditClicked += OnEditClicked;
                    item.OnDeleteClicked += OnDeleteClicked;
                }
                else
                {
                    // Fallback: Find by name (legacy)
                    var nameText = go.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                    if (nameText) nameText.text = $"{en.displayName} (Diff: {en.difficulty})";

                    var editBtn = go.transform.Find("EditButton")?.GetComponent<Button>();
                    if (editBtn)
                    {
                        string mapId = en.mapId;
                        editBtn.onClick.AddListener(() => OnEditClicked("endless", mapId));
                    }

                    var deleteBtn = go.transform.Find("DeleteButton")?.GetComponent<Button>();
                    if (deleteBtn)
                    {
                        string mapId = en.mapId;
                        string name = en.displayName;
                        deleteBtn.onClick.AddListener(() => OnDeleteClicked("endless", mapId, name));
                    }
                }
            }
        }

        void OnEditClicked(string mapType, string mapId)
        {
            // Show confirmation dialog before loading
            _pendingLoadType = mapType;
            _pendingLoadId = mapId;

            if (loadConfirmDialog) loadConfirmDialog.SetActive(true);
        }

        void OnLoadConfirmYes()
        {
            if (loadConfirmDialog) loadConfirmDialog.SetActive(false);
            StartCoroutine(LoadMapForEditCoroutine(_pendingLoadType, _pendingLoadId));
        }

        IEnumerator LoadMapForEditCoroutine(string mapType, string mapId)
        {
            if (loadingIndicator) loadingIndicator.SetActive(true);

            var task = LoadMapForEditAsync(mapType, mapId);
            yield return new WaitUntil(() => task.IsCompleted);

            if (loadingIndicator) loadingIndicator.SetActive(false);

            if (task.Result != null)
            {
                Close();
                // Load into scene
                if (buildMenu) buildMenu.LoadMapIntoScene(task.Result);
            }
        }

        async System.Threading.Tasks.Task<LoadedMapData> LoadMapForEditAsync(string mapType, string mapId)
        {
            try
            {
                var func = FirebaseFunctions.DefaultInstance.GetHttpsCallable("getMapForEdit");
                var payload = new Dictionary<string, object>
                {
                    { "mapType", mapType },
                    { "mapId", mapId }
                };

                var result = await func.CallAsync(payload);
                if (result?.Data is IDictionary d)
                {
                    if (!d.TryGetBool("ok"))
                    {
                        Debug.LogError($"[MapBrowser] getMapForEdit failed: {d.TryGetString("message")}");
                        return null;
                    }

                    return new LoadedMapData
                    {
                        mapId = d.TryGetString("mapId"),
                        mapType = d.TryGetString("mapType"),
                        mapName = d.TryGetString("mapName"),
                        mapDisplayName = d.TryGetString("mapDisplayName"),
                        mapOrder = d.TryGetInt("mapOrder"),
                        mapLength = d.TryGetInt("mapLength"),
                        difficultyTag = d.TryGetInt("difficultyTag"),
                        json = d.TryGetString("json")
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapBrowser] LoadMapForEditAsync error: {e.Message}");
            }
            return null;
        }

        void OnDeleteClicked(string mapType, string mapId, string displayName)
        {
            _pendingDeleteType = mapType;
            _pendingDeleteId = mapId;

            if (confirmMessage)
                confirmMessage.text = $"Are you sure you want to delete '{displayName}'?\n\nThis cannot be undone.";

            if (confirmDialog) confirmDialog.SetActive(true);
        }

        void OnConfirmYes()
        {
            if (confirmDialog) confirmDialog.SetActive(false);
            StartCoroutine(DeleteMapCoroutine(_pendingDeleteType, _pendingDeleteId));
        }

        IEnumerator DeleteMapCoroutine(string mapType, string mapId)
        {
            if (loadingIndicator) loadingIndicator.SetActive(true);

            var task = DeleteMapAsync(mapType, mapId);
            yield return new WaitUntil(() => task.IsCompleted);

            if (loadingIndicator) loadingIndicator.SetActive(false);

            // Refresh list
            StartCoroutine(RefreshCoroutine());
        }

        async System.Threading.Tasks.Task DeleteMapAsync(string mapType, string mapId)
        {
            try
            {
                var func = FirebaseFunctions.DefaultInstance.GetHttpsCallable("deleteMap");
                var payload = new Dictionary<string, object>
                {
                    { "mapType", mapType },
                    { "mapId", mapId }
                };

                var result = await func.CallAsync(payload);
                if (result?.Data is IDictionary d && d.TryGetBool("ok"))
                {
                    Debug.Log($"[MapBrowser] Deleted {mapType}/{mapId}");
                }
                else
                {
                    Debug.LogError($"[MapBrowser] Delete failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapBrowser] DeleteMapAsync error: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Data returned from getMapForEdit for loading into scene
    /// </summary>
    [Serializable]
    public class LoadedMapData
    {
        public string mapId;
        public string mapType;
        public string mapName;
        public string mapDisplayName;
        public int mapOrder;
        public int mapLength;
        public int difficultyTag;
        public string json;
    }

    // Helper extensions (matching UserDatabaseManager)
    public static class MapBrowserDictHelpers
    {
        public static string TryGetString(this IDictionary d, string key, string def = "")
        {
            if (d != null && d.Contains(key) && d[key] != null) return d[key].ToString();
            return def;
        }
        public static int TryGetInt(this IDictionary d, string key, int def = 0)
        {
            if (d != null && d.Contains(key) && d[key] != null)
            {
                var v = d[key];
                if (v is int i) return i;
                if (v is long l) return (int)l;
                if (v is double db) return (int)db;
                if (int.TryParse(v.ToString(), out var p)) return p;
            }
            return def;
        }
        public static bool TryGetBool(this IDictionary d, string key, bool def = false)
        {
            if (d != null && d.Contains(key) && d[key] != null)
            {
                var v = d[key];
                if (v is bool b) return b;
                if (bool.TryParse(v.ToString(), out var p)) return p;
            }
            return def;
        }
    }
}
