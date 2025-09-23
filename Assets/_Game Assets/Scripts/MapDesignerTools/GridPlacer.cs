using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MapGridCellUtility))]
public class GridPlacer : MonoBehaviour
{
    public LayerMask groundMask = ~0;
    public BuildDatabase database;
    public Transform placedParent; // yerleşen objelerin parent'ı
    public Material ghostMaterial;

    MapGridCellUtility grid;
    Camera cam;
    BuildableItem current;      // seçili item
    GameObject ghost;           // geçici gölge (preview)
    Renderer[] ghostRenderers;

    bool[,] occupied;           // [x,z]
    int w, h;

    void Awake()
    {
        grid = GetComponent<MapGridCellUtility>();
        cam = Camera.main;
        w = grid.xCells; h = grid.zCells;
        occupied = new bool[w, h];
    }

    public void BeginPlacement(BuildableItem item)
    {
        CancelPlacement();
        current = item;
        ghost = Instantiate(item.prefab);
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        foreach (var r in ghostRenderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
            r.materials = mats;
        }
        SetGhostAlpha(0.6f);
    }

    public void CancelPlacement()
    {
        if (ghost) Destroy(ghost);
        ghost = null; current = null;
    }

    void SetGhostAlpha(float a)
    {
        if (ghostRenderers == null) return;
        foreach (var r in ghostRenderers)
        {
            foreach (var m in r.materials)
                if (m.HasProperty("_Color"))
                {
                    var c = m.color; c.a = a; m.color = c;
                }
        }
    }

    void Update()
    {
        if (current == null) return;

        if (!RayToGrid(out var world, out var gx, out var gz))
        {
            if (ghost) ghost.SetActive(false);
            return;
        }

        ghost.SetActive(true);

        // item size kontrolü
        if (!CanPlace(gx, gz, current.size, out var clampedX, out var clampedZ))
        {
            // sınır dışına taşıyorsa clamp’le deneyelim
            gx = clampedX; gz = clampedZ;
        }

        var center = grid.GetCellCenterWorld(gx, gz);
        ghost.transform.position = center;
        ghost.transform.rotation = transform.rotation; // grid ile dönsün

        bool ok = CanPlace(gx, gz, current.size, out _, out _);
        SetGhostAlpha(ok ? 0.8f : 0.3f);

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Sol tık -> yerleştir
        if (ok && mouse.leftButton.wasPressedThisFrame)
        {
            Place(gx, gz, current);
        }

        // ESC / Right Click -> iptal
        if (mouse.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    bool RayToGrid(out Vector3 world, out int gx, out int gz)
    {
        world = default; gx = gz = 0;
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return false;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 5000f, groundMask))
        {
            // ground collider'ı grid düzleminde olmalı (y = grid plane)
            world = hit.point;
        }
        else
        {
            // collider yoksa grid düzlemini matematiksel kes: pivot düzleminde
            Plane p = new Plane(transform.up, transform.position);
            if (!p.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
        }

        if (!grid.TryWorldToCell(world, out gx, out gz)) return false;
        // Ortalamalı grid olduğundan bu indexler 0..w-1, 0..h-1
        return true;
    }

    bool CanPlace(int x, int z, Vector2Int size, out int clampedX, out int clampedZ)
    {
        clampedX = Mathf.Clamp(x, 0, w - size.x);
        clampedZ = Mathf.Clamp(z, 0, h - size.y);

        // sınır kontrolü
        if (x < 0 || z < 0 || x + size.x > w || z + size.y > h) return false;

        // doluluk kontrolü
        for (int ix = 0; ix < size.x; ix++)
            for (int iz = 0; iz < size.y; iz++)
                if (occupied[x + ix, z + iz]) return false;

        return true;
    }

    void Place(int x, int z, BuildableItem item)
    {
        var go = Instantiate(item.prefab, grid.GetCellCenterWorld(x, z), transform.rotation, placedParent);
        // işaretle
        for (int ix = 0; ix < item.size.x; ix++)
            for (int iz = 0; iz < item.size.y; iz++)
                occupied[x + ix, z + iz] = true;
    }
}