using UnityEngine;

[DisallowMultipleComponent]
public class PlacedItemData : MonoBehaviour
{
    [Tooltip("Bu sahne instance'ının ait olduğu BuildableItem (ScriptableObject).")]
    public BuildableItem item;     // SO — değiştirmiyoruz

    [Tooltip("Grid üzerinde X eksenindeki hücre indeksi.")]
    public int gridX;

    [Tooltip("Grid üzerinde Y/Z (kullandığın düzleme göre) eksenindeki hücre indeksi.")]
    public int gridY;

    /// <summary>
    /// Yerleştirme anında GridPlacer tarafından doldurulması için yardımcı setter.
    /// </summary>
    public void Init(BuildableItem srcItem, int x, int y)
    {
        item  = srcItem;
        gridX = x;
        gridY = y;
    }
}