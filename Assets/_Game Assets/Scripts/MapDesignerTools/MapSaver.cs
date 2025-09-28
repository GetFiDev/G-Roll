using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Auth;
using System.Threading.Tasks;
using RemoteApp;


#if UNITY_EDITOR
using UnityEditor;
#endif

// ===== DTOs =====
[Serializable]
public class MapSaveData
{
    public string savedAtIso;
    public string mapName;            // givenName_{dd/MM/yy - HH:mm:ss} (optional)
    public string mapDisplayName;     // UI'dan gelen ham isim (timestamp'siz)
    public int difficultyTag;         // 1=easy, 2=medium, 3=hard
    public int backgroundMaterialId;  // 1..N
    public List<PlacedItemSave> items = new List<PlacedItemSave>();
}

[Serializable]
public class PlacedItemSave
{
    public string displayName;
    public int gridX;
    public int gridY;
    public int linkedPortalId; // -1 = portal değil, 0 = eşleşmemiş, 1..N = eşleşme grubu
}

// ===== Plain Local Saver =====
public class MapSaver : MonoBehaviour
{
    [Header("UI (assign directly – no autoscan)")]
    [SerializeField] private TMP_InputField mapNameInput;       // required for name (or leave null)
    [SerializeField] private TMP_Dropdown  difficultyDropdown;  // values: 0=Easy,1=Medium,2=Hard

    [Header("Background (simple)")]
    [Tooltip("Aktif materyali okumak için arkaplan Renderer (opsiyonel).")]
    [SerializeField] private Renderer backgroundRenderer;
    [Tooltip("Palet materyalleri (1..N). Sırası kaydedilecek ID'yi belirler.")]
    [SerializeField] private Material[] knownBackgroundMaterials;
    [Tooltip("Eğer yukarıdakiler atanmadıysa kullanılacak sabit değer (1..4).")]
    [SerializeField] private int fallbackBackgroundIndex = 1;

    [Header("Toast (optional)")]
    [SerializeField] private TextMeshProUGUI saveToast;
    [SerializeField, Min(0.1f)] private float toastDuration = 3f;

    [Header("Cloud Save (optional)")]
    [SerializeField] private RemoteAppDataService remoteService;
    [SerializeField] private bool uploadToFirestoreOnSave = true;

    private void Awake()
    {
        if (saveToast != null) saveToast.gameObject.SetActive(false);
    }
    
    async Task EnsureSignedInAsync()
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != null) return;
        await FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync();
        Debug.Log($"Signed in anonymously: {FirebaseAuth.DefaultInstance.CurrentUser.UserId}");
    }

    static string SanitizeForId(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        foreach (var ch in new[]{'/', '\\', '#', '?', '%', '[',']',':','*','\"'})
            s = s.Replace(ch.ToString(), "_");
        return s.Trim();
    }

    // === Public entry ===
    public async void SaveMap()
    {
        // Collect & local write first
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
        if (saveToast != null) StartCoroutine(ShowSaveToast("Map saved!"));

        // Cloud (optional)
        if (!uploadToFirestoreOnSave || remoteService == null)
            return;

        try
        {
            await EnsureSignedInAsync(); // Anonymous sign-in (Editor + runtime)
            // Use timestamped mapName as id; sanitize for Firestore
            string mapId = !string.IsNullOrWhiteSpace(data.mapName) ? SanitizeForId(data.mapName) : stamp;
            Debug.Log($"☁️ Uploading map JSON with id '{mapId}'...");
            // Store the whole JSON under single field named 'json'
            await remoteService.SaveMapJsonAsync(mapId, "json", json);
            if (saveToast != null) StartCoroutine(ShowSaveToast($"Uploaded: {mapId}"));
            Debug.Log($"☁️ Uploaded: {mapId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Cloud upload failed: {ex.Message}\n{ex}");
            if (saveToast != null) StartCoroutine(ShowSaveToast("Upload failed"));
        }
    }

    // === Collect ===
    private MapSaveData Collect()
    {
        var data = new MapSaveData { savedAtIso = DateTime.Now.ToString("o") };

        // Name
        string given = mapNameInput ? mapNameInput.text : string.Empty;
        data.mapDisplayName = given ?? string.Empty;
        data.mapName        = ComposeNamedStamp(given); // boşsa empty kalır

        // Difficulty
        int tag = 1; // default Easy
        if (difficultyDropdown)
        {
            switch (difficultyDropdown.value)
            {
                case 0: tag = 1; break; // easy
                case 1: tag = 2; break; // medium
                case 2: tag = 3; break; // hard
                default: tag = 1; break;
            }
        }
        data.difficultyTag = tag;

        // Background
        data.backgroundMaterialId = Mathf.Clamp(GetBackgroundIndexSimple(), 1, 99);

        // Items
        var placed = GetSceneComponents<PlacedItemData>();
        foreach (var p in placed)
        {
            if (!p) continue;
            string nameField = p.item ? (string.IsNullOrWhiteSpace(p.item.displayName) ? p.item.name : p.item.displayName) : p.gameObject.name;
            data.items.Add(new PlacedItemSave
            {
                displayName   = nameField,
                gridX         = p.gridX,
                gridY         = p.gridY,
                linkedPortalId= p.linkedPortalId
            });
        }

        return data;
    }

    // === Helpers ===
    private int GetBackgroundIndexSimple()
    {
        // Try exact match by renderer & known palette
        if (backgroundRenderer && knownBackgroundMaterials != null && knownBackgroundMaterials.Length > 0)
        {
            var mat = backgroundRenderer.sharedMaterial != null ? backgroundRenderer.sharedMaterial : backgroundRenderer.material;
            if (mat != null)
            {
                for (int i = 0; i < knownBackgroundMaterials.Length; i++)
                {
                    var m = knownBackgroundMaterials[i];
                    if (!m) continue;
                    if (ReferenceEquals(m, mat) || m.name == mat.name)
                        return i + 1; // 1..N
                }
            }
        }
        // Fallback constant
        return Mathf.Clamp(fallbackBackgroundIndex, 1, 99);
    }

    private static string ComposeNamedStamp(string given)
    {
        if (string.IsNullOrWhiteSpace(given)) return string.Empty;
        string ts = DateTime.Now.ToString("dd/MM/yy - HH:mm:ss");
        return $"{given}_{ts}";
    }

    private System.Collections.IEnumerator ShowSaveToast(string message)
    {
        saveToast.text = message;
        saveToast.gameObject.SetActive(true);
        yield return new WaitForSeconds(toastDuration);
        saveToast.gameObject.SetActive(false);
    }

    private List<T> GetSceneComponents<T>() where T : Component
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<T>(FindObjectsSortMode.None).ToList();
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true).ToList();
#endif
    }
}