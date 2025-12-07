using UnityEngine;
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

    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerSecondPasswordInput;
    public TMP_InputField referralCodeInput;


    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;

    [Header("Password Visibility")]
    public UnityEngine.UI.Button registerPasswordToggleButton;
    public UnityEngine.UI.Button registerSecondPasswordToggleButton;
    public UnityEngine.UI.Button loginPasswordToggleButton;

    public Sprite visibleIcon;
    public Sprite invisibleIcon;

    private bool _registerPasswordVisible = false;
    private bool _registerSecondPasswordVisible = false;
    private bool _loginPasswordVisible = false;

    [Header("Action Buttons")]
    public UnityEngine.UI.Button registerActionButton;
    public UnityEngine.UI.Button loginActionButton;

    [Header("Action Button Sprites")]
    public Sprite loginEnabledSprite;
    public Sprite loginDisabledSprite;
    public Sprite registerEnabledSprite;
    public Sprite registerDisabledSprite;

    public TMP_Text logText;

    [Header("Loading")]
    public GameObject loginLoadingPanel; // Spinner panel (no text)

    // login tamamlandıktan sonra true olur
    private bool _authReady = false;

    [Header("Remember Me")]
    [Tooltip("Bir kez başarılı girişten sonra e‑posta ve şifreyi yerelde sakla ve uygulama açılışında input'lara otomatik doldur.")]
    public bool rememberCredentials = true;

    private const string PREF_EMAIL = "login_email";
    private const string PREF_PASS  = "login_pass"; // DİKKAT: PlayerPrefs düz metin saklar. Üretimde güvenli depolama kullanın.

    private void SaveCredentials(string email, string pass)
    {
        if (!rememberCredentials) return;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass)) return;
        PlayerPrefs.SetString(PREF_EMAIL, email);
        PlayerPrefs.SetString(PREF_PASS,  pass);
        PlayerPrefs.Save();
    }

    private void LoadCredentialsToInputs()
    {
        if (!rememberCredentials) return;
        if (loginEmailInput != null && PlayerPrefs.HasKey(PREF_EMAIL))
            loginEmailInput.text = PlayerPrefs.GetString(PREF_EMAIL);
        if (loginPasswordInput != null && PlayerPrefs.HasKey(PREF_PASS))
            loginPasswordInput.text = PlayerPrefs.GetString(PREF_PASS);
    }

    public void ClearSavedCredentials()
    {
        PlayerPrefs.DeleteKey(PREF_EMAIL);
        PlayerPrefs.DeleteKey(PREF_PASS);
        PlayerPrefs.Save();
    }

    private void Start()
    {
        // Başlangıçta loader kapalı
        SetLoading(false);
        LoadCredentialsToInputs();

        // Auto-login check
        if (rememberCredentials)
        {
            if (loginEmailInput != null && !string.IsNullOrEmpty(loginEmailInput.text) &&
                loginPasswordInput != null && !string.IsNullOrEmpty(loginPasswordInput.text))
            {
                StartCoroutine(WaitForFirebaseAndLogin());
            }
        }
        // ÖNEMLİ: Burada artık isim kontrolü YAPMIYORUZ.
        // Panel yalnızca login/register başarıdan sonra kontrol edilecek.

        if (registerPasswordInput != null)
            SetInputAsPassword(registerPasswordInput, false);
        if (registerSecondPasswordInput != null)
            SetInputAsPassword(registerSecondPasswordInput, false);
        if (loginPasswordInput != null)
            SetInputAsPassword(loginPasswordInput, false);

        UpdateToggleButtonIcon(registerPasswordToggleButton, false);
        UpdateToggleButtonIcon(registerSecondPasswordToggleButton, false);
        UpdateToggleButtonIcon(loginPasswordToggleButton, false);

        UpdateLoginButtonState();
        UpdateRegisterButtonState();
    }

    private void OnEnable()
    {
        if (manager == null) return;
        manager.OnLog += Log;
        manager.OnLoginSucceeded += HandleLoginSuccess;
        manager.OnLoginFailed += HandleLoginFail;
        manager.OnRegisterFailed += HandleRegisterFail;

        // Done butonunu garanti bağla (Inspector’dan da bağlayabilirsin)
        if (setNamePanel != null && setNamePanel.doneButton != null)
        {
            setNamePanel.doneButton.onClick.RemoveListener(OnSetNameDone);
            setNamePanel.doneButton.onClick.AddListener(OnSetNameDone);
        }

        if (registerPasswordToggleButton != null)
        {
            registerPasswordToggleButton.onClick.RemoveListener(ToggleRegisterPassword);
            registerPasswordToggleButton.onClick.AddListener(ToggleRegisterPassword);
        }

        if (registerSecondPasswordToggleButton != null)
        {
            registerSecondPasswordToggleButton.onClick.RemoveListener(ToggleRegisterSecondPassword);
            registerSecondPasswordToggleButton.onClick.AddListener(ToggleRegisterSecondPassword);
        }

        if (loginPasswordToggleButton != null)
        {
            loginPasswordToggleButton.onClick.RemoveListener(ToggleLoginPassword);
            loginPasswordToggleButton.onClick.AddListener(ToggleLoginPassword);
        }

        // Live enable/disable for Login button
        if (loginEmailInput != null)
        {
            loginEmailInput.onValueChanged.RemoveListener(_ => UpdateLoginButtonState());
            loginEmailInput.onValueChanged.AddListener(_ => UpdateLoginButtonState());
        }
        if (loginPasswordInput != null)
        {
            loginPasswordInput.onValueChanged.RemoveListener(_ => UpdateLoginButtonState());
            loginPasswordInput.onValueChanged.AddListener(_ => UpdateLoginButtonState());
        }

        // Live enable/disable for Register button
        if (registerEmailInput != null)
        {
            registerEmailInput.onValueChanged.RemoveListener(_ => UpdateRegisterButtonState());
            registerEmailInput.onValueChanged.AddListener(_ => UpdateRegisterButtonState());
        }
        if (registerPasswordInput != null)
        {
            registerPasswordInput.onValueChanged.RemoveListener(_ => UpdateRegisterButtonState());
            registerPasswordInput.onValueChanged.AddListener(_ => UpdateRegisterButtonState());
        }
        if (registerSecondPasswordInput != null)
        {
            registerSecondPasswordInput.onValueChanged.RemoveListener(_ => UpdateRegisterButtonState());
            registerSecondPasswordInput.onValueChanged.AddListener(_ => UpdateRegisterButtonState());
        }
    }

    private void OnDisable()
    {
        if (manager == null) return;
        manager.OnLog -= Log;
        manager.OnLoginSucceeded -= HandleLoginSuccess;
        manager.OnLoginFailed -= HandleLoginFail;
        manager.OnRegisterFailed -= HandleRegisterFail;

        if (setNamePanel != null && setNamePanel.doneButton != null)
            setNamePanel.doneButton.onClick.RemoveListener(OnSetNameDone);

        if (registerPasswordToggleButton != null)
            registerPasswordToggleButton.onClick.RemoveListener(ToggleRegisterPassword);

        if (registerSecondPasswordToggleButton != null)
            registerSecondPasswordToggleButton.onClick.RemoveListener(ToggleRegisterSecondPassword);

        if (loginPasswordToggleButton != null)
            loginPasswordToggleButton.onClick.RemoveListener(ToggleLoginPassword);

        // Remove live listeners
        if (loginEmailInput != null)
            loginEmailInput.onValueChanged.RemoveListener(_ => UpdateLoginButtonState());
        if (loginPasswordInput != null)
            loginPasswordInput.onValueChanged.RemoveListener(_ => UpdateLoginButtonState());

        if (registerEmailInput != null)
            registerEmailInput.onValueChanged.RemoveListener(_ => UpdateRegisterButtonState());
        if (registerPasswordInput != null)
            registerPasswordInput.onValueChanged.RemoveListener(_ => UpdateRegisterButtonState());
        if (registerSecondPasswordInput != null)
            registerSecondPasswordInput.onValueChanged.RemoveListener(_ => UpdateRegisterButtonState());

        SetLoading(false);
    }

    void Log(string msg)
    {
        Debug.Log("[UI] " + msg);
        if (logText != null) logText.text = msg;
    }

    private void SetLoading(bool on)
    {
        if (loginLoadingPanel && loginLoadingPanel.activeSelf != on)
            loginLoadingPanel.SetActive(on);
    }

    // --- UI Callbacks ---
    public void OnRegisterButton()
    {
        if (registerEmailInput.text == null || registerPasswordInput.text == null || registerSecondPasswordInput.text == null)
        {
            Log("Fields are null");
            return;
        }

        if (registerPasswordInput.text == registerSecondPasswordInput.text)
        {
            SetLoading(true);
            manager.Register(registerEmailInput.text, registerPasswordInput.text,referralCodeInput.text);
        }
        else
        {
            Log("Passwords don't match");
        }
    }

    public void OnLoginButton()
    {
        if (loginEmailInput.text == null || loginPasswordInput.text == null)
        {
            Log("Fields are null");
            return;
        }

        SetLoading(true);
        manager.Login(loginEmailInput.text, loginPasswordInput.text);
    }

    // --- Event Handlers ---
    private async void HandleLoginSuccess()
    {
        Log("Login success");
        _authReady = true;

        // Ensure fresh inventory state for new user
        if (UserInventoryManager.Instance != null)
        {
            UserInventoryManager.Instance.Reset();
        }

        // Reset Referral Cache
        var refMgr = FindObjectOfType<ReferralManager>();
        if (refMgr != null) refMgr.Reset();

        // Son kullanılan login bilgilerini kaydet
        if (loginEmailInput != null && loginPasswordInput != null)
            SaveCredentials(loginEmailInput.text, loginPasswordInput.text);

        // Preload player stats JSON right after auth (step 4)
        try
        {
            var uid = manager != null && manager.currentUser != null ? manager.currentUser.UserId : null;
            if (!string.IsNullOrWhiteSpace(uid) && PlayerStatsRemoteService.Instance != null)
            {
                await PlayerStatsRemoteService.Instance.PreloadOnLoginAsync(uid);
            }
        }
        catch { /* ignore fetch errors here; gameplay will fall back to base stats */ }

        // İsim gerekiyor mu?
        var needs = await NeedsUsernameAsync();

        mainMenu.ShowPanel(UIMainMenu.PanelType.Home);
        topPanel.Initialize();

        if (needs)
        {
            // Login panel açık kalsın, SetName adımı görünsün
            if (loginPanel != null) loginPanel.gameObject.SetActive(true);
            if (setNamePanel != null) setNamePanel.Open();
        }
        else
        {
            // İsim varsa login panel kapanır, ana menüye geçersin
            if (loginPanel != null) loginPanel.CloseManualLoginPanel();
        }
        SetLoading(false);

    }

    private void HandleLoginFail(string msg)
    {
        SetLoading(false);
        Log("Login failed: " + msg);
    }
    private void HandleRegisterFail(string msg)
    {
        SetLoading(false);
        Log("Register failed: " + msg);
    }

    // --- Username kontrolü ---
    private async Task<bool> NeedsUsernameAsync()
    {
        if (setNamePanel == null || editHandler == null) return false;

        // Sadece login SONRASI kontrol edelim
        if (!_authReady) return false;

        if (setNamePanel.handler == null) setNamePanel.handler = editHandler;

        var data = await editHandler.GetUserDataAsync();
        return (data == null) || string.IsNullOrWhiteSpace(data.username);
    }

    // --- Done: username yaz ve paneli kapat ---
    public async void OnSetNameDone()
    {
        if (setNamePanel == null || editHandler == null) return;

        var name = setNamePanel.CurrentName;
        if (string.IsNullOrWhiteSpace(name))
        {
            Log("Username cannot be empty");
            return;
        }

        if (setNamePanel.doneButton != null)
            setNamePanel.doneButton.interactable = false;

        bool ok = await editHandler.SetUsernameAsync(name);
        if (ok)
        {
            Log("Username set: " + name);
            setNamePanel.Close();
            if (loginPanel != null) loginPanel.CloseManualLoginPanel();
        }
        else
        {
            Log("Failed to set username");
            if (setNamePanel.doneButton != null)
                setNamePanel.doneButton.interactable = true;
        }
    }
    private void SetInputAsPassword(TMP_InputField input, bool visible)
    {
        if (input == null) return;

        input.contentType = visible 
            ? TMP_InputField.ContentType.Standard 
            : TMP_InputField.ContentType.Password;

        input.ForceLabelUpdate();
    }

    private void UpdateToggleButtonIcon(UnityEngine.UI.Button button, bool visible)
    {
        if (button == null) return;
        var img = button.GetComponent<UnityEngine.UI.Image>();
        if (img == null) return;

        img.sprite = visible ? visibleIcon : invisibleIcon;
    }

    private void ToggleRegisterPassword()
    {
        _registerPasswordVisible = !_registerPasswordVisible;
        SetInputAsPassword(registerPasswordInput, _registerPasswordVisible);
        UpdateToggleButtonIcon(registerPasswordToggleButton, _registerPasswordVisible);
    }

    private void ToggleRegisterSecondPassword()
    {
        _registerSecondPasswordVisible = !_registerSecondPasswordVisible;
        SetInputAsPassword(registerSecondPasswordInput, _registerSecondPasswordVisible);
        UpdateToggleButtonIcon(registerSecondPasswordToggleButton, _registerSecondPasswordVisible);
    }

    private void ToggleLoginPassword()
    {
        _loginPasswordVisible = !_loginPasswordVisible;
        SetInputAsPassword(loginPasswordInput, _loginPasswordVisible);
        UpdateToggleButtonIcon(loginPasswordToggleButton, _loginPasswordVisible);
    }

    private void SetButtonState(UnityEngine.UI.Button btn, bool interactable, Sprite enabledSprite, Sprite disabledSprite)
    {
        if (btn == null) return;
        btn.interactable = interactable;
        var img = btn.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
            img.sprite = interactable ? enabledSprite : disabledSprite;
    }

    private void UpdateLoginButtonState()
    {
        bool ready = !string.IsNullOrEmpty(loginEmailInput ? loginEmailInput.text : null)
                  && !string.IsNullOrEmpty(loginPasswordInput ? loginPasswordInput.text : null);
        SetButtonState(loginActionButton, ready, loginEnabledSprite, loginDisabledSprite);
    }



    private void UpdateRegisterButtonState()
    {
        bool ready = !string.IsNullOrEmpty(registerEmailInput ? registerEmailInput.text : null)
                  && !string.IsNullOrEmpty(registerPasswordInput ? registerPasswordInput.text : null)
                  && !string.IsNullOrEmpty(registerSecondPasswordInput ? registerSecondPasswordInput.text : null)
                  && (registerPasswordInput != null && registerSecondPasswordInput != null && registerPasswordInput.text == registerSecondPasswordInput.text);
        SetButtonState(registerActionButton, ready, registerEnabledSprite, registerDisabledSprite);
    }

    private System.Collections.IEnumerator WaitForFirebaseAndLogin()
    {
        SetLoading(true);
        // Wait until manager exists
        while (manager == null)
        {
            yield return null;
        }

        // Wait until manager is ready
        while (!manager.IsFirebaseReady)
        {
            yield return null;
        }

        OnLoginButton();
    }
}
