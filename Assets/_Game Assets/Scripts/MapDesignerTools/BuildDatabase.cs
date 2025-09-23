using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Build/Build Database", fileName = "BuildDatabase")]
public class BuildDatabase : ScriptableObject
{
    public List<BuildableItem> items = new List<BuildableItem>();
}