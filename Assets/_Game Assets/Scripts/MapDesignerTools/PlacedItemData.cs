using UnityEngine;

[DisallowMultipleComponent]
public class PlacedItemData : MonoBehaviour
{
    [Tooltip("Bu sahne instance'ının ait olduğu BuildableItem (ScriptableObject).")]
    public BuildableItem item; // SO — değiştirmiyoruz

    [Tooltip("Grid üzerinde X eksenindeki hücre indeksi.")]
    public int gridX;

    [Tooltip("Grid üzerinde Y/Z (kullandığın düzleme göre) eksenindeki hücre indeksi.")]
    public int gridY;

    [Tooltip("Portal eşleştirme kimliği. -1 = portal değil, 0 = portal ama henüz eşleşmedi, 1..N = eşleşme grubu")]
    public int linkedPortalId = -1;

    /// <summary>
    /// Yerleştirme anında GridPlacer tarafından doldurulması için yardımcı setter.
    /// </summary>
    public void Init(BuildableItem srcItem, int x, int y)
    {
        item  = srcItem;
        gridX = x;
        gridY = y;

        // Eğer bu sahne objesi bir portal ise (TeleportLinkRuntime varsa) 0'a çek,
        // değilse -1'de kalsın.
        linkedPortalId = GetComponentInChildren<TeleportLinkRuntime>(true) != null ? 0 : -1;
    }
}