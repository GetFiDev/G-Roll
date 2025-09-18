using UnityEngine;
using TMPro;

public class FirebaseTestUI : MonoBehaviour
{
    public UserDatabaseManager manager;

    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerSecondPasswordInput;

    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;

    public TMP_Text logText;

    private void OnEnable()
    {
        if (manager != null)
            manager.OnLog += Log; // UserDatabaseManager’ın loglarını dinle
    }

    private void OnDisable()
    {
        if (manager != null)
            manager.OnLog -= Log;
    }

    void Log(string msg)
    {
        Debug.Log("[UI] " + msg);
        if (logText != null) logText.text = msg;
    }

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
}
