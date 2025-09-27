using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ----- DTO'lar -----
[Serializable]
public class MapSaveData
{
    public string savedAtIso;
    public string mapName;            // optional: givenName_{dd/MM/yy - HH:mm:ss}
    public int difficultyTag;         // required: 1=easy, 2=medium, 3=hard
    public int backgroundMaterialId;  // required: 1..4 (selected background index)
    public List<PlacedItemSave> items = new List<PlacedItemSave>();
}

[Serializable]
public class PlacedItemSave
{
    public string displayName;
    public int gridX;
    public int gridY;
    public int linkedPortalId; // -1 = portal değil, 0 = portal ama eşleşmemiş, 1..N = eşleşme grubu
}

// ----- Kaydedici -----
public class MapSaver : MonoBehaviour
{
    [Header("UI Wiring (Optional/Required)")]
    [Tooltip("Map adı için UI input (Unity UI InputField veya TMP_InputField olabilir). Boş bırakılırsa isim kaydedilmez.")]
    [SerializeField] private Component mapNameInput; // InputField or TMP_InputField

    [Tooltip("Zorluk için UI dropdown (Unity UI Dropdown veya TMP_Dropdown). 0=Easy,1=Medium,2=Hard")] 
    [SerializeField] private Component difficultyDropdown; // Dropdown or TMP_Dropdown

    [Tooltip("Zaten var olan arayüz: aktif zemin materyali indexini buradan okuruz.")]
    [SerializeField] private GroundMaterialSwitcher groundMaterialSwitcher; // optional

    [Tooltip("Alternatif arayüz: renk paleti UI üzerinde index varsa buradan okuruz.")]
    [SerializeField] private GroundColorPaletteUI groundColorPaletteUI; // optional

    [Header("Background Probe")]
    [Tooltip("Arkaplan objesinin Renderer'ı. Kaydederken aktif materyali buradan okur ve palete göre index tespit ederiz.")]
    [SerializeField] private Renderer backgroundRenderer; // optional but recommended

    [Tooltip("Palet materyalleri (1..N). GroundMaterialSwitcher'dan okunamazsa buraya sürüklenen dizi kullanılır.")]
    [SerializeField] private Material[] knownBackgroundMaterials; // optional

    [Header("Optional Fallbacks")]
    [Tooltip("UI referansı atanmamışsa buradaki değeri kullanır (1=easy,2=medium,3=hard)")]
    [SerializeField] private int fallbackDifficultyTag = 1;
    [Tooltip("UI referansı atanmamışsa veya okunamazsa kullanılacak zemin indexi (1..4)")]
    [SerializeField] private int fallbackBackgroundIndex = 1;
    [Tooltip("Arayüz referansları atanmamışsa sahnede otomatik tarama yapar")] 
    [SerializeField] private bool autoScanUIInHierarchy = true;

    [Header("Save Toast (TMP)")]
    [Tooltip("Kaydetme sonrası gösterilecek TMP Text. Oyun başında kapalı olmalı veya script kapatır.")]
    [SerializeField] private TextMeshProUGUI saveToast;
    [SerializeField, Min(0.1f)] private float toastDuration = 3f;

    [Header("Optional Search Scope")]
    [Tooltip("Boşsa tüm sahne taranır. Dilersen sadece bu parent altında arat.")]
    public Transform searchRoot;

    private void Awake()
    {
        if (saveToast != null)
            saveToast.gameObject.SetActive(false);
    }

    // UI'daki "Save Map" butonuna bağla
    public void SaveMap()
    {
        var data = Collect();
        var json = JsonUtility.ToJson(data, true);

        string dir = Path.Combine(Application.dataPath, "MapSaveJsons");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path  = Path.Combine(dir, $"{stamp}_SavedMap.json");

        File.WriteAllText(path, json);
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        Debug.Log($"✅ Map saved: {path}");
        if (saveToast != null)
            StartCoroutine(ShowSaveToast("Map saved!"));
    }

    private IEnumerator ShowSaveToast(string message)
    {
        if (saveToast == null) yield break;
        saveToast.text = message;
        saveToast.gameObject.SetActive(true);
        yield return new WaitForSeconds(toastDuration);
        if (saveToast != null)
            saveToast.gameObject.SetActive(false);
    }

    string GetInputText(Component c)
    {
        // 1) Direct component checks (strongly-typed)
        if (c is TMP_InputField tmpIF)
            return tmpIF.text;
        if (c is InputField uiIF)
            return uiIF.text;

        // 2) Reflection fallback for any component that exposes a public string `text`
        if (c != null)
        {
            var p = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(string))
            {
                var v = (string)p.GetValue(c);
                if (!string.IsNullOrEmpty(v))
                    return v;
            }
        }

        if (!autoScanUIInHierarchy)
            return string.Empty;

        // 3) Auto-scan for TMP_InputField or UI InputField in hierarchy/scene
        if (searchRoot)
        {
            var tmp = searchRoot.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                return tmp.text;
            var ui = searchRoot.GetComponentsInChildren<InputField>(true).FirstOrDefault();
            if (ui != null && !string.IsNullOrEmpty(ui.text))
                return ui.text;
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            var tmpAll = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
#else
            var tmpAll = Resources.FindObjectsOfTypeAll<TMP_InputField>();
#endif
            var tmp = tmpAll.FirstOrDefault(t => t != null && !string.IsNullOrEmpty(t.text));
            if (tmp != null) return tmp.text;

#if UNITY_2023_1_OR_NEWER
            var uiAll = FindObjectsByType<InputField>(FindObjectsSortMode.None);
#else
            var uiAll = Resources.FindObjectsOfTypeAll<InputField>();
#endif
            var ui = uiAll.FirstOrDefault(t => t != null && !string.IsNullOrEmpty(t.text));
            if (ui != null) return ui.text;
        }

        return string.Empty;
    }

    int GetDropdownIndex(Component c)
    {
        int ReadValue(Component comp)
        {
            if (comp == null) return -1;
            var p = comp.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(int))
                return (int)p.GetValue(comp);
            return -1;
        }

        // First try the provided component
        int v = ReadValue(c);
        if (v >= 0) return v;

        if (!autoScanUIInHierarchy) return -1;

        // Try to auto-find a Dropdown/TMP_Dropdown under searchRoot (preferred) or whole scene
        IEnumerable<Component> pool = null;
        if (searchRoot)
            pool = searchRoot.GetComponentsInChildren<Component>(true);
        else
#if UNITY_2023_1_OR_NEWER
            pool = FindObjectsByType<Component>(FindObjectsSortMode.None);
#else
            pool = Resources.FindObjectsOfTypeAll<Component>();
#endif

        foreach (var comp in pool)
        {
            if (comp == null) continue;
            var typeName = comp.GetType().Name; // e.g., TMP_Dropdown or Dropdown
            if (!typeName.Contains("Dropdown")) continue;
            int val = ReadValue(comp);
            if (val >= 0) return val;
        }

        return -1;
    }

    int GetBackgroundIndexFromRenderer()
    {
        if (backgroundRenderer == null) return -1;
        var mat = backgroundRenderer.sharedMaterial != null ? backgroundRenderer.sharedMaterial : backgroundRenderer.material;
        if (mat == null) return -1;

        // Try to read palette from GroundMaterialSwitcher by reflection
        IEnumerable<Material> EnumeratePaletteFrom(object src)
        {
            if (src == null) yield break;
            var t = src.GetType();
            // candidate property/field names that can hold Material[] or List<Material>
            string[] names = {"Materials","materials","Palette","palette","Variants","variants","MaterialList","materialList"};
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (p != null)
                {
                    var val = p.GetValue(src);
                    foreach (var m in UnrollMaterials(val)) yield return m;
                }
                var f = t.GetField(n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (f != null)
                {
                    var val = f.GetValue(src);
                    foreach (var m in UnrollMaterials(val)) yield return m;
                }
            }
        }

        IEnumerable<Material> UnrollMaterials(object val)
        {
            if (val == null) yield break;
            if (val is Material[] arr)
            {
                foreach (var m in arr) if (m) yield return m;
                yield break;
            }
            if (val is List<Material> list)
            {
                foreach (var m in list) if (m) yield return m;
                yield break;
            }
        }

        int FindIndexIn(IEnumerable<Material> palette)
        {
            if (palette == null) return -1;
            int i = 0;
            foreach (var pm in palette)
            {
                i++;
                if (!pm) continue;
                if (ReferenceEquals(pm, mat)) return i; // exact ref match
                if (pm.name == mat.name) return i;      // name match fallback
            }
            return -1;
        }

        // 1) From GroundMaterialSwitcher
        var idx = FindIndexIn(EnumeratePaletteFrom(groundMaterialSwitcher));
        if (idx > 0) return idx;

        // 2) From explicit palette
        if (knownBackgroundMaterials != null && knownBackgroundMaterials.Length > 0)
            idx = FindIndexIn(knownBackgroundMaterials);
        if (idx > 0) return idx;

        return -1;
    }

    int GetBackgroundIndex()
    {
        // Prefer exact detection from renderer's active material
        int idxByRenderer = GetBackgroundIndexFromRenderer();
        if (idxByRenderer > 0)
            return idxByRenderer;

        // Fallback to UI/manager-based detection (existing logic below)
        int ReadIndexFrom(object obj)
        {
            if (obj == null) return -1;
            var t = obj.GetType();
            // Common props/fields
            string[] names = {"CurrentIndex","SelectedIndex","Index","ActiveIndex","currentIndex","selectedIndex","index","activeIndex"};
            foreach (var n in names)
            {
                var prop = t.GetProperty(n, BindingFlags.Public|BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(int))
                    return (int)prop.GetValue(obj);
                var field = t.GetField(n, BindingFlags.Public|BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(int))
                    return (int)field.GetValue(obj);
            }
            // Common getters
            string[] methods = {"GetActiveIndex","GetCurrentIndex","GetIndex","GetSelectedIndex"};
            foreach (var mn in methods)
            {
                var m = t.GetMethod(mn, BindingFlags.Public|BindingFlags.Instance);
                if (m != null && m.ReturnType == typeof(int))
                    return (int)m.Invoke(obj, null);
            }
            return -1;
        }

        object src = groundMaterialSwitcher ? (object)groundMaterialSwitcher : groundColorPaletteUI;
        int idx = ReadIndexFrom(src);

        if (idx < 0 && autoScanUIInHierarchy)
        {
            // try to locate either component in hierarchy/scene
            IEnumerable<Component> pool = null;
            if (searchRoot)
                pool = searchRoot.GetComponentsInChildren<Component>(true);
            else
#if UNITY_2023_1_OR_NEWER
                pool = FindObjectsByType<Component>(FindObjectsSortMode.None);
#else
                pool = Resources.FindObjectsOfTypeAll<Component>();
#endif
            foreach (var comp in pool)
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name; // e.g., GroundMaterialSwitcher, GroundColorPaletteUI
                if (!typeName.Contains("Material") && !typeName.Contains("Palette")) continue;
                idx = ReadIndexFrom(comp);
                if (idx >= 0) break;
            }
        }

        if (idx < 0)
        {
            Debug.LogWarning("MapSaver: Background index could not be read from UI; using fallback.");
            idx = fallbackBackgroundIndex;
        }

        return idx;
    }

    static string ComposeNamedStamp(string given)
    {
        if (string.IsNullOrWhiteSpace(given)) return string.Empty; // optional
        string ts = DateTime.Now.ToString("dd/MM/yy - HH:mm:ss");
        return $"{given}_{ts}";
    }

    static int ToDifficultyTag(int dropdownIndex)
    {
        // UI: 0=Easy,1=Medium,2=Hard  -> save as 1,2,3
        switch (dropdownIndex)
        {
            case 0: return 1; // easy
            case 1: return 2; // medium
            case 2: return 3; // hard
            default:
                Debug.LogError($"MapSaver: Invalid difficulty dropdown index {dropdownIndex}; expected 0..2. Defaulting to Easy (1).");
                return 1;
        }
    }

    private MapSaveData Collect()
    {
        var data = new MapSaveData
        {
            savedAtIso = DateTime.Now.ToString("o")
        };

        // ---- map-level metadata ----
        string givenName = GetInputText(mapNameInput);
        int diffIdx = GetDropdownIndex(difficultyDropdown);
        data.mapName = ComposeNamedStamp(givenName); // optional: empty if no name given

        if (diffIdx < 0)
        {
            if (fallbackDifficultyTag < 1 || fallbackDifficultyTag > 3)
                fallbackDifficultyTag = 1;
            Debug.LogWarning($"MapSaver: Difficulty dropdown not found; using fallback tag {fallbackDifficultyTag}.");
            data.difficultyTag = fallbackDifficultyTag;
        }
        else
        {
            data.difficultyTag = ToDifficultyTag(diffIdx);
        }

        data.backgroundMaterialId = Mathf.Clamp(GetBackgroundIndex(), 1, 4);

        var placed = GetSceneComponents<PlacedItemData>();
        foreach (var p in placed)
        {
            if (!p) continue;

            // BuildableItem'i değiştirmiyoruz; displayName'in orada olduğunu varsayıyoruz.
            string nameField = p.item ? p.item.displayName : "";
            if (string.IsNullOrWhiteSpace(nameField))
                nameField = p.item ? p.item.name : p.gameObject.name;

            data.items.Add(new PlacedItemSave
            {
                displayName = nameField,
                gridX = p.gridX,
                gridY = p.gridY,
                linkedPortalId = p.linkedPortalId
            });
        }

        return data;
    }

    private List<T> GetSceneComponents<T>() where T : Component
    {
        if (searchRoot) return searchRoot.GetComponentsInChildren<T>(true).ToList();
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<T>(FindObjectsSortMode.None).ToList();
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true).ToList();
#endif
    }
}