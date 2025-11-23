using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System;
using System.Threading.Tasks;
using Firebase.Functions;
using NetworkingData;

public class UIProfilePanel : MonoBehaviour
{
    [Header("Profile Fields")]
    [SerializeField] private TextMeshProUGUI emailText;
    [SerializeField] private TextMeshProUGUI currencyText;

    [Header("Username UI")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TextMeshProUGUI usernameLabel;
    [SerializeField] private GameObject usernameBusyOverlay;
    [SerializeField] private TextMeshProUGUI usernameErrorText;

    [Header("Button Sprites")]
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;

    [Header("Save Username Button")]
    [SerializeField] private Button saveUsernameButton;
    [SerializeField] private TextMeshProUGUI saveUsernameLabel;

    [Header("Button Texts")]
    [SerializeField] private string inactiveText;
    [SerializeField] private string activeText;
    [SerializeField] private string fetchingText;

    private bool _isSavingUsername;
    private UserData _data;

    private string _originalUsername = string.Empty;

    private enum UsernameButtonState
    {
        Inactive,
        Active,
        Fetching
    }

    private UsernameButtonState _buttonState = UsernameButtonState.Inactive;
    private Coroutine _errorRoutine;

    private void OnEnable()
    {
        if (usernameInput != null)
        {
            usernameInput.onValueChanged.RemoveListener(OnUsernameInputChanged);
            usernameInput.onValueChanged.AddListener(OnUsernameInputChanged);
        }

        if (usernameErrorText != null)
        {
            usernameErrorText.text = string.Empty;
        }

        _ = RefreshProfileAsync();
    }

    private void OnDisable()
    {
        if (usernameInput != null)
        {
            usernameInput.onValueChanged.RemoveListener(OnUsernameInputChanged);
        }
    }

    private async Task RefreshProfileAsync()
    {
        try
        {
            SetUsernameBusy(false, null);
            _data = await UserDatabaseManager.Instance.LoadUserData();
            ApplyToUI();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIProfilePanel] Refresh error: {e.Message}");
        }
    }

    private void ApplyToUI()
    {
        if (_data == null)
        {
            if (emailText) emailText.text = "-";
            if (currencyText) currencyText.text = "0 GET";
            if (usernameInput) usernameInput.text = string.Empty;
            if (usernameLabel) usernameLabel.text = "-";
            _originalUsername = string.Empty;
            UpdateSaveButtonVisual();
            return;
        }

        if (emailText) emailText.text = string.IsNullOrEmpty(_data.mail) ? "-" : _data.mail;
        if (currencyText) currencyText.text = $"{_data.currency} GET";

        _originalUsername = _data.username ?? string.Empty;

        if (usernameInput) usernameInput.text = _originalUsername;
        if (usernameLabel) usernameLabel.text = string.IsNullOrEmpty(_originalUsername) ? "-" : _originalUsername;

        UpdateSaveButtonVisual();
    }

    public async void OnClickSaveUsername()
    {
        if (_isSavingUsername) return;
        if (usernameInput == null) return;

        string newName = (usernameInput.text ?? string.Empty).Trim();

        // State 1 vs 2: button aktifliği zaten UpdateSaveButtonVisual ile kontrol ediliyor.
        // Burada sadece gerçekten değişmiş bir isim için işlem yapıyoruz.
        if (string.Equals(newName, _originalUsername, StringComparison.Ordinal))
        {
            // Zaten mevcut isim; hiçbir şey yapma.
            return;
        }

        string localErr = ValidateLocal(newName);
        if (localErr != null)
        {
            // Buton state'i ACTIVE kalabilir, kullanıcı ismi düzenlemeye devam eder.
            ShowMessage(localErr);
            return;
        }

        _isSavingUsername = true;
        SetUsernameBusy(true, null); // FETCHING state

        try
        {
            bool ok = await ChangeUsernameAsync(newName);
            if (ok)
            {
                _originalUsername = newName;

                if (_data != null)
                    _data.username = newName;

                ApplyToUI();
                ShowMessage("Username changed");
            }
        }
        finally
        {
            _isSavingUsername = false;
            SetUsernameBusy(false, null); // geri dön: INACTIVE veya ACTIVE (input değerine göre)
        }
    }

    private string ValidateLocal(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Kullanıcı adı boş olamaz.";
        if (name.Length < 3) return "En az 3 karakter olmalı.";
        if (name.Length > 20) return "En fazla 20 karakter.";
        foreach (char c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                return "Sadece harf, rakam ve _-.";
        }
        return null;
    }

    private async Task<bool> ChangeUsernameAsync(string newName)
    {
        try
        {
            var fn = FirebaseFunctions.GetInstance("us-central1").GetHttpsCallable("changeUsername");
            var dict = new System.Collections.Generic.Dictionary<string, object>
            {
                {"newName", newName}
            };

            var resp = await fn.CallAsync(dict);
            if (resp.Data is System.Collections.IDictionary d)
            {
                if (d.Contains("ok") && d["ok"] is bool b && b)
                    return true;

                if (d.Contains("error"))
                {
                    ShowError(MapServerError(d["error"].ToString()));
                    return false;
                }
            }

            ShowError("Sunucu hatası.");
            return false;
        }
        catch (FunctionsException fe)
        {
            ShowError(MapServerError(fe.Message));
            return false;
        }
        catch (Exception e)
        {
            ShowError("Bağlantı hatası.");
            return false;
        }
    }

    private void OnUsernameInputChanged(string value)
    {
        if (_isSavingUsername) return;
        UpdateSaveButtonVisual();
    }

    private void UpdateSaveButtonVisual()
    {
        string current = usernameInput ? (usernameInput.text ?? string.Empty).Trim() : string.Empty;
        string baseline = (_originalUsername ?? string.Empty).Trim();

        if (_isSavingUsername)
        {
            _buttonState = UsernameButtonState.Fetching;
        }
        else if (string.IsNullOrEmpty(current) || string.Equals(current, baseline, StringComparison.Ordinal))
        {
            _buttonState = UsernameButtonState.Inactive;
        }
        else
        {
            _buttonState = UsernameButtonState.Active;
        }

        if (saveUsernameButton != null)
        {
            switch (_buttonState)
            {
                case UsernameButtonState.Inactive:
                    saveUsernameButton.interactable = false;
                    break;
                case UsernameButtonState.Active:
                    saveUsernameButton.interactable = true;
                    break;
                case UsernameButtonState.Fetching:
                    saveUsernameButton.interactable = false;
                    break;
            }

            var img = saveUsernameButton.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = _buttonState == UsernameButtonState.Active ? activeSprite : inactiveSprite;
            }
        }

        if (saveUsernameLabel != null)
        {
            saveUsernameLabel.text = _buttonState == UsernameButtonState.Inactive ? inactiveText :
                                     _buttonState == UsernameButtonState.Active ? activeText :
                                     fetchingText;
        }

        if (usernameBusyOverlay != null)
        {
            usernameBusyOverlay.SetActive(_buttonState == UsernameButtonState.Fetching);
        }
    }

    private string MapServerError(string code)
    {
        switch (code)
        {
            case "USERNAME_TAKEN": return "Bu kullanıcı adı zaten alınmış.";
            case "USERNAME_BAD_WORD": return "Uygunsuz kelime içeriyor.";
            case "USERNAME_INVALID_LENGTH": return "3-20 karakter arası olmalı.";
            case "USERNAME_INVALID_CHARS": return "Geçersiz karakter.";
            case "USERNAME_CHANGE_TOO_SOON": return "Haftada bir kez değiştirebilirsin.";
            default: return "Kullanıcı adı değiştirilemedi.";
        }
    }

    private void ShowError(string msg)
    {
        SetUsernameBusy(false, msg);
    }

    private void ShowMessage(string msg, float duration = 3f)
    {
        if (usernameErrorText == null) return;

        if (_errorRoutine != null)
        {
            StopCoroutine(_errorRoutine);
            _errorRoutine = null;
        }

        usernameErrorText.text = msg ?? string.Empty;
        if (!string.IsNullOrEmpty(msg))
        {
            _errorRoutine = StartCoroutine(ClearMessageAfterDelay(duration));
        }
    }

    private System.Collections.IEnumerator ClearMessageAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (usernameErrorText != null)
        {
            usernameErrorText.text = string.Empty;
        }
        _errorRoutine = null;
    }

    private void SetUsernameBusy(bool busy, string error)
    {
        if (usernameBusyOverlay) usernameBusyOverlay.SetActive(busy);

        // busy state değişince buton state'ini güncelle
        UpdateSaveButtonVisual();

        if (!string.IsNullOrEmpty(error))
        {
            ShowMessage(error);
        }
        else if (busy && usernameErrorText != null)
        {
            // yeni bir işlem başlarken eski mesajı temizle
            if (_errorRoutine != null)
            {
                StopCoroutine(_errorRoutine);
                _errorRoutine = null;
            }
            usernameErrorText.text = string.Empty;
        }
    }
    /// <summary>
    /// Logout button action. Signs out the user and restarts the application.
    /// </summary>
    public void OnClickLogout()
    {
        Application.Quit();
    }
}