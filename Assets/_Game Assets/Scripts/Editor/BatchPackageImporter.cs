using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class BatchPackageImporter : EditorWindow
{
    private string _selectedFolderPath = "";
    private List<PackageItem> _packageList = new List<PackageItem>();
    private Vector2 _scrollPos;

    private const string PREF_QUEUE = "BatchImporter_Queue";
    private const string PREF_IS_PROCESSING = "BatchImporter_IsProcessing";

    [System.Serializable]
    private class PackageItem
    {
        public string Path;
        public string Name;
        public bool IsSelected = true;
    }

    [MenuItem("Tools/Batch Package Importer")]
    public static void ShowWindow()
    {
        GetWindow<BatchPackageImporter>("Batch Importer");
    }

    private void OnEnable()
    {
        // Resume queue checking on recompile/load
        EditorApplication.update += ProcessQueue;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ProcessQueue;
    }
    
    // --- IMPORT LOGIC ---

    private void ProcessQueue()
    {
        // Only run if not compiling and not updating
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        bool isProcessing = EditorPrefs.GetBool(PREF_IS_PROCESSING, false);
        if (!isProcessing) return;

        string queueStr = EditorPrefs.GetString(PREF_QUEUE, "");
        if (string.IsNullOrEmpty(queueStr))
        {
            // Done
            EditorPrefs.SetBool(PREF_IS_PROCESSING, false);
            Debug.Log("<color=cyan>[BatchImporter]</color> All packages imported successfully!");
            // Refresh DB one last time
            AssetDatabase.Refresh();
            Repaint();
            return;
        }

        // Get next item
        var paths = queueStr.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();
        string currentPath = paths[0];

        Debug.Log($"<color=cyan>[BatchImporter]</color> Importing: {Path.GetFileName(currentPath)}...");

        // Remove from queue immediately to avoid infinite loop if crash, 
        // OR execute first then remove. 
        // Better: Remove first, then save, then Import. 
        // If Import triggers reload, we come back and process next[0].
        
        paths.RemoveAt(0);
        string newQueue = string.Join(";", paths);
        EditorPrefs.SetString(PREF_QUEUE, newQueue);

        // Execute Import
        if (File.Exists(currentPath))
        {
            AssetDatabase.ImportPackage(currentPath, false); // false = interactive? NO. 
            // interactive = true shows the dialog. interactive = false imports ALL immediately.
            // User requested "one click", so false is correct.
        }
        else
        {
            Debug.LogError($"[BatchImporter] File not found: {currentPath}");
        }
    }

    private void StartImport()
    {
        var selected = _packageList.Where(x => x.IsSelected).Select(x => x.Path).ToList();
        if (selected.Count == 0)
        {
            EditorUtility.DisplayDialog("Info", "No packages selected.", "OK");
            return;
        }

        string queue = string.Join(";", selected);
        EditorPrefs.SetString(PREF_QUEUE, queue);
        EditorPrefs.SetBool(PREF_IS_PROCESSING, true);
        
        Debug.Log($"<color=cyan>[BatchImporter]</color> Started batch import of {selected.Count} packages.");
    }

    // --- GUI ---

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Batch Package Importer", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Folder Selection
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextField("Folder:", _selectedFolderPath);
        if (GUILayout.Button("Select", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder with UnityPackages", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                _selectedFolderPath = path;
                ScanFolder();
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Actions
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All")) SetSelection(true);
        if (GUILayout.Button("Select None")) SetSelection(false);
        if (GUILayout.Button("Refresh List")) ScanFolder();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // List
        EditorGUILayout.LabelField($"Found {_packageList.Count} packages:");
        
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, "box");
        for (int i = 0; i < _packageList.Count; i++)
        {
            var item = _packageList[i];
            EditorGUILayout.BeginHorizontal();
            item.IsSelected = EditorGUILayout.Toggle(item.IsSelected, GUILayout.Width(20));
            EditorGUILayout.LabelField(item.Name);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        bool isProcessing = EditorPrefs.GetBool(PREF_IS_PROCESSING, false);
        
        EditorGUI.BeginDisabledGroup(isProcessing || _packageList.Count == 0);
        if (GUILayout.Button("Import Selected", GUILayout.Height(30)))
        {
            StartImport();
        }
        EditorGUI.EndDisabledGroup();
        
        if (isProcessing)
        {
             // Show queue status
             string q = EditorPrefs.GetString(PREF_QUEUE, "");
             int count = q.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
             EditorGUILayout.HelpBox($"Importing in progress... Remaining: {count}. Please wait.", MessageType.Info);
             
             if (GUILayout.Button("Stop / Clear Queue"))
             {
                 EditorPrefs.SetBool(PREF_IS_PROCESSING, false);
                 EditorPrefs.SetString(PREF_QUEUE, "");
             }
        }
    }

    private void ScanFolder()
    {
        _packageList.Clear();
        if (string.IsNullOrEmpty(_selectedFolderPath) || !Directory.Exists(_selectedFolderPath)) return;

        var files = Directory.GetFiles(_selectedFolderPath, "*.unitypackage");
        foreach (var f in files)
        {
            _packageList.Add(new PackageItem
            {
                Path = f,
                Name = Path.GetFileName(f),
                IsSelected = true
            });
        }
    }

    private void SetSelection(bool state)
    {
        foreach (var item in _packageList) item.IsSelected = state;
    }
}
