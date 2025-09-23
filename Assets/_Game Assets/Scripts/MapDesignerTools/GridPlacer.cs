using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

[RequireComponent(typeof(MapGridCellUtility))]
public class GridPlacer : MonoBehaviour
{
    [Header("Refs")]
    public LayerMask groundMask = ~0;
    public BuildDatabase database;
    public Transform placedParent;
    public Material ghostMaterial;

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

    // Exposed read-only properties for HUD
    public bool PortalPairMode => portalPairMode;
    public bool HasPortalFirstSelection => firstPortal != null;

    struct Footprint { public int x, z; public Vector2Int size; }
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
            case BuildMode.Navigate:
            default:
                // no-op
                break;
        }

        if (portalPairMode)
            TickPortalPairing();
        else if (pairLine != null && pairLine.enabled)
            pairLine.enabled = false;
    }

    void HandleHotkeys()
    {
        var kbd = Keyboard.current;
        if (kbd == null) return;

        // ESC — universal exit: leave any mode and pairing, clear selection, back to Navigate
        if (kbd.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();                 // drop ghost/currentItem
            currentMode = BuildMode.Navigate;  // go to navigate

            // fully exit portal pairing
            portalPairMode = false;
            firstPortal = null;
            if (pairLine) pairLine.enabled = false;
            return; // nothing else this frame
        }

        // X — enter Demolish mode (one-way). If already in Demolish, do nothing.
        if (kbd.xKey.wasPressedThisFrame)
        {
            if (currentMode != BuildMode.Demolish)
            {
                CancelPlacement();             // if we were placing, drop the ghost
                // leave pairing if active
                portalPairMode = false;
                firstPortal = null;
                if (pairLine) pairLine.enabled = false;

                currentMode = BuildMode.Demolish;
            }
            return; // ignore further handling this frame
        }

        // P — enter Portal Pairing mode (one-way). If already pairing, do nothing.
        if (kbd.pKey.wasPressedThisFrame)
        {
            if (!portalPairMode)
            {
                CancelPlacement();             // dropping any placement ghost
                currentMode = BuildMode.Navigate; // pairing is independent of build modes; keep nav for camera

                portalPairMode = true;
                firstPortal = null;            // start fresh selection
                if (pairLine) pairLine.enabled = false;
            }
            return;
        }
    }

    // PUBLIC API (UI butonu çağırır)
    public void BeginPlacement(BuildableItem item)
    {
        // If we were in portal pairing, exit it when a new item selection starts
        if (portalPairMode)
        {
            portalPairMode = false;
            firstPortal = null;
            if (pairLine) pairLine.enabled = false;
        }

        currentItem = item;
        currentMode = BuildMode.Place;

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

        // sınır clamp denemesi
        if (!CanPlace(gx, gz, currentItem.size, out var clampedX, out var clampedZ))
        { gx = clampedX; gz = clampedZ; }

        var ok = CanPlace(gx, gz, currentItem.size, out _, out _);
        var center = grid.GetCellCenterWorld(gx, gz);
        ghost.transform.position = center;
        ghost.transform.rotation = transform.rotation;
        SetGhostAlpha(ok ? 0.25f : 0.15f);

        var mouse = Mouse.current;
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

                    // işaretleri boşalt
                    for (int ix = 0; ix < fp.size.x; ix++)
                        for (int iz = 0; iz < fp.size.y; iz++)
                            occupied[fp.x + ix, fp.z + iz] = false;

                    placedIndex.Remove(go);
                }
            }
        }
    }

    // --------- Core helpers ---------
    bool RayToGrid(out Vector3 world, out int gx, out int gz)
    {
        world = default; gx = gz = 0;
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return false;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 5000f, groundMask))
            world = hit.point;
        else
        {
            Plane p = new Plane(transform.up, transform.position);
            if (!p.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
        }

        if (!grid.TryWorldToCell(world, out gx, out gz)) return false;
        return true;
    }

    bool CanPlace(int x, int z, Vector2Int size, out int clampedX, out int clampedZ)
    {
        clampedX = Mathf.Clamp(x, 0, w - size.x);
        clampedZ = Mathf.Clamp(z, 0, h - size.y);

        if (x < 0 || z < 0 || x + size.x > w || z + size.y > h) return false;

        for (int ix = 0; ix < size.x; ix++)
            for (int iz = 0; iz < size.y; iz++)
                if (occupied[x + ix, z + iz]) return false;

        return true;
    }

    void Place(int x, int z, BuildableItem item)
    {
        var pos = grid.GetCellCenterWorld(x, z);
        var go  = Instantiate(item.prefab, pos, transform.rotation, placedParent);

        if (enablePortalPairing)
        {
            var tp = go.GetComponent<Teleport>();
            if (tp != null)
            {
                if (firstPortal == null)
                {
                    firstPortal = tp;
                    // show small preview from first to mouse
                    if (pairLine)
                    {
                        pairLine.enabled = true;
                        pairLine.startColor = pairLine.endColor = portalPairColor;
                        pairLine.SetPosition(0, tp.transform.position + Vector3.up * portalPairYOffset);
                        pairLine.SetPosition(1, tp.transform.position + Vector3.up * portalPairYOffset);
                    }
                    // stay in Place mode to allow placing the second portal
                }
                else if (firstPortal != tp)
                {
                    PairPortals(firstPortal, tp);
                    firstPortal = null;
                    if (pairLine) pairLine.enabled = false;
                }
            }
        }

        // işaretle
        for (int ix = 0; ix < item.size.x; ix++)
            for (int iz = 0; iz < item.size.y; iz++)
                occupied[x + ix, z + iz] = true;

        placedIndex[go] = new Footprint { x = x, z = z, size = item.size };

        // güzel bir “pop-in” animasyonu
        var t = go.transform;
        var start = t.localScale;
        t.localScale = start * 0.82f;
        t.DOScale(start * placeOvershoot, placeScaleIn * 0.6f).SetEase(Ease.OutQuad)
         .OnComplete(() =>
             t.DOScale(start, placeScaleIn * 0.4f).SetEase(Ease.OutQuad));
    }

    void TickPortalPairing()
    {
        if (!enablePortalPairing) return;
        var mouse = Mouse.current; if (mouse == null || cam == null) return;

        // Update preview line to mouse or to the hovered portal
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
                // fallback: project mouse to grid plane height
                var plane = new Plane(transform.up, transform.position);
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (!plane.Raycast(ray, out float enter)) return;
                endPos = ray.GetPoint(enter) + Vector3.up * portalPairYOffset;
            }
            pairLine.SetPosition(1, endPos);
        }

        // Left click behavior: select first, then second → pair
        if (mouse.leftButton.wasPressedThisFrame)
        {
            var tp = RaycastPortal();
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
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 5000f, portalMask))
        {
            var tp = hit.collider.GetComponentInParent<Teleport>();
            return tp;
        }
        return null;
    }

    void PairPortals(Teleport a, Teleport b)
    {
        if (a == null || b == null || a == b) return;

        // already paired together? give a tiny pulse and return
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

        // Unpair both sides first (and their current partners) then pair new ones
        Unpair(a);
        Unpair(b);

        a.otherPortal = b;
        b.otherPortal = a;

        // feedback
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
            // break reciprocal link
            if (other.otherPortal == t) other.otherPortal = null;
            t.otherPortal = null;

            // light feedback on the partner to indicate unlink
            if (other.transform)
            {
                var s = other.transform.localScale;
                other.transform.DOKill();
                other.transform.DOScale(s * 1.02f, 0.08f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
            }
        }
    }
}
