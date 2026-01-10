using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // Added for UI check
using DG.Tweening;
using MapDesignerTool; // For IMapConfigurable
using ETouch = UnityEngine.InputSystem.EnhancedTouch;

[RequireComponent(typeof(MapGridCellUtility))]
public class GridPlacer : MonoBehaviour
{
    [Header("Refs")]
    public LayerMask groundMask = ~0;
    public BuildDatabase database;
    public Transform placedParent;
    public Material ghostMaterial;

    [Header("Selection")]
    public GameObject selectionParticlePrefab;
    private GameObject currentSelectionParticle;
    private GameObject _selectedObject;
    public GameObject SelectedObject 
    { 
        get => _selectedObject; 
        private set => _selectedObject = value; 
    }

    [Header("Portal Pairing (in-GridPlacer)")]
    public bool enablePortalPairing = true;
    public LayerMask portalMask = ~0;      // portal collider layer(s)
    public Color portalPairColor = new Color(0.3f, 1f, 0.8f, 0.9f);
    public float portalPairYOffset = 0.25f;

    [Header("Modes")]
    public BuildMode currentMode = BuildMode.Navigate;
    public BuildableItem currentItem;

    [Header("Animations")]
    public float placeScaleIn  = 0.12f;
    public float placeOvershoot= 1.06f;
    public float demolishOut   = 0.10f;

    MapGridCellUtility grid;
    Camera cam;

    GameObject ghost;
    Renderer[] ghostRenderers;

    bool[,] occupied;                     // [x,z]
    int w, h;

    // Portal pairing state
    public bool portalPairMode = false;      // toggle with P
    Teleport firstPortal = null;      // pending first portal
    LineRenderer pairLine = null;     // live preview line

    // Button-Door pairing state
    public bool buttonDoorPairMode = false;
    ButtonDoorLinkRuntime firstButtonDoor = null;
    LineRenderer buttonDoorPairLine = null;

    // Exposed read-only properties for HUD
    public bool PortalPairMode => portalPairMode;
    public bool HasPortalFirstSelection => firstPortal != null;
    public bool ButtonDoorPairMode => buttonDoorPairMode;
    public bool HasButtonDoorFirstSelection => firstButtonDoor != null;

    // EVENT for UI
    public event System.Action OnModeChanged;
    public event System.Action<GameObject> OnObjectSelected;

    struct Footprint { public int x, z; public Vector2Int size; public BuildableItem item; public int rotationIndex; }
    readonly Dictionary<GameObject, Footprint> placedIndex = new();

    void Awake()
    {
        grid = GetComponent<MapGridCellUtility>();
        cam  = Camera.main;
        w = grid.xCells; h = grid.zCells;
        occupied = new bool[w, h];

        // create line for portal pairing preview
        var lineGO = new GameObject("_PortalPairLine");
        lineGO.transform.SetParent(transform, false);
        pairLine = lineGO.AddComponent<LineRenderer>();
        pairLine.useWorldSpace = true;
        pairLine.enabled = false;
        pairLine.material = new Material(Shader.Find("Sprites/Default"));
        pairLine.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        pairLine.widthMultiplier = 0.03f;
        pairLine.positionCount = 2;
        pairLine.startColor = pairLine.endColor = portalPairColor;

        // create line for Button-Door pairing preview
        var bdLineGO = new GameObject("_ButtonDoorPairLine");
        bdLineGO.transform.SetParent(transform, false);
        buttonDoorPairLine = bdLineGO.AddComponent<LineRenderer>();
        buttonDoorPairLine.useWorldSpace = true;
        buttonDoorPairLine.enabled = false;
        buttonDoorPairLine.material = new Material(Shader.Find("Sprites/Default"));
        buttonDoorPairLine.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        buttonDoorPairLine.widthMultiplier = 0.03f;
        buttonDoorPairLine.positionCount = 2;
        buttonDoorPairLine.startColor = buttonDoorPairLine.endColor = new Color(1f, 0.5f, 0.2f, 0.9f);
    }

    void Update()
    {
        HandleHotkeys();

        switch (currentMode)
        {
            case BuildMode.Place:
                TickPlacement();
                break;
            case BuildMode.Demolish:
                TickDemolish();
                break;
            case BuildMode.Modify:
                TickModify();
                break;
            case BuildMode.Navigate:
            default:
                // no-op
                break;
        }

        if (portalPairMode)
            TickPortalPairing();
        else if (pairLine != null && pairLine.enabled)
            pairLine.enabled = false;

        if (buttonDoorPairMode)
            TickButtonDoorPairing();
        else if (buttonDoorPairLine != null && buttonDoorPairLine.enabled)
            buttonDoorPairLine.enabled = false;
    }

    void HandleHotkeys()
    {
        var kbd = Keyboard.current;
        if (kbd == null) return;

        // ESC — universal exit
        if (kbd.escapeKey.wasPressedThisFrame)
        {
            SetNavigateMode();
            return;
        }

        // X — enter Demolish mode
        if (kbd.xKey.wasPressedThisFrame)
        {
            SetDemolishMode();
            return;
        }

        // P — enter Portal Pairing mode
        if (kbd.pKey.wasPressedThisFrame)
        {
            if (!portalPairMode) TogglePortalPairMode();
            return;
        }
    }

    void OnEnable()
    {
        if (!ETouch.EnhancedTouchSupport.enabled)
            ETouch.EnhancedTouchSupport.Enable();
    }

    // --- HELPER: UI Check ---
    bool IsPointerOverUI()
    {
        // Check touch first
        var touches = ETouch.Touch.activeTouches;
        if (touches.Count > 0)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                if (EventSystem.current != null && 
                    EventSystem.current.IsPointerOverGameObject(touches[i].finger.index))
                    return true;
            }
            return false;
        }
        
        // Fallback to mouse
        if (EventSystem.current != null)
        {
            return EventSystem.current.IsPointerOverGameObject();
        }
        return false;
    }

    /// <summary>
    /// Returns true if a primary click/tap was started this frame.
    /// For touch: single finger tap began and ended quickly (tap gesture).
    /// For mouse: left button was pressed.
    /// Also outputs the screen position of the click/tap.
    /// </summary>
    bool WasScreenTapped(out Vector2 screenPos)
    {
        screenPos = Vector2.zero;
        
        // Check touch first
        var touches = ETouch.Touch.activeTouches;
        if (touches.Count == 1)
        {
            var touch = touches[0];
            // For a tap, we detect when the touch just began
            // We'll use 'Began' phase for immediate response (like wasPressedThisFrame)
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                screenPos = touch.screenPosition;
                return true;
            }
        }
        
        // Fallback to mouse
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Gets the current pointer position (touch or mouse).
    /// </summary>
    Vector2 GetCurrentPointerPosition()
    {
        var touches = ETouch.Touch.activeTouches;
        if (touches.Count > 0)
        {
            return touches[0].screenPosition;
        }
        
        var mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
        
        return Vector2.zero;
    }

    // --- PUBLIC API FOR UI ---

    public void SetNavigateMode()
    {
        CancelPlacement();
        ExitPortalPairing();
        ExitButtonDoorPairing();
        ClearSelection();
        
        currentMode = BuildMode.Navigate;
        NotifyModeChanged();
    }

    public void SetDemolishMode()
    {
        if (currentMode == BuildMode.Demolish) return;

        CancelPlacement();
        ExitPortalPairing();
        ExitButtonDoorPairing();
        ClearSelection();

        currentMode = BuildMode.Demolish;
        NotifyModeChanged();
    }

    public void SetModifyMode()
    {
        if (currentMode == BuildMode.Modify) return;

        CancelPlacement();
        ExitPortalPairing();
        ExitButtonDoorPairing();
        ClearSelection();

        currentMode = BuildMode.Modify;
        NotifyModeChanged();
    }

    public void TogglePortalPairMode()
    {
        if (portalPairMode)
        {
            ExitPortalPairing();
            // Fallback to navigate if we were just pairing
            if (currentMode != BuildMode.Place) 
                currentMode = BuildMode.Navigate;
        }
        else
        {
            // Enter pairing
            CancelPlacement(); // drop ghosts
            ClearSelection();
            currentMode = BuildMode.Navigate; 
            
            portalPairMode = true;
            firstPortal = null;
            if (pairLine) pairLine.enabled = false;
        }
        NotifyModeChanged();
    }

    void ExitPortalPairing()
    {
        portalPairMode = false;
        firstPortal = null;
        if (pairLine) pairLine.enabled = false;
    }

    public void ToggleButtonDoorPairMode()
    {
        if (buttonDoorPairMode)
        {
            ExitButtonDoorPairing();
            if (currentMode != BuildMode.Place)
                currentMode = BuildMode.Navigate;
        }
        else
        {
            CancelPlacement();
            ClearSelection();
            ExitPortalPairing(); // Exit portal mode if active
            currentMode = BuildMode.Navigate;
            
            buttonDoorPairMode = true;
            firstButtonDoor = null;
            if (buttonDoorPairLine) buttonDoorPairLine.enabled = false;
        }
        NotifyModeChanged();
    }

    void ExitButtonDoorPairing()
    {
        buttonDoorPairMode = false;
        firstButtonDoor = null;
        if (buttonDoorPairLine) buttonDoorPairLine.enabled = false;
    }

    void NotifyModeChanged()
    {
        OnModeChanged?.Invoke();
    }

    // PUBLIC API (UI butonu çağırır)
    public void BeginPlacement(BuildableItem item)
    {
        // If we were in portal pairing, exit it when a new item selection starts
        if (portalPairMode) ExitPortalPairing();
        
        ClearSelection(); // Clear modify selection if we start placing

        currentItem = item;
        currentMode = BuildMode.Place;
        NotifyModeChanged();

        if (ghost) Destroy(ghost);
        ghost = Instantiate(item.prefab);
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        foreach (var r in ghostRenderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
            r.materials = mats;
        }
        SetGhostAlpha(0.1f);
    }

    public void CancelPlacement()
    {
        if (ghost) Destroy(ghost);
        ghost = null;
        currentItem = null;
    }

    void SetGhostAlpha(float a)
    {
        if (ghostRenderers == null) return;
        foreach (var r in ghostRenderers)
        foreach (var m in r.materials)
            if (m.HasProperty("_Color"))
            { var c = m.color; c.a = a; m.color = c; }
    }

    // --------- Placement loop ---------
    void TickPlacement()
    {
        if (currentItem == null) { currentMode = BuildMode.Navigate; return; }

        if (!RayToGrid(out var world, out var gx, out var gz))
        { if (ghost) ghost.SetActive(false); return; }

        ghost.SetActive(true);

        // Standard 0-rotation check for placement
        if (!CanPlace(gx, gz, currentItem.size, out var clampedX, out var clampedZ))
        { gx = clampedX; gz = clampedZ; }

        var ok = CanPlace(gx, gz, currentItem.size, out _, out _);
        var center = grid.GetCellCenterWorld(gx, gz);
        ghost.transform.position = center;
        ghost.transform.rotation = transform.rotation;
        SetGhostAlpha(ok ? 0.25f : 0.15f);

        var mouse = Mouse.current;
        // Check UI block BEFORE processing click
        if (IsPointerOverUI()) return;

        if (ok && mouse.leftButton.wasPressedThisFrame)
        {
            Place(gx, gz, currentItem);
        }

        // Sağ tık da placement iptali için kullanılabilir (orbit ile çakışmasın diye sadece tıklama)
        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
            currentMode = BuildMode.Navigate;
        }
    }

    GameObject GetPlacedRootFromHit(Transform hitTransform)
    {
        if (hitTransform == null) return null;
        var t = hitTransform;
        // climb up until direct child of placedParent (the intended root for placed instances)
        while (t.parent != null && t.parent != placedParent)
            t = t.parent;
        // if we reached a direct child of placedParent, that's our placed root
        if (t != null && t.parent == placedParent)
            return t.gameObject;
        // fallback: if placedParent is null or hierarchy is different, return the highest we found
        return t != null ? t.gameObject : null;
    }

    // --------- Demolish loop ---------
    void TickDemolish()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // Check UI block
        if (IsPointerOverUI()) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            // placedParent altı bir şeye vur?
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 5000f))
            {
                var go = GetPlacedRootFromHit(hit.collider.transform);
                if (!go) return;

                // placed listemizde mi?
                if (placedIndex.TryGetValue(go, out var fp))
                {
                    // if this object is a portal, cleanly unpair before destroying
                    var tp = go.GetComponent<Teleport>();
                    if (tp == null) tp = go.GetComponentInChildren<Teleport>();
                    if (tp != null) Unpair(tp);

                    // animasyonlu yok et
                    go.transform.DOScale(0.0f, demolishOut).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        Destroy(go);
                    });

                    // Set occupation removed
                    // SetOccupation(fp.item, fp.x, fp.z, fp.size, fp.rotationIndex, false);

                    placedIndex.Remove(go);
                }
            }
        }
    }

    // --------- Modify loop ---------
    void TickModify()
    {
        if (cam == null) return;

        // Check UI block
        if (IsPointerOverUI()) return;

        if (WasScreenTapped(out var tapPos))
        {
            Ray ray = cam.ScreenPointToRay(tapPos);
            if (Physics.Raycast(ray, out var hit, 5000f))
            {
                // Did we hit an object?
                var go = GetPlacedRootFromHit(hit.collider.transform);
                if (go != null && placedIndex.ContainsKey(go))
                {
                    // Select it
                    SelectObject(go);
                }
                else
                {
                    ClearSelection();
                }
            }
            else
            {
                // Hit nothing, clear
                ClearSelection();
            }
        }
    }

    void SelectObject(GameObject go)
    {
        if (SelectedObject == go) return; // already selected

        SelectedObject = go;

        // Calculate visual center based on grid footprint
        Vector3 visualCenter = go.transform.position;
        if (placedIndex.TryGetValue(go, out var fp))
        {
            // Center is average of min cell and max cell world positions
            Vector3 p1 = grid.GetCellCenterWorld(fp.x, fp.z);
            Vector3 p2 = grid.GetCellCenterWorld(fp.x + fp.size.x - 1, fp.z + fp.size.y - 1);
            visualCenter = (p1 + p2) * 0.5f;
        }

        // Spawn particle
        if (currentSelectionParticle) Destroy(currentSelectionParticle);

        if (selectionParticlePrefab)
        {
            currentSelectionParticle = Instantiate(selectionParticlePrefab, visualCenter, Quaternion.identity);
            currentSelectionParticle.transform.SetParent(go.transform); // Follow target
        }

        // Camera Focus
        var orbitCam = cam.GetComponent<OrbitCamera>();
        if (orbitCam)
        {
            // Focus on calculated visual center, zoom to minDistance (max zoom in)
            orbitCam.FocusOn(visualCenter, orbitCam.minDistance, 0.25f);
        }

        OnObjectSelected?.Invoke(go);
    }

    public void ClearSelection()
    {
        SelectedObject = null;
        if (currentSelectionParticle) Destroy(currentSelectionParticle);
        currentSelectionParticle = null;
    }

    // --------- TRANSFORM OPERATIONS (Move/Rotate) ---------

    public bool TryMoveObject(GameObject go, int dx, int dz)
    {
        if (go == null || !placedIndex.TryGetValue(go, out var fp)) return false;

        // 1. Clear occupation REMOVED

        // 2. Check destination
        int nx = fp.x + dx;
        int nz = fp.z + dz;

        // We use current rotation index to determine occupancy check
        if (CanPlace(nx, nz, fp.size, out _, out _))
        {
            // Apply Move
            // SetOccupation REMOVED
            
            // Visual Move (Smooth)
            go.transform.DOKill(); // Stop any verified tween
            var targetPos = grid.GetCellCenterWorld(nx, nz);
            go.transform.DOMove(targetPos, 0.25f).SetEase(Ease.OutBack);

            // Update Data
            var data = go.GetComponent<PlacedItemData>();
            if (data)
            {
                data.gridX = nx;
                data.gridY = nz;
            }

            // Update Index
            fp.x = nx; fp.z = nz;
            placedIndex[go] = fp;

            // Update Selection (particle/camera)
            SelectObject(go); // Refresh visual center
            return true;
        }

        // Revert REMOVED
        return false;
    }

    public bool TryRotateObject(GameObject go)
    {
        if (go == null || !placedIndex.TryGetValue(go, out var fp)) return false;

        // 1. Clear occupation REMOVED

        // 2. Calc New State
        int newRot = (fp.rotationIndex + 1) % 4;
        // Swap dimensions for 90 degree rotation
        Vector2Int newSize = new Vector2Int(fp.size.y, fp.size.x);

        // 3. Check Validity
        if (CanPlace(fp.x, fp.z, newSize, out _, out _))
        {
            // Apply REMOVED SetOccupation

            // Visual Rotate (90 deg Y) (Smooth)
            go.transform.DOKill();
            // We rotate by 90 relative. Using DORotate with mode relative or just adding 90 to current target?
            // Safer to rotate relative to current value since we might be mid-tween?
            // Actually, for pure 90 increments, better to calculate Exact Target Rotation from index?
            // No, user might have played with it. But TryRotateObject logic is strictly 0->1->2->3.
            // Let's rely on relative rotation to respect visual continuity.
            // But wait! If I spam rotate, I want it to spin 90, 180, 270...
            // transform.Rotate(0, 90, 0) is instantaneous.
            // transform.DORotate(..., mode: RotateMode.LocalAxisAdd) is what we want.
            go.transform.DOLocalRotate(new Vector3(0, 90, 0), 0.25f, RotateMode.LocalAxisAdd).SetEase(Ease.OutBack);

            // Update Data
            var data = go.GetComponent<PlacedItemData>();
            if (data)
            {
                data.rotationIndex = newRot;
            }

            // Update Index
            fp.size = newSize;
            fp.rotationIndex = newRot;
            placedIndex[go] = fp;

            SelectObject(go); // Refresh particles/center
            return true;
        }

        // Revert REMOVED
        return false;
    }

    // IsRotatedOccupied Method Deleted
    
    // --- MANUAL GHOST / DRAG API ---

    public bool IsScreenPointOnGrid(Vector2 screenPos, out int gx, out int gz)
    {
        gx = 0; gz = 0;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        Vector3 worldPos;

        if (Physics.Raycast(ray, out var hit, 5000f, groundMask))
            worldPos = hit.point;
        else
        {
            Plane p = new Plane(transform.up, transform.position);
            if (!p.Raycast(ray, out float enter)) return false;
            worldPos = ray.GetPoint(enter);
        }

        return grid.TryWorldToCell(worldPos, out gx, out gz);
    }

    public void ShowGhostAt(BuildableItem item, int gx, int gz)
    {
        // Ensure ghost instance matches item
        if (ghost == null || currentItem != item)
        {
            if (ghost) Destroy(ghost);
            ghost = Instantiate(item.prefab);
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
            foreach (var r in ghostRenderers)
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                r.materials = mats;
            }
            currentItem = item;
        }

        if (!CanPlace(gx, gz, item.size, out var cx, out var cz))
        {
            gx = cx; gz = cz;
        }
        
        bool ok = CanPlace(gx, gz, item.size, out _, out _);

        ghost.SetActive(true);
        ghost.transform.position = grid.GetCellCenterWorld(gx, gz);
        ghost.transform.rotation = transform.rotation;
        SetGhostAlpha(ok ? 0.25f : 0.15f);
    }

    public void HideGhost()
    {
        if (ghost) Destroy(ghost);
        ghost = null;
        currentItem = null;
    }

    /// <summary>
    /// Directly places an item at grid coordinates (bypassing input).
    /// </summary>
    public bool PlaceAt(BuildableItem item, int gx, int gz)
    {
        if (!CanPlace(gx, gz, item.size, out var cx, out var cz)) return false;
        Place(gx, gz, item);
        return true;
    }

    /// <summary>
    /// Places an item with full data restoration (for loading saved maps).
    /// </summary>
    public void PlaceItemDirectly(BuildableItem item, int gx, int gz, int rotationIndex, int linkedPortalId, int linkedButtonDoorId, System.Collections.Generic.List<ConfigPair> config)
    {
        if (item == null || item.prefab == null) return;

        Vector3 pos = grid.GetCellCenterWorld(gx, gz);
        Quaternion rot = Quaternion.Euler(0, 90f * rotationIndex, 0);

        var go = Instantiate(item.prefab, pos, rot, placedParent);

        // Calculate rotated size
        Vector2Int size = item.size;
        if (rotationIndex == 1 || rotationIndex == 3)
            size = new Vector2Int(size.y, size.x);

        placedIndex[go] = new Footprint
        {
            item = item,
            x = gx,
            z = gz,
            size = size,
            rotationIndex = rotationIndex
        };

        var data = go.GetComponent<PlacedItemData>();
        if (data == null) data = go.AddComponent<PlacedItemData>();

        data.Init(item, gx, gz, rotationIndex);
        data.linkedPortalId = linkedPortalId;
        data.linkedButtonDoorId = linkedButtonDoorId;

        // Restore config
        if (config != null && config.Count > 0)
        {
            data.savedConfig = new System.Collections.Generic.List<ConfigPair>(config);
            data.LoadConfigToRuntime();

            // Apply config to IMapConfigurable components
            var configurables = go.GetComponentsInChildren<IMapConfigurable>(true);
            foreach (var cfg in configurables)
            {
                cfg.ApplyConfig(data.runtimeConfig);
            }
        }

        // Restore teleport link
        if (linkedPortalId > 0)
        {
            var tlr = go.GetComponentInChildren<TeleportLinkRuntime>(true);
            if (tlr != null)
            {
                tlr.SetLinkedIdOnly(linkedPortalId);
            }
        }

        // Restore button-door link
        if (linkedButtonDoorId > 0)
        {
            var bdlr = go.GetComponentInChildren<ButtonDoorLinkRuntime>(true);
            if (bdlr != null)
            {
                bdlr.SetLinkedIdOnly(linkedButtonDoorId);
            }
        }
    }
    
    // --------- Core helpers ---------
    bool RayToGrid(out Vector3 world, out int gx, out int gz)
    {
        world = default; gx = gz = 0;
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return false;

        return IsScreenPointOnGrid(mouse.position.ReadValue(), out gx, out gz);
    }

    // Refactored CanPlace to only check MAP BOUNDARIES (No overlap check)
    bool CanPlace(int x, int z, Vector2Int size, out int clampedX, out int clampedZ)
    {
        clampedX = Mathf.Clamp(x, 0, w - size.x);
        clampedZ = Mathf.Clamp(z, 0, h - size.y);

        if (x < 0 || z < 0 || x + size.x > w || z + size.y > h) return false;

        return true;
    }

    void Place(int x, int z, BuildableItem item)
    {
        var pos = grid.GetCellCenterWorld(x, z);
        var go  = Instantiate(item.prefab, pos, transform.rotation, placedParent);
        // Attach and initialize PlacedItemData for save system
        var placedData = go.AddComponent<PlacedItemData>();
        if (placedData == null) placedData = go.AddComponent<PlacedItemData>();
        placedData.Init(item, x, z, 0); // Default rot 0

        if (enablePortalPairing)
        {
            var tp = go.GetComponent<Teleport>();
            if (tp != null)
            {
                if (firstPortal == null)
                {
                    firstPortal = tp;
                    if (pairLine)
                    {
                        pairLine.enabled = true;
                        pairLine.startColor = pairLine.endColor = portalPairColor;
                        pairLine.SetPosition(0, tp.transform.position + Vector3.up * portalPairYOffset);
                        pairLine.SetPosition(1, tp.transform.position + Vector3.up * portalPairYOffset);
                    }
                }
                else if (firstPortal != tp)
                {
                    PairPortals(firstPortal, tp);
                    firstPortal = null;
                    if (pairLine) pairLine.enabled = false;
                }
            }
        }

        // MARK OCCUPATION
        // SetOccupation(item, x, z, item.size, 0, true); REMOVED

        placedIndex[go] = new Footprint { x = x, z = z, size = item.size, item = item, rotationIndex = 0 };

        var t = go.transform;
        var start = t.localScale;
        t.localScale = start * 0.82f;
        t.DOScale(start * placeOvershoot, placeScaleIn * 0.6f).SetEase(Ease.OutQuad)
         .OnComplete(() =>
             t.DOScale(start, placeScaleIn * 0.4f).SetEase(Ease.OutQuad));
    }

    void PairPortals(Teleport a, Teleport b)
    {
        if (a == null || b == null || a == b) return;

        if (a.otherPortal == b && b.otherPortal == a)
        {
            if (a.transform && b.transform)
            {
                var sa = a.transform.localScale; var sb = b.transform.localScale;
                a.transform.DOKill(); b.transform.DOKill();
                a.transform.DOScale(sa * 1.03f, 0.10f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
                b.transform.DOScale(sb * 1.03f, 0.10f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
            }
            return;
        }

        Unpair(a);
        Unpair(b);

        a.otherPortal = b;
        b.otherPortal = a;

        var ra = GetRuntimeOfTeleport(a);
        var rb = GetRuntimeOfTeleport(b);
        if (ra != null && rb != null)
        {
            TeleportLinkRuntime.AssignPair(ra, rb);
        }

        if (a.transform && b.transform)
        {
            var sA = a.transform.localScale; var sB = b.transform.localScale;
            a.transform.DOKill(); b.transform.DOKill();
            a.transform.DOScale(sA * 1.06f, 0.12f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
            b.transform.DOScale(sB * 1.06f, 0.12f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
        }
    }

    void Unpair(Teleport t)
    {
        if (t == null) return;
        var other = t.otherPortal;
        if (other != null)
        {
            if (other.otherPortal == t) other.otherPortal = null;
            t.otherPortal = null;

            var rt = GetRuntimeOfTeleport(t);
            var ro = GetRuntimeOfTeleport(other);
            if (rt != null) rt.ClearPairFlag();
            if (ro != null) ro.ClearPairFlag();

            if (other.transform)
            {
                var s = other.transform.localScale;
                other.transform.DOKill();
                other.transform.DOScale(s * 1.02f, 0.08f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
            }
        }
        else
        {
            var rt = GetRuntimeOfTeleport(t);
            if (rt != null) rt.ClearPairFlag();
        }
    }

    void TickPortalPairing()
    {
        if (!enablePortalPairing) return;
        if (cam == null) return;

        // Update preview line to pointer or to the hovered portal
        if (firstPortal != null && pairLine != null)
        {
            pairLine.enabled = true;
            pairLine.startColor = pairLine.endColor = portalPairColor;
            pairLine.SetPosition(0, firstPortal.transform.position + Vector3.up * portalPairYOffset);
            // try snap to hovered portal center
            var hitPortal = RaycastPortal();
            Vector3 endPos;
            if (hitPortal != null)
                endPos = hitPortal.transform.position + Vector3.up * portalPairYOffset;
            else
            {
                // fallback: project pointer to grid plane height
                var plane = new Plane(transform.up, transform.position);
                Ray ray = cam.ScreenPointToRay(GetCurrentPointerPosition());
                if (!plane.Raycast(ray, out float enter)) return;
                endPos = ray.GetPoint(enter) + Vector3.up * portalPairYOffset;
            }
            pairLine.SetPosition(1, endPos);
        }

        // Tap/click behavior: select first, then second → pair
        if (WasScreenTapped(out var tapPos))
        {
            // Check UI block
            if (IsPointerOverUI()) return;

            var tp = RaycastPortalAt(tapPos);
            if (tp != null)
            {
                if (firstPortal == null)
                {
                    firstPortal = tp;
                }
                else if (firstPortal != tp)
                {
                    PairPortals(firstPortal, tp);
                    firstPortal = null;
                    if (pairLine) pairLine.enabled = false;
                }
            }
        }
    }

    Teleport RaycastPortal()
    {
        return RaycastPortalAt(GetCurrentPointerPosition());
    }

    Teleport RaycastPortalAt(Vector2 screenPos)
    {
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 5000f, portalMask))
        {
            var tp = hit.collider.GetComponentInParent<Teleport>();
            return tp;
        }
        return null;
    }

    TeleportLinkRuntime GetRuntimeOfTeleport(Teleport t)
    {
        if (t == null) return null;
        var rootGO = GetPlacedRootFromHit(t.transform);
        if (rootGO == null) return null;
        // TeleportLinkRuntime can live on a child; include inactive children just in case
        return rootGO.GetComponentInChildren<TeleportLinkRuntime>(true);
    }

    // ========= Button-Door Pairing =========

    void TickButtonDoorPairing()
    {
        if (cam == null) return;

        // Update preview line
        if (firstButtonDoor != null && buttonDoorPairLine != null)
        {
            buttonDoorPairLine.enabled = true;
            buttonDoorPairLine.SetPosition(0, firstButtonDoor.transform.position + Vector3.up * 0.25f);
            
            var hitBD = RaycastButtonDoor();
            Vector3 endPos;
            if (hitBD != null)
                endPos = hitBD.transform.position + Vector3.up * 0.25f;
            else
            {
                var plane = new Plane(transform.up, transform.position);
                Ray ray = cam.ScreenPointToRay(GetCurrentPointerPosition());
                if (!plane.Raycast(ray, out float enter)) return;
                endPos = ray.GetPoint(enter) + Vector3.up * 0.25f;
            }
            buttonDoorPairLine.SetPosition(1, endPos);
        }

        if (WasScreenTapped(out var tapPos))
        {
            if (IsPointerOverUI()) return;

            var bd = RaycastButtonDoorAt(tapPos);
            if (bd != null)
            {
                if (firstButtonDoor == null)
                {
                    firstButtonDoor = bd;
                }
                else if (firstButtonDoor != bd)
                {
                    // Check they are different types (one button, one door)
                    bool firstIsButton = firstButtonDoor.GetComponentInParent<TriggerPushButton>() != null;
                    bool secondIsButton = bd.GetComponentInParent<TriggerPushButton>() != null;
                    
                    // Only pair if one is button and other is door
                    if (firstIsButton != secondIsButton)
                    {
                        ButtonDoorLinkRuntime.AssignPair(firstButtonDoor, bd);
                        Debug.Log($"Paired Button-Door: {firstButtonDoor.name} <-> {bd.name}");
                    }
                    else
                    {
                        Debug.LogWarning("Cannot pair: Must select one Button and one Door.");
                    }
                    
                    firstButtonDoor = null;
                    if (buttonDoorPairLine) buttonDoorPairLine.enabled = false;
                }
            }
        }
    }

    ButtonDoorLinkRuntime RaycastButtonDoor()
    {
        return RaycastButtonDoorAt(GetCurrentPointerPosition());
    }

    ButtonDoorLinkRuntime RaycastButtonDoorAt(Vector2 screenPos)
    {
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 5000f))
        {
            var bd = hit.collider.GetComponentInParent<ButtonDoorLinkRuntime>();
            return bd;
        }
        return null;
    }
}
