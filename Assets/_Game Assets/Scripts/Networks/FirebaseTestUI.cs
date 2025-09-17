using UnityEngine;
using TMPro;

public class FirebaseTestUI : MonoBehaviour
{
    public FirebaseUserManager manager;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_Text logText;

    void Log(string msg)
    {
        Debug.Log(msg);
        if (logText != null) logText.text = msg;
    }

    public void OnRegisterButton()
    {
        manager.Register(emailInput.text, passwordInput.text);
        Log("Kayıt deneniyor...");
    }

    public void OnLoginButton()
    {
        manager.Login(emailInput.text, passwordInput.text);
        Log("Login deneniyor...");
    }

    public void OnSaveButton()
    {
        manager.SaveUserData(123, 999); // test amaçlı skor & currency
        Log("Veri kaydediliyor...");
    }

    public void OnLoadButton()
    {
        manager.LoadUserData();
        Log("Veri yükleniyor...");
    }
}
