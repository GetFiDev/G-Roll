using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Build/Build Database", fileName = "BuildDatabase")]
public class BuildDatabase : ScriptableObject
{
    public List<BuildableItem> items = new List<BuildableItem>();
    public BuildableItem GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && !string.IsNullOrEmpty(it.displayName) &&
                string.Equals(it.displayName, id, System.StringComparison.OrdinalIgnoreCase))
            {
                return it;
            }
        }
        return null;
    }
}