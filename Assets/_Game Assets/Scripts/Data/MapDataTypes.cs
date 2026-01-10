using System;
using System.Collections.Generic;

[Serializable]
public class MapSaveData
{
    public string savedAtIso;
    public string mapName;            // givenName_{dd/MM/yy - HH:mm:ss} or sanitized ID
    public string mapDisplayName;     // UI friendly name
    
    // New fields
    public string mapType;            // "endless" or "chapter"
    public int mapOrder;              // Order for chapters (e.g. 1, 2, 3...)
    public int mapLength;             // Z-Length of the map (zCells)

    public int difficultyTag;         // 1=Very Easy, 2=Easy, 3=Medium, 4=Hard
    public int chunkCount;             // Number of floor chunks (ceil(mapLength/120))
    public int backgroundMaterialId;  // 1..N
    public List<MapItemData> items = new List<MapItemData>();
}

[Serializable]
public class MapItemData
{
    public string displayName;
    public int gridX;
    public int gridY;
    public int rotationIndex; // 0, 1, 2, 3
    public int linkedPortalId; // -1 = not portal
    public int linkedButtonDoorId; // -1 = not button/door
    public List<ConfigPair> config = new List<ConfigPair>();
}

[Serializable]
public class ConfigPair
{
    public string key;
    public string value;
}
