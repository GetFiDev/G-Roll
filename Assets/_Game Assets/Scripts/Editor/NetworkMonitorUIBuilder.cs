#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor tool to create the NetworkConnectionMonitor UI placeholder at runtime.
/// Menu: G-Roll/Build Network Monitor UI
/// </summary>
public class NetworkMonitorUIBuilder : Editor
{
    [MenuItem("G-Roll/Build Network Monitor UI")]
    public static void BuildNetworkMonitorUI()
    {
        // 1. Find or create a Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("NetworkMonitorCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Top of all UI
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        
        // 2. Create the NetworkConnectionMonitor GameObject
        GameObject monitorGO = new GameObject("NetworkConnectionMonitor");
        monitorGO.transform.SetParent(null); // Root level for DontDestroyOnLoad
        var monitor = monitorGO.AddComponent<NetworkConnectionMonitor>();
        
        // 3. Create the Network Error Popup
        GameObject popup = CreatePopup(canvas.transform);
        
        // 4. Assign references to the monitor
        SerializedObject so = new SerializedObject(monitor);
        so.FindProperty("networkErrorPopup").objectReferenceValue = popup;
        so.FindProperty("retryButton").objectReferenceValue = popup.transform.Find("Panel/RetryButton")?.GetComponent<Button>();
        so.FindProperty("quitButton").objectReferenceValue = popup.transform.Find("Panel/QuitButton")?.GetComponent<Button>();
        so.ApplyModifiedProperties();
        
        // Select the new object
        Selection.activeGameObject = monitorGO;
        
        Debug.Log("[NetworkMonitorUIBuilder] Created NetworkConnectionMonitor with UI popup. Drag to a prefab or add to your boot scene.");
        
        EditorUtility.DisplayDialog(
            "Network Monitor UI Created", 
            "Created:\n• NetworkConnectionMonitor GameObject\n• Network Error Popup UI\n\nDon't forget to:\n1. Save this as a prefab or add to your boot scene\n2. The popup is initially inactive (correct behavior)", 
            "OK"
        );
    }
    
    private static GameObject CreatePopup(Transform canvasParent)
    {
        // Root popup container
        GameObject popup = new GameObject("NetworkErrorPopup");
        popup.transform.SetParent(canvasParent, false);
        var popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;
        
        // Semi-transparent background overlay
        Image overlay = popup.AddComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.85f);
        overlay.raycastTarget = true;
        
        // Center panel
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(popup.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(600, 400);
        panelRect.anchoredPosition = Vector2.zero;
        
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        
        // Add outline
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.3f, 0.3f, 1f);
        outline.effectDistance = new Vector2(3, -3);
        
        // Icon (WiFi Off symbol placeholder)
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(panel.transform, false);
        var iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.75f);
        iconRect.anchorMax = new Vector2(0.5f, 0.75f);
        iconRect.sizeDelta = new Vector2(80, 80);
        iconRect.anchoredPosition = Vector2.zero;
        
        var iconText = icon.AddComponent<TextMeshProUGUI>();
        iconText.text = "⚠";
        iconText.fontSize = 64;
        iconText.color = new Color(1f, 0.4f, 0.4f, 1f);
        iconText.alignment = TextAlignmentOptions.Center;
        
        // Title
        GameObject title = new GameObject("Title");
        title.transform.SetParent(panel.transform, false);
        var titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.55f);
        titleRect.anchorMax = new Vector2(0.5f, 0.55f);
        titleRect.sizeDelta = new Vector2(500, 50);
        titleRect.anchoredPosition = Vector2.zero;
        
        var titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Network Connection Error";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        
        // Message
        GameObject message = new GameObject("Message");
        message.transform.SetParent(panel.transform, false);
        var msgRect = message.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.5f, 0.4f);
        msgRect.anchorMax = new Vector2(0.5f, 0.4f);
        msgRect.sizeDelta = new Vector2(500, 60);
        msgRect.anchoredPosition = Vector2.zero;
        
        var msgText = message.AddComponent<TextMeshProUGUI>();
        msgText.text = "Unable to connect to the internet.\nPlease check your connection and try again.";
        msgText.fontSize = 20;
        msgText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        msgText.alignment = TextAlignmentOptions.Center;
        
        // Buttons container
        GameObject buttons = new GameObject("Buttons");
        buttons.transform.SetParent(panel.transform, false);
        var buttonsRect = buttons.AddComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.5f, 0.15f);
        buttonsRect.anchorMax = new Vector2(0.5f, 0.15f);
        buttonsRect.sizeDelta = new Vector2(400, 60);
        buttonsRect.anchoredPosition = Vector2.zero;
        
        var hlg = buttons.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 40;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        
        // Retry Button
        GameObject retryBtn = CreateButton("RetryButton", "RETRY", buttons.transform, 
            new Color(0.2f, 0.7f, 0.3f, 1f));
        
        // Quit Button
        GameObject quitBtn = CreateButton("QuitButton", "QUIT", buttons.transform, 
            new Color(0.7f, 0.2f, 0.2f, 1f));
        
        // Hide popup by default
        popup.SetActive(false);
        
        return popup;
    }
    
    private static GameObject CreateButton(string name, string label, Transform parent, Color bgColor)
    {
        GameObject btn = new GameObject(name);
        btn.transform.SetParent(parent, false);
        var btnRect = btn.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(140, 50);
        
        Image btnBg = btn.AddComponent<Image>();
        btnBg.color = bgColor;
        
        Button button = btn.AddComponent<Button>();
        button.targetGraphic = btnBg;
        
        // Set color transitions
        var colors = button.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = new Color(bgColor.r + 0.1f, bgColor.g + 0.1f, bgColor.b + 0.1f, 1f);
        colors.pressedColor = new Color(bgColor.r - 0.1f, bgColor.g - 0.1f, bgColor.b - 0.1f, 1f);
        button.colors = colors;
        
        // Label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btn.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        
        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 22;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        
        return btn;
    }
}
#endif
