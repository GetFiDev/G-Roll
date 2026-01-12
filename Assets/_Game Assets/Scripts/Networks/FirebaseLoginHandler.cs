using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class FirebaseLoginHandler : MonoBehaviour
{
    public UserDatabaseManager manager;

    [Header("UI Refs")]
    public UILoginPanel loginPanel;          // Login panel (SetName bunun içinde)
    public UISetNamePanel setNamePanel;      // SetName step (loginPanel’in çocuğu)
    public UserDataEditHandler editHandler;  // username yazmak için
    public UIMainMenu mainMenu;
    public UITopPanel topPanel;



    [Header("Sign In Buttons")]
    [Header("Sign In Buttons")]
    public Button androidLoginButton; // Changed from GameObject to Button
    public Button iosLoginButton;     // Changed from GameObject to Button

    public TMP_Text logText;

    [Header("Loading")]
    public GameObject loginLoadingPanel; // Spinner panel (no text)

    // login tamamlandıktan sonra true olur
    private bool _authReady = false;

    [Header("Remember Me")]
    [Tooltip("Bir kez başarılı girişten sonra e‑posta ve şifreyi yerelde sakla ve uygulama açılışında input'lara otomatik doldur.")]
    public bool rememberCredentials = true;



    // --- Single Sign-On Logic ---

    private void Start()
    {
        // NO AUTO LOGIC HERE. Controlled by AppFlowManager.
        
        SetLoading(false);
        UpdatePlatformButtons();

        // Wire up buttons programmatically to ensure they work even if Inspector event is missing
        if (androidLoginButton)
        {
             androidLoginButton.onClick.RemoveListener(OnSignInClicked);
             androidLoginButton.onClick.AddListener(OnSignInClicked);
        }
        if (iosLoginButton)
        {
             iosLoginButton.onClick.RemoveListener(OnSignInClicked);
             iosLoginButton.onClick.AddListener(OnSignInClicked);
        }
        
        // Critical: Wire up SetName Done button
        if (setNamePanel != null && setNamePanel.doneButton != null)
        {
            setNamePanel.doneButton.onClick.RemoveListener(OnSetNameDone);
            setNamePanel.doneButton.onClick.AddListener(OnSetNameDone);
        }
    }

    private void OnEnable()
    {
        if (manager == null) return;
        manager.OnLog += Log;
        manager.OnLoginSucceeded += HandleLoginSuccess;
        manager.OnLoginFailed += HandleLoginFail;
    }

    private void OnDisable()
    {
        if (manager == null) return;
        manager.OnLog -= Log;
        manager.OnLoginSucceeded -= HandleLoginSuccess;
        manager.OnLoginFailed -= HandleLoginFail;
        
        SetLoading(false);
    }
    
    // Public method for AppFlow to ensure panel is ready for manual entry
    public void OpenManualLoginPanel()
    {
        // Logic to show/reset the login UI if needed
        UpdatePlatformButtons();
    }
    
    public void CloseManualLoginPanel()
    {
        gameObject.SetActive(false);
    }

    void Log(string msg)
    {
        Debug.Log("[UI] " + msg);
        if (logText != null) logText.text = msg;
    }

    public void SetLoading(bool on)
    {
        if (loginLoadingPanel && loginLoadingPanel.activeSelf != on)
            loginLoadingPanel.SetActive(on);
    }

    // Called by the single "Sign In" button in UI
    public void OnSignInClicked()
    {
        SetLoading(true);

#if UNITY_ANDROID
        manager.LoginWithGooglePlayGames();
#elif UNITY_IOS
        manager.LoginWithGameCenter();
#else
        Log("Platform not supported for social login (Editor?)");
        // Fallback for Editor testing if needed, or just fail
        SetLoading(false);
#endif
    }
    
    private void UpdatePlatformButtons()
    {
        if (androidLoginButton != null) androidLoginButton.gameObject.SetActive(false);
        if (iosLoginButton != null) iosLoginButton.gameObject.SetActive(false);

#if UNITY_ANDROID
        if (androidLoginButton != null) androidLoginButton.gameObject.SetActive(true);
#elif UNITY_IOS
        if (iosLoginButton != null) iosLoginButton.gameObject.SetActive(true);
#else
        // Editor fallback - show android for testing likely, or both
        if (androidLoginButton != null) androidLoginButton.gameObject.SetActive(true); 
#endif
    }

    // --- Event Handlers ---
    private void HandleLoginSuccess()
    {
        // STRICT FLOW: Do NOT hide loading here.
        // We wait until Profile Check determines if we need SetName (which hides it)
        // or GameLoad (which hides the whole panel).
        // SetLoading(false); 
        
        Log("Login success");
        
        // Notify AppFlow
        if (AppFlowManager.Instance != null)
        {
            AppFlowManager.Instance.OnAuthenticationSuccess();
        }
        else
        {
            Log("⚠️ AppFlowManager.Instance missing.");
        }
    }
    
    public void ForceHideLoading() => SetLoading(false);
    
    private void HandleLoginFail(string msg)
    {
        SetLoading(false);
        Log("Login failed: " + msg);
    }
    
    // --- Updated OnSetNameDone for Architecture Phase 2 ---
    public async void OnSetNameDone()
    {
        Log("OnSetNameDone clicked");
        if (setNamePanel == null) 
        {
             Log("SetNamePanel is null!");
             return;
        }

        var name = setNamePanel.CurrentName;
        // Logic for referral code if needed
        string refCode = setNamePanel.CurrentReferral; // Use the property from UISetNamePanel
        
        if (string.IsNullOrEmpty(refCode))
        {
            refCode = PlayerPrefs.GetString("PendingReferral", "");
        }

        if (string.IsNullOrEmpty(name))
        {
             Log("Username cannot be empty");
             return;
        }

        if (setNamePanel.doneButton != null)
            setNamePanel.doneButton.interactable = false;

        // Call New Complete Profile
        bool ok = await manager.CompleteProfileAsync(name, refCode);
        
        if (ok)
        {
            Log("Profile Completed: " + name);
            
            // Clean up persisted referral code
            PlayerPrefs.DeleteKey("PendingReferral");
            PlayerPrefs.Save();

            // STRICT FLOW: UI closing is delegated to AppFlowManager
            // setNamePanel.Close(); 
            // CloseManualLoginPanel(); 
            
            // Notify AppFlow
            if (AppFlowManager.Instance != null)
                AppFlowManager.Instance.OnProfileCompleted();
        }
        else
        {
            Log("Failed to complete profile");
            if (setNamePanel.doneButton != null)
                setNamePanel.doneButton.interactable = true;
        }
    }
}
