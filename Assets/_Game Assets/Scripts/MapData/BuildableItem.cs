using UnityEngine;

[CreateAssetMenu(menuName = "Build/Buildable Item", fileName = "BuildableItem")]
public class BuildableItem : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one; // hücre boyutu (w,h) -> (x,z)

    // Grid occupation logic removed as per request to allow overlapping.
}