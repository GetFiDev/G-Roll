using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using System.Globalization;
using TMPro;

namespace MapDesignerTool
{
    [System.Serializable]
    public struct TabButtonConfig
    {
        public Button button;
        public GameObject contentPanel; // The panel that slides up
    }

    public class MapEditorBuildMenu : MonoBehaviour
    {
        [Header("Refs")]
        public RectTransform slidingPanel; // The main sliding container
        public Button toggleButton;        // The handle button text-only
        public RectTransform toggleIcon;   // Optional arrow icon

        [Header("Sliding Configuration")]
        public float panelClosedY = -400f;  // Default hidden
        public float panelOpenY = 0f;       // Default visible
        public float animDuration = 0.3f;

        [Header("Global Tab Sprites")]
        public Sprite commonActiveSprite;
        public Sprite commonInactiveSprite;

        [Header("Tabs")]
        public TabButtonConfig placeTab;
        public TabButtonConfig configTab;
        public TabButtonConfig saveTab;

        [Header("Save Tab UI")]
        public GameObject saveTabPanel;     // Panel containing the entire Save UI
        public TMP_Dropdown modeDropdown;   // 0=Endless, 1=Chapter
        public GameObject endlessPanel;
        public GameObject chapterPanel;
        
        // Endless Inputs
        public TMP_InputField mapNameInput;
        public TMP_Dropdown difficultyDropdown; // Very Easy..Hard

        // Chapter Inputs
        public TMP_InputField orderInput;
        public Button lengthPlusBtn;
        public Button lengthMinusBtn;
        public TextMeshProUGUI lengthDisplay;   // Optional: Show current map length

        // Shared
        public Button saveButton;

        // Overwrite Dialog
        public GameObject overwriteDialog;
        public TextMeshProUGUI overwriteMessage;
        public Button confirmOverwriteBtn;
        public Button cancelOverwriteBtn;
        
        // Save Toast
        public TextMeshProUGUI saveToastText;
        private Coroutine toastCoroutine;

        private TabButtonConfig currentTab;

        [Header("Place Tab Population")]
        public BuildDatabase database;
        public DraggableBuildItem itemTemplate;
        public Transform itemsContainer;
        public GridPlacer gridPlacer;
        public OrbitCamera orbitCam;
        public Canvas mainCanvas; 

        [Header("Config Tab Population")]
        public Transform configContainer;
        public ConfigItemUI configSliderPrefab;
        public ConfigItemUI configTogglePrefab;
        public ConfigItemUI configInputPrefab; // For Text Input
        
        [Header("Config Special Prefabs")]
        public ConfigItemUI configTransformPrefab; // For standard Move/Rotate controls

        [Header("UI Feedback")]
        public TextMeshProUGUI selectedObjectNameText; // Displays selected object name

        [Header("Map Browser")]
        public Button mapBrowserButton;    // Button to open the map browser
        public MapBrowserPanel mapBrowserPanel; // Reference to the browser panel

        private bool isOpen = false;
        private GameObject currentSelectedObject; // Track selection

        // Loaded map context for editing existing maps
        private LoadedMapData _loadedMapContext = null;
        public bool IsEditingExistingMap => _loadedMapContext != null;
        
        // Prevent double-click on save button
        private bool isSaving = false;

        void Start()
        {
            if (toggleButton) toggleButton.onClick.AddListener(OnToggleClicked);
            if (mapBrowserButton) mapBrowserButton.onClick.AddListener(OnMapBrowserClicked);

            // Bind Tabs
            if (placeTab.button) placeTab.button.onClick.AddListener(() => OnTabClicked(placeTab));
            if (configTab.button) configTab.button.onClick.AddListener(() => OnTabClicked(configTab));
            if (saveTab.button) saveTab.button.onClick.AddListener(() => OnTabClicked(saveTab));

            // Initialize Save Tab
            if (modeDropdown) modeDropdown.onValueChanged.AddListener(OnSaveModeChanged);
            if (lengthPlusBtn) lengthPlusBtn.onClick.AddListener(() => ChangeMapLength(20));
            if (lengthMinusBtn) lengthMinusBtn.onClick.AddListener(() => ChangeMapLength(-20));
            if (saveButton) saveButton.onClick.AddListener(() => StartSaveProcess(false));
            
            // Overwrite
            if (overwriteDialog) overwriteDialog.SetActive(false);
            if (confirmOverwriteBtn) confirmOverwriteBtn.onClick.AddListener(() => StartSaveProcess(true));
            if (cancelOverwriteBtn) cancelOverwriteBtn.onClick.AddListener(() => overwriteDialog.SetActive(false));

            OnSaveModeChanged(0); // Default to Endless

            // Listen for GridPlacer Selection
            if (gridPlacer != null)
                gridPlacer.OnObjectSelected += OnObjectSelectionChanged;

            // Setup Items for Place Tab
            PopulatePlaceItems();

            // 1. Force State OFF
            isOpen = false;

            // 2. Force Position immediately (Kill any tween that might exist)
            if (slidingPanel)
            {
                slidingPanel.DOKill();
                var pos = slidingPanel.anchoredPosition;
                pos.y = panelClosedY;
                slidingPanel.anchoredPosition = pos;
            }

            // 3. Select Place tab (setup visuals)
            SelectTab(placeTab);
        }

        void OnDestroy()
        {
            if (gridPlacer != null)
                gridPlacer.OnObjectSelected -= OnObjectSelectionChanged;
        }

        // === MAP BROWSER ===

        void OnMapBrowserClicked()
        {
            if (mapBrowserPanel != null)
            {
                mapBrowserPanel.Open();
                // Start refresh from here since we're active
                StartCoroutine(RefreshBrowserNextFrame());
            }
        }

        System.Collections.IEnumerator RefreshBrowserNextFrame()
        {
            yield return null; // Wait one frame for panel to activate
            if (mapBrowserPanel != null)
                mapBrowserPanel.RefreshList();
        }

        // === SAVE TAB LOGIC ===

        public void OnSaveModeChanged(int modeIndex)
        {
            bool isEndless = (modeIndex == 0);
            if (endlessPanel) endlessPanel.SetActive(isEndless);
            if (chapterPanel) chapterPanel.SetActive(!isEndless);

            // Lock endless mode to 1 chunk (z=120)
            if (isEndless)
            {
                var grid = FindObjectOfType<MapGridCellUtility>();
                if (grid)
                {
                    grid.SetMapLength(MapGridCellUtility.GRID_CELLS_PER_CHUNK);
                }
            }

            UpdateLengthDisplay();
        }

        public void ChangeMapLength(int delta)
        {
            var grid = FindObjectOfType<MapGridCellUtility>(); 
            if (grid) 
            {
                grid.UpdateMapLength(delta);
                UpdateLengthDisplay();
            }
        }

        void UpdateLengthDisplay()
        {
            var grid = FindObjectOfType<MapGridCellUtility>();
            if (grid && lengthDisplay)
            {
                lengthDisplay.text = grid.zCells.ToString(); // Raw number only
            }
        }

        async void StartSaveProcess(bool force)
        {
            // Prevent double-click
            if (isSaving) return;
            isSaving = true;
            
            // Disable save button while saving
            if (saveButton) saveButton.interactable = false;
            
            if (overwriteDialog) overwriteDialog.SetActive(false);

            string mapName = "";
            string displayName = "Untitled";
            string type = (modeDropdown.value == 0) ? "endless" : "chapter";
            int difficulty = 1;
            int order = 1;
            
            var grid = FindObjectOfType<MapGridCellUtility>();
            int length = 120; // Default for Endless

            // If editing existing map, use loaded context
            if (_loadedMapContext != null && force)
            {
                // Use original map ID for overwrite
                mapName = _loadedMapContext.mapId;
                displayName = _loadedMapContext.mapDisplayName;
                type = _loadedMapContext.mapType;
                difficulty = _loadedMapContext.difficultyTag;
                order = _loadedMapContext.mapOrder;
                length = (grid != null) ? grid.zCells : _loadedMapContext.mapLength;
                
                // Force is already true, proceed to save
            }

            // Gather Inputs
            if (type == "endless")
            {
                // Endless: Fixed length of 120
                length = 120;
                mapName = mapNameInput ? mapNameInput.text : "EndlessMap";
                mapName = SanitizeForId(mapName); 
                displayName = mapName;
                difficulty = (difficultyDropdown ? difficultyDropdown.value : 0) + 1; // 0->1, 1->2...
            }
            else
            {
                // Chapter: Variable length from grid
                length = (grid != null) ? grid.zCells : 100;
                if (orderInput && int.TryParse(orderInput.text, out int ord)) order = ord;
                mapName = $"Chapter_{order}";
                displayName = $"Chapter {order}";
                
                // FinishGate Validation
                bool hasFinishGate = false;
                var placedItems = FindObjectsOfType<PlacedItemData>();
                foreach (var item in placedItems)
                {
                    if (item == null || item.item == null) continue;
                    if (item.item.name.Contains("FinishGate") || item.item.displayName.Contains("FinishGate"))
                    {
                        hasFinishGate = true;
                        break;
                    }
                }
                
                if (!hasFinishGate)
                {
                    Debug.LogError("Cannot save Chapter: At least one Finish Gate is required!");
                    isSaving = false;
                    if (saveButton) saveButton.interactable = true;
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(mapName))
            {
                Debug.LogError("Invalid Map Name");
                isSaving = false;
                if (saveButton) saveButton.interactable = true;
                return;
            }

            // Collect Data
            var saver = FindObjectOfType<MapSaver>();
            if (!saver)
            {
                Debug.LogError("No MapSaver found!");
                isSaving = false;
                if (saveButton) saveButton.interactable = true;
                return;
            }
            
            var data = saver.Collect(mapName, displayName, type, order, difficulty, length, 0);

            // Show saving toast
            string typeLabel = type == "endless" ? "Endless" : "Chapter";
            ShowToast($"Saving {typeLabel}: {displayName}...", Color.yellow, 0f); // 0 = stay until changed

            // Upload
            string status = await saver.SaveMapToCloud(data, force);

            if (status == "success")
            {
                Debug.Log("Map Saved Successfully!");
                ShowToast($"Map Saved: {typeLabel} - {displayName}", Color.green, 3f);
            }
            else if (status == "exists")
            {
                // Hide toast, show overwrite dialog
                HideToast();
                
                // Show Overwrite Dialog with type-specific message
                if (overwriteDialog)
                {
                    overwriteDialog.SetActive(true);
                    if (overwriteMessage)
                    {
                        if (type == "endless")
                        {
                            overwriteMessage.text = $"An endless map named '{mapName}' already exists.\nDo you want to overwrite it?";
                        }
                        else
                        {
                            overwriteMessage.text = $"A chapter with order {order} already exists.\nDo you want to overwrite it?";
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("Save Failed.");
                ShowToast("Save Failed!", Color.red, 3f);
            }
            
            // Re-enable save button
            isSaving = false;
            if (saveButton) saveButton.interactable = true;
        }

        static string SanitizeForId(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Untitled";
            foreach (var ch in new[]{'/', '\\', '#', '?', '%', '[',']',':','*','\"', ' '}) // Replace spaces too for pure ID
                s = s.Replace(ch.ToString(), "_");
            return s.Trim();
        }
        
        /// <summary>
        /// Shows a toast message with the specified color.
        /// If duration is 0, the toast stays until manually hidden.
        /// </summary>
        void ShowToast(string message, Color color, float duration)
        {
            if (saveToastText == null) return;
            
            // Cancel any existing auto-hide
            if (toastCoroutine != null)
            {
                StopCoroutine(toastCoroutine);
                toastCoroutine = null;
            }
            
            saveToastText.text = message;
            saveToastText.color = color;
            saveToastText.gameObject.SetActive(true);
            
            if (duration > 0f)
            {
                toastCoroutine = StartCoroutine(HideToastAfter(duration));
            }
        }
        
        void HideToast()
        {
            if (toastCoroutine != null)
            {
                StopCoroutine(toastCoroutine);
                toastCoroutine = null;
            }
            
            if (saveToastText != null)
            {
                saveToastText.text = "";
                saveToastText.gameObject.SetActive(false);
            }
        }
        
        System.Collections.IEnumerator HideToastAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideToast();
        }

        void OnMove(GameObject target, int dx, int dz)
        {
            if (target != null && gridPlacer != null)
            {
                gridPlacer.TryMoveObject(target, dx, dz);
            }
        }

        void OnRotate(GameObject target)
        {
            if (target != null && gridPlacer != null)
            {
                gridPlacer.TryRotateObject(target);
            }
        }

        void PopulatePlaceItems()
        {
            if (!itemTemplate || !itemsContainer || !database) return;

            itemTemplate.gameObject.SetActive(false);

            // Cleanup old
            foreach (Transform child in itemsContainer)
            {
                if (child.gameObject != itemTemplate.gameObject)
                    Destroy(child.gameObject);
            }

            foreach (var item in database.items)
            {
                var go = Instantiate(itemTemplate, itemsContainer);
                go.gameObject.SetActive(true);
                go.Init(item, gridPlacer, orbitCam, mainCanvas);
            }
        }

        void OnObjectSelectionChanged(GameObject selectedObj)
        {
            currentSelectedObject = selectedObj;
            
            // Update Name Display
            if (selectedObjectNameText != null && selectedObj != null)
            {
                string cleanName = selectedObj.name.Replace("(Clone)", "").Trim();
                selectedObjectNameText.text = cleanName;
            }
            
            // Generate Config UI
            GenerateConfigUI(selectedObj);

            // Auto open Config tab
            if (configTab.button != null)
            {
                SelectTab(configTab);
                if (!isOpen) SetOpenState(true);
            }
        }

        void GenerateConfigUI(GameObject target)
        {
            if (configContainer == null) return;

            // Clear old UI
            foreach (Transform child in configContainer)
            {
                Destroy(child.gameObject);
            }

            if (target == null) return;

            // 1. Special Transform Controls (Always show for any placed item)
            // We assume if it has PlacedItemData, it is movable/rotatable
            var dataInfo = target.GetComponent<PlacedItemData>();
            
            if (dataInfo != null && configTransformPrefab)
            {
                var transUI = Instantiate(configTransformPrefab, configContainer);
                transUI.gameObject.SetActive(true);

                // Bind Buttons directly to this specific target
                if (transUI.moveXPos) transUI.moveXPos.onClick.AddListener(() => OnMove(target, 1, 0));
                if (transUI.moveXNeg) transUI.moveXNeg.onClick.AddListener(() => OnMove(target, -1, 0));
                if (transUI.moveZPos) transUI.moveZPos.onClick.AddListener(() => OnMove(target, 0, 1));
                if (transUI.moveZNeg) transUI.moveZNeg.onClick.AddListener(() => OnMove(target, 0, -1));
                if (transUI.rotateBtn) transUI.rotateBtn.onClick.AddListener(() => OnRotate(target));
            }

            // 2. Regular IMapConfigurable
            var configurable = target.GetComponentInChildren<IMapConfigurable>();
            if (configurable == null) return; 

            // Loop definitions
            var defs = configurable.GetConfigDefinitions();
            foreach (var def in defs)
            {
                if (def.type == ConfigType.Float && configSliderPrefab)
                {
                    var ui = Instantiate(configSliderPrefab, configContainer);
                    ui.gameObject.SetActive(true);
                    if (ui.labelText) ui.labelText.text = def.displayName;
                    
                    float currentVal = def.defaultValue;
                    
                    // Check saved value
                    if (dataInfo != null && dataInfo.runtimeConfig.TryGetValue(def.key, out var savedStr))
                    {
                        if (float.TryParse(savedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                            currentVal = f;
                    }
                    
                    if (ui.slider)
                    {
                        ui.slider.minValue = def.min;
                        ui.slider.maxValue = def.max;
                        ui.slider.value = currentVal;

                        // Update value text
                        if (ui.valueText) ui.valueText.text = currentVal.ToString("F1");

                        // Bind Listener
                        ui.slider.onValueChanged.AddListener((rawVal) =>
                        {
                            // Enforce 0.1 step
                            float v = Mathf.Round(rawVal * 10f) / 10f;
                            
                            // Only update if value actually changed after snap (prevents infinite loop if setting slider.value)
                            if (Mathf.Abs(ui.slider.value - v) > 0.001f)
                            {
                                ui.slider.value = v;
                                return; // Will trigger listener again with clean value
                            }

                            if (ui.valueText) ui.valueText.text = v.ToString("F1"); // Display cleanly
                            
                            // 1. Update Runtime Dict
                            if (dataInfo)
                            {
                                dataInfo.runtimeConfig[def.key] = v.ToString(CultureInfo.InvariantCulture);
                                dataInfo.SaveConfigFromRuntime(); // Sync to list for serialization
                            }

                            // 2. Apply Live
                            var dict = new Dictionary<string, string> {{def.key, v.ToString(CultureInfo.InvariantCulture)}};
                            configurable.ApplyConfig(dict);
                        });
                    }
                    
                    // Initial Apply (to ensure sync)
                     var initDict = new Dictionary<string, string> {{def.key, currentVal.ToString(CultureInfo.InvariantCulture)}};
                     configurable.ApplyConfig(initDict);
                }
                else if (def.type == ConfigType.Bool && configTogglePrefab)
                {
                    var ui = Instantiate(configTogglePrefab, configContainer);
                    ui.gameObject.SetActive(true);
                    if (ui.labelText) ui.labelText.text = def.displayName;
                    
                    bool currentVal = def.defaultBool;

                    // Check saved value
                    if (dataInfo != null && dataInfo.runtimeConfig.TryGetValue(def.key, out var savedStr))
                    {
                         bool.TryParse(savedStr, out currentVal);
                    }

                    if (ui.toggle)
                    {
                        ui.toggle.isOn = currentVal;
                        
                        // Bind Listener
                        ui.toggle.onValueChanged.AddListener((v) =>
                        {
                             // 1. Update Runtime Dict
                            if (dataInfo)
                            {
                                dataInfo.runtimeConfig[def.key] = v.ToString();
                                dataInfo.SaveConfigFromRuntime();
                            }

                            // 2. Apply Live
                            var dict = new Dictionary<string, string> {{def.key, v.ToString()}};
                            configurable.ApplyConfig(dict);
                        });
                    }
                    
                    // Initial Apply
                    var initDict = new Dictionary<string, string> {{def.key, currentVal.ToString()}};
                    configurable.ApplyConfig(initDict);
                }
                else if (def.type == ConfigType.FloatInput && configInputPrefab)
                {
                    var ui = Instantiate(configInputPrefab, configContainer);
                    ui.gameObject.SetActive(true);
                    if (ui.labelText) ui.labelText.text = def.displayName;
                    
                    float currentVal = def.defaultValue;

                    // Check saved value
                    if (dataInfo != null && dataInfo.runtimeConfig.TryGetValue(def.key, out var savedStr))
                    {
                        if (float.TryParse(savedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                            currentVal = f;
                    }

                    if (ui.inputField)
                    {
                        ui.inputField.text = currentVal.ToString(CultureInfo.InvariantCulture);
                        ui.inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
                        
                        // Changed to onValueChanged for instant feedback
                        ui.inputField.onValueChanged.AddListener((val) =>
                        {
                            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                            {
                                // 1. Update Runtime Dict
                                if (dataInfo)
                                {
                                    dataInfo.runtimeConfig[def.key] = parsed.ToString(CultureInfo.InvariantCulture);
                                    dataInfo.SaveConfigFromRuntime();
                                }

                                // 2. Apply Live
                                var dict = new Dictionary<string, string> {{def.key, parsed.ToString(CultureInfo.InvariantCulture)}};
                                configurable.ApplyConfig(dict);
                            }
                            // Do nothing on invalid input (allow user to continue typing)
                        });
                    }
                    
                    // Initial Apply
                    var initDict = new Dictionary<string, string> {{def.key, currentVal.ToString(CultureInfo.InvariantCulture)}};
                    configurable.ApplyConfig(initDict);
                }
            }
        }

        void OnToggleClicked()
        {
            // If opening, force selection of Place tab
            if (!isOpen)
            {
                SelectTab(placeTab);
            }
            
            SetOpenState(!isOpen);
        }

        void OnTabClicked(TabButtonConfig tab)
        {
            // Strictly switch tabs. Do not animate panel.
            bool isSameTab = (currentTab.button == tab.button);

            if (!isSameTab)
            {
                SelectTab(tab);
            }
        }

        void SelectTab(TabButtonConfig tab)
        {
            currentTab = tab;

            // Update ALL Visuals
            // We compare buttons to check identity
            if (placeTab.button) UpdateTabVisual(placeTab, placeTab.button == currentTab.button);
            if (configTab.button) UpdateTabVisual(configTab, configTab.button == currentTab.button);
            if (saveTab.button) UpdateTabVisual(saveTab, saveTab.button == currentTab.button);

            // Show proper content panel
            if (placeTab.contentPanel) placeTab.contentPanel.SetActive(placeTab.button == currentTab.button);
            if (configTab.contentPanel) configTab.contentPanel.SetActive(configTab.button == currentTab.button);
            if (saveTab.contentPanel) saveTab.contentPanel.SetActive(saveTab.button == currentTab.button);
        }

        void UpdateTabVisual(TabButtonConfig tab, bool isActive)
        {
            if (tab.button == null) return;
            var img = tab.button.GetComponent<Image>();
            if (img)
            {
                if (isActive && commonActiveSprite != null) img.sprite = commonActiveSprite;
                else if (!isActive && commonInactiveSprite != null) img.sprite = commonInactiveSprite;
            }
        }

        public void SetOpenState(bool open)
        {
            isOpen = open;

            // Animate Y position
            if (slidingPanel)
            {
                slidingPanel.DOKill();
                slidingPanel.DOAnchorPosY(isOpen ? panelOpenY : panelClosedY, animDuration).SetEase(Ease.OutQuad);
            }

            // Optional: Rotate toggle icon if exists
            if (toggleIcon)
            {
                toggleIcon.DOKill();
                toggleIcon.DORotate(new Vector3(0, 0, isOpen ? 180 : 0), animDuration);
            }
        }

        // ===== MAP LOADING =====

        /// <summary>
        /// Clears all placed items from the scene
        /// </summary>
        public void ClearScene()
        {
            var allPlaced = FindObjectsOfType<PlacedItemData>();
            foreach (var item in allPlaced)
            {
                if (item != null && item.gameObject != null)
                    DestroyImmediate(item.gameObject);
            }

            if (gridPlacer != null)
            {
                gridPlacer.ClearSelection();
            }

            _loadedMapContext = null;
            Debug.Log("[MapEditorBuildMenu] Scene cleared.");
        }

        /// <summary>
        /// Loads a map into the scene for editing.
        /// Called by MapBrowserPanel when user clicks Edit.
        /// </summary>
        public void LoadMapIntoScene(LoadedMapData mapData)
        {
            if (mapData == null || string.IsNullOrEmpty(mapData.json))
            {
                Debug.LogError("[MapEditorBuildMenu] Cannot load: Invalid map data.");
                return;
            }

            // 1. Clear existing scene
            ClearScene();

            // 2. Store context for overwrite
            _loadedMapContext = mapData;

            // 3. Parse JSON
            MapSaveData saveData;
            try
            {
                saveData = JsonUtility.FromJson<MapSaveData>(mapData.json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MapEditorBuildMenu] JSON parse error: {e.Message}");
                return;
            }

            if (saveData == null || saveData.items == null)
            {
                Debug.LogError("[MapEditorBuildMenu] Parsed data is empty.");
                return;
            }

            // 4. Adjust grid size and spawn chunks
            var grid = FindObjectOfType<MapGridCellUtility>();
            if (grid != null && saveData.mapLength > 0)
            {
                grid.SetMapLength(saveData.mapLength); // This will spawn correct chunks
            }

            // 5. Spawn items
            int spawned = 0;
            foreach (var item in saveData.items)
            {
                var buildable = database.GetById(item.displayName);
                if (buildable == null)
                {
                    Debug.LogWarning($"[MapEditorBuildMenu] Buildable not found: {item.displayName}");
                    continue;
                }

                if (gridPlacer != null)
                {
                    // Place via GridPlacer
                    gridPlacer.PlaceItemDirectly(buildable, item.gridX, item.gridY, item.rotationIndex, item.linkedPortalId, item.linkedButtonDoorId, item.config);
                    spawned++;
                }
            }

            // 6. Update Save Tab UI with loaded values
            if (modeDropdown != null)
            {
                modeDropdown.value = (mapData.mapType == "endless") ? 0 : 1;
                OnSaveModeChanged(modeDropdown.value);
            }

            if (mapData.mapType == "endless")
            {
                if (mapNameInput) mapNameInput.text = mapData.mapDisplayName;
                if (difficultyDropdown) difficultyDropdown.value = Mathf.Clamp(mapData.difficultyTag - 1, 0, 3);
            }
            else
            {
                if (orderInput) orderInput.text = mapData.mapOrder.ToString();
            }

            if (lengthDisplay && grid) lengthDisplay.text = $"Length: {grid.zCells}";

            Debug.Log($"[MapEditorBuildMenu] Loaded '{mapData.mapDisplayName}': {spawned} items, type={mapData.mapType}");
        }

        /// <summary>
        /// Resets the loaded map context (for new map creation)
        /// </summary>
        public void ResetLoadedContext()
        {
            _loadedMapContext = null;
        }
    }
}
