using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// FIX #7: Global network connection monitor that checks connectivity every 5 seconds.
/// Shows a full-screen popup when connection is lost with Retry and Quit buttons.
/// Must be placed in the first scene and will persist across scene loads.
/// </summary>
public class NetworkConnectionMonitor : MonoBehaviour
{
    public static NetworkConnectionMonitor Instance { get; private set; }
    
    [Header("Settings")]
    [SerializeField] private float checkInterval = 5f;
    [SerializeField] private string pingUrl = "https://www.google.com/generate_204";
    [SerializeField] private float pingTimeout = 5f;
    
    [Header("UI References")]
    [SerializeField] private GameObject networkErrorPopup;
    [SerializeField] private UnityEngine.UI.Button retryButton;
    [SerializeField] private UnityEngine.UI.Button quitButton;
    
    private bool _isChecking = false;
    private bool _popupShowing = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Setup buttons
        if (retryButton) retryButton.onClick.AddListener(OnRetryClicked);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);
        
        // Hide popup initially
        if (networkErrorPopup) networkErrorPopup.SetActive(false);
    }
    
    private void Start()
    {
        StartCoroutine(ConnectionCheckLoop());
    }
    
    private IEnumerator ConnectionCheckLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(checkInterval);
            
            if (!_isChecking && !_popupShowing)
            {
                yield return CheckConnection();
            }
        }
    }
    
    private IEnumerator CheckConnection()
    {
        _isChecking = true;
        bool hasConnection = false;
        
        // İlk kontrol: Unity'nin NetworkReachability
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            hasConnection = false;
        }
        else
        {
            // İkinci kontrol: Gerçek ping testi
            using (var request = UnityWebRequest.Get(pingUrl))
            {
                request.timeout = (int)pingTimeout;
                yield return request.SendWebRequest();
                
                hasConnection = request.result == UnityWebRequest.Result.Success;
            }
        }
        
        _isChecking = false;
        
        if (!hasConnection && !_popupShowing)
        {
            ShowNetworkErrorPopup();
        }
    }
    
    private void ShowNetworkErrorPopup()
    {
        _popupShowing = true;
        
        if (networkErrorPopup)
        {
            networkErrorPopup.SetActive(true);
            
            // Tüm diğer UI'ların önüne çıkar
            var canvas = networkErrorPopup.GetComponentInParent<Canvas>();
            if (canvas) canvas.sortingOrder = 9999;
        }
        
        // Oyunu durdur
        Time.timeScale = 0f;
    }
    
    private void HideNetworkErrorPopup()
    {
        _popupShowing = false;
        
        if (networkErrorPopup)
        {
            networkErrorPopup.SetActive(false);
        }
        
        Time.timeScale = 1f;
    }
    
    private void OnRetryClicked()
    {
        // App'i restart et
        // Unity'de native restart yapılamaz, alternatif çözümler:
        
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Android için: Activity'yi restart et
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var intent = activity.Call<AndroidJavaObject>("getIntent");
                var componentName = intent.Call<AndroidJavaObject>("getComponent");
                
                var mainIntent = new AndroidJavaObject("android.content.Intent", intent);
                mainIntent.Call<AndroidJavaObject>("setComponent", componentName);
                mainIntent.Call<AndroidJavaObject>("setFlags", 0x10000000 | 0x04000000); // FLAG_ACTIVITY_NEW_TASK | FLAG_ACTIVITY_CLEAR_TASK
                
                activity.Call("startActivity", mainIntent);
                activity.Call("finish");
                
                // Process'i öldür
                using (var process = new AndroidJavaClass("android.os.Process"))
                {
                    int pid = process.CallStatic<int>("myPid");
                    process.CallStatic("killProcess", pid);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkConnectionMonitor] Android restart failed: {ex.Message}. Falling back to scene reload.");
            HideNetworkErrorPopup();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
#else
        // iOS/Editor/Standalone için: Scene reload yap (native restart mümkün değil)
        HideNetworkErrorPopup();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
#endif
    }
    
    private void OnQuitClicked()
    {
        Application.Quit();
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    
    /// <summary>
    /// Manuel olarak bağlantı kontrolü tetikle (opsiyonel API)
    /// </summary>
    public void ForceCheck()
    {
        if (!_isChecking)
        {
            StartCoroutine(CheckConnection());
        }
    }
    
    /// <summary>
    /// Check if network error popup is currently showing
    /// </summary>
    public bool IsPopupShowing => _popupShowing;
}
