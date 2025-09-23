using System;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "MapData", menuName = "Game/Map Data", order = 10)]
public class MapData : ScriptableObject
{
    [BoxGroup("Grid Settings")]
    [Min(1)] public int width  = 16;
    [BoxGroup("Grid Settings")]
    [Min(1)] public int height = 120;

    [BoxGroup("Grid Settings")] public Vector2 cellSize   = new Vector2(1, 1);
    [BoxGroup("Grid Settings")] public Vector3 localOffset = Vector3.zero;

    // Odin: 2D tablo
    [BoxGroup("Cells"), PropertySpace(8)]
    [TableMatrix(
        DrawElementMethod = nameof(DrawCell),
        ResizableColumns  = false,
        SquareCells       = true,
        RowHeight         = 22,
        HorizontalTitle   = "X →",
        VerticalTitle     = "Z ↓"
    )]
    public MapCell[,] Cells;

    // Hücre verisine erişim
    public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < width && z < height;
    public ref MapCell Ref(int x, int z) => ref Cells[x, z];

    // Boyut değişince matrisi koruyarak yeniden ölçekle
    [Button(ButtonSizes.Medium), PropertySpace]
    public void RegenerateMatrix()
    {
        if (width < 1) width = 1;
        if (height < 1) height = 1;

        var newCells = new MapCell[width, height];
        if (Cells != null)
        {
            int minW = Mathf.Min(width,  Cells.GetLength(0));
            int minH = Mathf.Min(height, Cells.GetLength(1));
            for (int x = 0; x < minW; x++)
                for (int z = 0; z < minH; z++)
                    newCells[x, z] = Cells[x, z];
        }
        Cells = newCells;
    }

    private void OnValidate()
    {
        if (Cells == null || Cells.GetLength(0) != width || Cells.GetLength(1) != height)
            RegenerateMatrix();
    }

#if UNITY_EDITOR
    // Odin tablo hücre çizimi — tek tıkla toggle, sağ tıkla tür menüsü
    private static MapCell DrawCell(Rect rect, MapCell value)
    {
        // Renk: tür + walkable atanır
        var col = value.GetColor();
        var old = GUI.color;
        GUI.color = col;
        GUI.Box(rect, GUIContent.none);
        GUI.color = old;

        // Sol tık: walkable toggle
        var e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            if (e.button == 0) { value.walkable = !value.walkable; e.Use(); }
            else if (e.button == 1)
            {
                // Sağ tık: enum seçimi popup
                var menu = new UnityEditor.GenericMenu();
                foreach (MapCellKind k in Enum.GetValues(typeof(MapCellKind)))
                {
                    var kk = k;
                    menu.AddItem(new GUIContent(kk.ToString()), value.kind == kk, () => {
                        value.kind = kk;
                        UnityEditor.EditorWindow.focusedWindow?.Repaint();
                    });
                }
                menu.ShowAsContext();
                e.Use();
            }
        }

        // Küçük işaret (W = walkable)
        if (value.walkable)
        {
            var label = new Rect(rect.x + 3, rect.y + 2, rect.width - 6, rect.height - 4);
            GUI.Label(label, "·"); // minimal işaret
        }

        return value;
    }
#endif
}