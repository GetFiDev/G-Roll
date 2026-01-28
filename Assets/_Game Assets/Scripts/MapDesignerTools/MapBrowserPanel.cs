using UnityEngine;

/// <summary>
/// Stub MapBrowserPanel - Migration sırasında derleme hatalarını önlemek için.
/// TODO: Yeni servis mimarisine entegre edilmeli.
/// </summary>
public class MapBrowserPanel : MonoBehaviour
{
    public void Open()
    {
        Debug.Log("[MapBrowserPanel] Open called (stub)");
        gameObject.SetActive(true);
    }

    public void Close()
    {
        Debug.Log("[MapBrowserPanel] Close called (stub)");
        gameObject.SetActive(false);
    }

    public void RefreshList()
    {
        Debug.Log("[MapBrowserPanel] RefreshList called (stub)");
    }
}

/// <summary>
/// Data container for loaded map information
/// </summary>
[System.Serializable]
public class LoadedMapData
{
    public string mapId;
    public string mapDisplayName;
    public string mapType;
    public int difficultyTag;
    public int mapOrder;
    public int mapLength;
    public string json;
}
