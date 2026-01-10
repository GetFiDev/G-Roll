using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BuildableItem))]
public class BuildableItemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BuildableItem item = (BuildableItem)target;

        Undo.RecordObject(item, "Update BuildableItem");

        // 1. Display Name
        item.displayName = EditorGUILayout.TextField("Display Name", item.displayName);

        // 2. Large Preview: ICON
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Icon", EditorStyles.boldLabel);
        item.icon = (Sprite)EditorGUILayout.ObjectField(
            item.icon, 
            typeof(Sprite), 
            false, 
            GUILayout.Height(80), 
            GUILayout.Width(80)
        );

        // 3. Large Preview: PREFAB
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
        item.prefab = (GameObject)EditorGUILayout.ObjectField(
            item.prefab, 
            typeof(GameObject), 
            false, 
            GUILayout.Height(80), 
            GUILayout.Width(80)
        );

        // 4. Size
        EditorGUILayout.Space();
        item.size = EditorGUILayout.Vector2IntField("Size (W x H)", item.size);
        
        // Occupation Matrix logic removed.

        if (GUI.changed)
        {
            EditorUtility.SetDirty(item);
        }
    }
}
