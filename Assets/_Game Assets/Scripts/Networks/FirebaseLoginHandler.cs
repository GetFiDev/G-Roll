using UnityEngine;
using TMPro;

public class FirebaseLoginHandler : MonoBehaviour
{
    public UserDatabaseManager manager;

    [Header("UI Refs")]
    public UILoginPanel loginPanel; // 🎯 Login başarılı olunca kapatmak için

    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerSecondPasswordInput;

    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;

    public TMP_Text logText;

    private void OnEnable()
    {
        if (manager == null) return;
        manager.OnLog += Log;
        manager.OnLoginSucceeded += HandleLoginSuccess;
        manager.OnLoginFailed += HandleLoginFail;
        manager.OnRegisterFailed += HandleRegisterFail;
        // OnRegisterSucceeded dinlemek şart değil; Register -> LoginSucceeded zaten tetikleniyor.
    }

    private void OnDisable()
    {
        if (manager == null) return;
        manager.OnLog -= Log;
        manager.OnLoginSucceeded -= HandleLoginSuccess;
        manager.OnLoginFailed -= HandleLoginFail;
        manager.OnRegisterFailed -= HandleRegisterFail;
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
            manager.Register(registerEmailInput.text, registerPasswordInput.text);
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
    private void HandleLoginSuccess()
    {
        Log("Login success");
        if (loginPanel != null)
        {
            loginPanel.CloseManualLoginPanel();
        }
    }

    private void HandleLoginFail(string msg)
    {
        Log("Login failed: " + msg);
    }

    private void HandleRegisterFail(string msg)
    {
        Log("Register failed: " + msg);
    }
}
