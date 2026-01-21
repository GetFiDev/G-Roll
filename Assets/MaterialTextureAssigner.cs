#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MaterialTextureAssigner : EditorWindow
{
    private List<Material> droppedMaterials = new List<Material>();
    private Vector2 scrollPos;
    private string baseMapPropertyName = "_BaseMap"; // URP/HDRP
    private string[] commonTextureProperties = new[] { "_BaseMap", "_MainTex", "_Albedo", "_DiffuseMap" };
    private int selectedPropertyIndex = 0;

    [MenuItem("Tools/Material Texture Assigner")]
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialTextureAssigner>("Material Texture Assigner");
        window.minSize = new Vector2(450, 350);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Material Base Texture Assigner", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Drag & drop materials below. The tool will find the first texture in each material's folder and assign it as the base texture.",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // Texture property selection
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Texture Property:", GUILayout.Width(110));
        selectedPropertyIndex = EditorGUILayout.Popup(selectedPropertyIndex, commonTextureProperties);
        baseMapPropertyName = commonTextureProperties[selectedPropertyIndex];
        EditorGUILayout.EndHorizontal();

        // Custom property name
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Or Custom:", GUILayout.Width(110));
        string customProp = EditorGUILayout.TextField(baseMapPropertyName);
        if (customProp != commonTextureProperties[selectedPropertyIndex])
        {
            baseMapPropertyName = customProp;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Drop area
        DrawDropArea();

        EditorGUILayout.Space(10);

        // Material list
        if (droppedMaterials.Count > 0)
        {
            EditorGUILayout.LabelField($"Materials ({droppedMaterials.Count}):", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            for (int i = droppedMaterials.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(droppedMaterials[i], typeof(Material), false);
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    droppedMaterials.RemoveAt(i);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                droppedMaterials.Clear();
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("ASSIGN TEXTURES", GUILayout.Height(30)))
            {
                AssignTexturesToMaterials();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawDropArea()
    {
        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));

        GUI.Box(dropArea, "", EditorStyles.helpBox);

        // Draw centered text
        GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(dropArea, "Drop Materials Here", centeredStyle);

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Material mat)
                        {
                            if (!droppedMaterials.Contains(mat))
                                droppedMaterials.Add(mat);
                        }
                    }

                    // Also check paths for multi-selection from project
                    foreach (var path in DragAndDrop.paths)
                    {
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (mat != null && !droppedMaterials.Contains(mat))
                        {
                            droppedMaterials.Add(mat);
                        }
                    }
                }
                evt.Use();
                break;
        }
    }

    private void AssignTexturesToMaterials()
    {
        int successCount = 0;
        int failCount = 0;
        List<string> errors = new List<string>();

        foreach (var material in droppedMaterials)
        {
            if (material == null)
                continue;

            string matPath = AssetDatabase.GetAssetPath(material);
            string matFolder = Path.GetDirectoryName(matPath)?.Replace("\\", "/");

            if (string.IsNullOrEmpty(matFolder))
            {
                errors.Add($"{material.name}: Could not find material folder");
                failCount++;
                continue;
            }

            // Find first texture in the folder
            Texture2D foundTexture = FindFirstTextureInFolder(matFolder);

            if (foundTexture == null)
            {
                errors.Add($"{material.name}: No texture found in folder '{matFolder}'");
                failCount++;
                continue;
            }

            // Check if material has the property
            if (!material.HasProperty(baseMapPropertyName))
            {
                // Try fallback properties
                string foundProperty = null;
                foreach (var prop in commonTextureProperties)
                {
                    if (material.HasProperty(prop))
                    {
                        foundProperty = prop;
                        break;
                    }
                }

                if (foundProperty == null)
                {
                    errors.Add($"{material.name}: Property '{baseMapPropertyName}' not found");
                    failCount++;
                    continue;
                }

                // Use the found property
                material.SetTexture(foundProperty, foundTexture);
                Debug.Log($"Assigned '{foundTexture.name}' to '{material.name}' (property: {foundProperty})");
            }
            else
            {
                material.SetTexture(baseMapPropertyName, foundTexture);
                Debug.Log($"Assigned '{foundTexture.name}' to '{material.name}' (property: {baseMapPropertyName})");
            }

            EditorUtility.SetDirty(material);
            successCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Show results
        string message = $"Success: {successCount}\nFailed: {failCount}";
        if (errors.Count > 0)
        {
            message += "\n\nErrors:\n" + string.Join("\n", errors.Take(10));
            if (errors.Count > 10)
                message += $"\n... and {errors.Count - 10} more";
        }

        EditorUtility.DisplayDialog("Complete", message, "OK");

        if (successCount > 0)
        {
            droppedMaterials.Clear();
        }
    }

    private Texture2D FindFirstTextureInFolder(string folderPath)
    {
        // Search for textures in the folder (non-recursive)
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        foreach (var guid in guids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            
            // Make sure it's directly in this folder, not a subfolder
            string texFolder = Path.GetDirectoryName(texPath)?.Replace("\\", "/");
            if (texFolder != folderPath)
                continue;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (texture != null)
                return texture;
        }

        return null;
    }
}
#endif