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

    [Tooltip("Rotation index (0=0, 1=90, 2=180, 3=270).")]
    public int rotationIndex = 0;

    [Tooltip("Portal eşleştirme kimliği. -1 = portal değil, 0 = portal ama henüz eşleşmedi, 1..N = eşleşme grubu")]
    public int linkedPortalId = -1;

    [Tooltip("Button-Door eşleştirme kimliği. -1 = button/door değil, 0 = henüz eşleşmedi, 1..N = eşleşme grubu")]
    public int linkedButtonDoorId = -1;

    [Tooltip("Serialized list of configuration values.")]
    public System.Collections.Generic.List<ConfigPair> savedConfig = new();

    // Runtime dictionary for faster lookup
    public System.Collections.Generic.Dictionary<string, string> runtimeConfig = new();

    /// <summary>
    /// Yerleştirme anında GridPlacer tarafından doldurulması için yardımcı setter.
    /// </summary>
    public void Init(BuildableItem srcItem, int x, int y, int rotIdx = 0)
    {
        item  = srcItem;
        gridX = x;
        gridY = y;
        rotationIndex = rotIdx;

        // Eğer bu sahne objesi bir portal ise (TeleportLinkRuntime varsa) 0'a çek,
        // değilse -1'de kalsın.
        linkedPortalId = GetComponentInChildren<TeleportLinkRuntime>(true) != null ? 0 : -1;
        
        // Eğer bu sahne objesi bir button/door ise (ButtonDoorLinkRuntime varsa) 0'a çek
        linkedButtonDoorId = GetComponentInChildren<ButtonDoorLinkRuntime>(true) != null ? 0 : -1;
    }

    public void SaveConfigFromRuntime()
    {
        savedConfig.Clear();
        foreach (var kvp in runtimeConfig)
        {
            savedConfig.Add(new ConfigPair { key = kvp.Key, value = kvp.Value });
        }
    }

    public void LoadConfigToRuntime()
    {
        runtimeConfig.Clear();
        foreach (var kv in savedConfig)
        {
            runtimeConfig[kv.key] = kv.value;
        }
    }
}