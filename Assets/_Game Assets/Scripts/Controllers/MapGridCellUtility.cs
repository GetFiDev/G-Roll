using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MapGridCellUtility : MonoBehaviour
{
    [Header("Grid Size")]
    [Min(1)] public int xCells = 10;
    [Min(1)] public int zCells = 10;

    [Header("Cell Size")]
    public Vector2 cellSize = new Vector2(1f, 1f); // (X, Z)

    [Header("Offset (Local)")]
    public Vector3 localOffset = Vector3.zero;

    [Header("Draw Options")]
    public bool drawGrid = true;
    public bool drawBounds = true;
    public bool drawCenters = false;
    public bool drawIndices = false;
    [Range(0.01f, 0.2f)] public float centerHandleSize = 0.05f;

    [Header("Colors")]
    public Color gridColor   = new Color(0f, 0.8f, 1f, 0.6f);
    public Color boundsColor = new Color(0f, 0.4f, 1f, 0.9f);
    public Color centerColor = new Color(1f, 0.3f, 0.1f, 0.9f);
    public Color axesColor   = new Color(1f, 0.9f, 0f, 0.9f);

    [Header("Game View Drawing")]
    public bool drawInGameView = true;          // draw in Game view (and builds)
    public bool onlyMainCamera = true;          // if true, draw only for Camera.main

    Matrix4x4 GridMatrix
    {
        get
        {
            var trs = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            var off = Matrix4x4.TRS(localOffset, Quaternion.identity, Vector3.one);
            return trs * off;
        }
    }

    public Vector2 GridWorldSize => new Vector2(xCells * Mathf.Max(0.0001f, cellSize.x),
                                                zCells * Mathf.Max(0.0001f, cellSize.y));

    Vector2 HalfSize => GridWorldSize * 0.5f;

    static Material _lineMat;
    static void EnsureLineMaterial()
    {
        if (_lineMat) return;
        var shader = Shader.Find("Hidden/Internal-Colored");
        _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMat.SetInt("_ZWrite", 0);
    }

    void OnValidate()
    {
        xCells = Mathf.Max(1, xCells);
        zCells = Mathf.Max(1, zCells);
        cellSize.x = Mathf.Max(0.0001f, cellSize.x);
        cellSize.y = Mathf.Max(0.0001f, cellSize.y);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        using (new Handles.DrawingScope(GridMatrix))
        {
            var size = GridWorldSize;
            var half = HalfSize;

            // Grid local origin is centered at (0,0); so the min corner is at (-halfX, -halfZ)
            var origin = new Vector3(-half.x, 0f, -half.y);

            if (drawBounds)
            {
                Handles.color = boundsColor;
                var a = origin;
                var b = origin + new Vector3(size.x, 0f, 0f);
                var c = origin + new Vector3(size.x, 0f, size.y);
                var d = origin + new Vector3(0f, 0f, size.y);
                Handles.DrawLine(a, b);
                Handles.DrawLine(b, c);
                Handles.DrawLine(c, d);
                Handles.DrawLine(d, a);
            }

            if (drawGrid)
            {
                Handles.color = gridColor;

                // Vertical lines along Z
                for (int ix = 0; ix <= xCells; ix++)
                {
                    float x = -half.x + ix * cellSize.x;
                    var p0 = new Vector3(x, 0f, -half.y);
                    var p1 = new Vector3(x, 0f,  half.y);
                    Handles.DrawLine(p0, p1);
                }

                // Horizontal lines along X
                for (int iz = 0; iz <= zCells; iz++)
                {
                    float z = -half.y + iz * cellSize.y;
                    var p0 = new Vector3(-half.x, 0f, z);
                    var p1 = new Vector3( half.x, 0f, z);
                    Handles.DrawLine(p0, p1);
                }
            }

            if (drawCenters || drawIndices)
            {
                for (int ix = 0; ix < xCells; ix++)
                for (int iz = 0; iz < zCells; iz++)
                {
                    var center = new Vector3(-half.x + (ix + 0.5f) * cellSize.x, 0f, -half.y + (iz + 0.5f) * cellSize.y);

                    if (drawCenters)
                    {
                        Handles.color = centerColor;
                        Handles.DotHandleCap(0, center, Quaternion.identity, HandleUtility.GetHandleSize(center) * centerHandleSize, EventType.Repaint);
                    }

                    if (drawIndices)
                    {
                        Handles.color = Color.white;
                        Handles.Label(center + Vector3.up * 0.01f, $"{ix},{iz}");
                    }
                }
            }

            // Axes at the true center (0,0)
            Handles.color = axesColor;
            Handles.DrawLine(Vector3.zero, Vector3.right  * Mathf.Min(1.0f, Mathf.Max(0.2f, cellSize.x)));
            Handles.DrawLine(Vector3.zero, Vector3.forward* Mathf.Min(1.0f, Mathf.Max(0.2f, cellSize.y)));
        }
    }
#endif

    // Centered grid utilities

    public Vector3 GetCellCenterWorld(int x, int z)
    {
        x = Mathf.Clamp(x, 0, xCells - 1);
        z = Mathf.Clamp(z, 0, zCells - 1);
        var half = HalfSize;
        var local = new Vector3(-half.x + (x + 0.5f) * cellSize.x, 0f, -half.y + (z + 0.5f) * cellSize.y);
        return GridMatrix.MultiplyPoint3x4(local);
    }

    public bool TryWorldToCell(Vector3 world, out int x, out int z)
    {
        var inv = GridMatrix.inverse;
        var local = inv.MultiplyPoint3x4(world);
        var half = HalfSize;

        float lx = local.x + half.x; // shift to 0..size.x
        float lz = local.z + half.y; // shift to 0..size.y

        x = Mathf.FloorToInt(lx / cellSize.x);
        z = Mathf.FloorToInt(lz / cellSize.y);

        if (x < 0 || z < 0 || x >= xCells || z >= zCells) return false;
        return true;
    }

    public Vector3 SnapWorldToCellCenter(Vector3 world)
    {
        var inv = GridMatrix.inverse;
        var local = inv.MultiplyPoint3x4(world);
        var half = HalfSize;

        float lx = Mathf.Clamp(local.x, -half.x, half.x);
        float lz = Mathf.Clamp(local.z, -half.y, half.y);

        int ix = Mathf.Clamp(Mathf.FloorToInt((lx + half.x) / cellSize.x), 0, xCells - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt((lz + half.y) / cellSize.y), 0, zCells - 1);

        var centerLocal = new Vector3(-half.x + (ix + 0.5f) * cellSize.x, 0f, -half.y + (iz + 0.5f) * cellSize.y);
        return GridMatrix.MultiplyPoint3x4(centerLocal);
    }


    /// <summary>
    /// Map.cs burayı çağırır. Grid (x,y) -> dünya konumu (hücre merkezi).
    /// </summary>
    public Vector3 GridToWorld(int gridX, int gridY)
    {
        return GetCellCenterWorld(gridX, gridY);
    }

    public void GetCellWorldCorners(int x, int z, out Vector3 a, out Vector3 b)
    {
        x = Mathf.Clamp(x, 0, xCells - 1);
        z = Mathf.Clamp(z, 0, zCells - 1);
        var half = HalfSize;

        var localMin = new Vector3(-half.x + x * cellSize.x, 0f, -half.y + z * cellSize.y);
        var localMax = localMin + new Vector3(cellSize.x, 0f, cellSize.y);

        a = GridMatrix.MultiplyPoint3x4(localMin);
        b = GridMatrix.MultiplyPoint3x4(localMax);
    }

    void OnRenderObject()
    {
        if (!drawInGameView) return;
        var cam = Camera.current;
        if (onlyMainCamera && cam != Camera.main) return;

        EnsureLineMaterial();
        if (!_lineMat) return;

        _lineMat.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(GridMatrix);

        // Draw bounds
        if (drawBounds)
        {
            GL.Begin(GL.LINES);
            GL.Color(boundsColor);
            var size = GridWorldSize;
            var half = HalfSize;
            var origin = new Vector3(-half.x, 0f, -half.y);
            var a = origin;
            var b = origin + new Vector3(size.x, 0f, 0f);
            var c = origin + new Vector3(size.x, 0f, size.y);
            var d = origin + new Vector3(0f, 0f, size.y);
            GL.Vertex(a); GL.Vertex(b);
            GL.Vertex(b); GL.Vertex(c);
            GL.Vertex(c); GL.Vertex(d);
            GL.Vertex(d); GL.Vertex(a);
            GL.End();
        }

        // Draw grid
        if (drawGrid)
        {
            GL.Begin(GL.LINES);
            GL.Color(gridColor);
            var size = GridWorldSize;
            var half = HalfSize;
            for (int ix = 0; ix <= xCells; ix++)
            {
                float x = -half.x + ix * cellSize.x;
                GL.Vertex(new Vector3(x, 0f, -half.y));
                GL.Vertex(new Vector3(x, 0f,  half.y));
            }
            for (int iz = 0; iz <= zCells; iz++)
            {
                float z = -half.y + iz * cellSize.y;
                GL.Vertex(new Vector3(-half.x, 0f, z));
                GL.Vertex(new Vector3( half.x, 0f, z));
            }
            GL.End();
        }

        GL.PopMatrix();
    }
}
