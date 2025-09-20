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

    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerSecondPasswordInput;
    public TMP_InputField referralCodeInput;


    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;

    public TMP_Text logText;

    // login tamamlandıktan sonra true olur
    private bool _authReady = false;

    private void Start()
    {
        // ÖNEMLİ: Burada artık isim kontrolü YAPMIYORUZ.
        // Panel yalnızca login/register başarıdan sonra kontrol edilecek.
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
    }

    void Log(string msg)
    {
        Debug.Log("[UI] " + msg);
        if (logText != null) logText.text = msg;
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

        manager.Login(loginEmailInput.text, loginPasswordInput.text);
    }

    // --- Event Handlers ---
    private async void HandleLoginSuccess()
    {
        Log("Login success");
        _authReady = true;

        // İsim gerekiyor mu?
        var needs = await NeedsUsernameAsync();

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
    }

    private void HandleLoginFail(string msg)   { Log("Login failed: " + msg); }
    private void HandleRegisterFail(string msg){ Log("Register failed: " + msg); }

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
}
