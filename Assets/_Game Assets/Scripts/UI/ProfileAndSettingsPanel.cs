using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Threading.Tasks;
using Firebase.Functions;
using NetworkingData;

public class ProfileAndSettingsPanel : MonoBehaviour
{
    [Header("Settings - Toggles")]
    [SerializeField] private ToggleButton hapticToggle;
    [SerializeField] private ToggleButton soundToggle;
    [SerializeField] private ToggleButton musicToggle;

    [Header("Profile - Fields")]
    [SerializeField] private TextMeshProUGUI emailText;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private TextMeshProUGUI premiumCurrencyText;

    [Header("Profile - Username UI")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TextMeshProUGUI usernameLabel;
    
    [Header("Profile - Metadata")]
    [SerializeField] private TextMeshProUGUI appVersionText;

    [Header("External Links - URLs")]
    [SerializeField] private string tosUrl = "https://google.com";
    [SerializeField] private string privacyUrl = "https://google.com";
    [SerializeField] private string telegramUrl = "https://t.me";
    [SerializeField] private string xUrl = "https://x.com";
    [SerializeField] private string instagramUrl = "https://instagram.com";
    [SerializeField] private string youtubeUrl = "https://youtube.com";

    [Header("External Links - Buttons")]
    [SerializeField] private Button tosButton;
    [SerializeField] private Button privacyButton;
    [SerializeField] private Button telegramButton;
    [SerializeField] private Button xButton;
    [SerializeField] private Button instagramButton;
    [SerializeField] private Button youtubeButton;

    private UserData _data;
    private string _originalUsername = string.Empty;
    
    // PlayerPrefs keys matching UserStatsDisplayer to share cache
    private const string PREF_KEY_CURRENCY = "UserStatsDisplayer.LastCurrency"; // Must match UserStatsDisplayer
    private const string PREF_KEY_PREM_CURRENCY = "UserStatsDisplayer.LastPremiumCurrency"; // Must match UserStatsDisplayer

    // Animation Coroutines
    private Coroutine _currencyAnimRoutine;
    private Coroutine _premiumCurrencyAnimRoutine;
    private Coroutine _usernameFeedbackRoutine;

    private void OnEnable()
    {
        InitializeSettings();
        InitializeProfile();
        InitializeLinks();
    }

    private void OnDisable()
    {
        if (usernameInput != null)
        {
            usernameInput.onSubmit.RemoveListener(OnUsernameSubmit);
        }
        
        // Good practice to remove listeners, though OnEnable adds them back.
        // For buttons it's usually fine if we don't remove, but to prevent duplicates if OnEnable called multiple times:
        RemoveLinkListeners();
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #region Settings Logic

    private void InitializeSettings()
    {
        if (hapticToggle) hapticToggle.SetValue(DataManager.Vibration);
        if (soundToggle) soundToggle.SetValue(DataManager.Sound);
        if (musicToggle) musicToggle.SetValue(DataManager.Music);

        // Version Display
        if (appVersionText != null)
        {
            appVersionText.text = $"v{Application.version}";
        }
    }

    public void OnSoundToggled()
    {
        bool value = soundToggle != null && soundToggle.Value;
        Debug.Log($"[ProfileAndSettingsPanel] OnSoundToggled called. soundToggle={soundToggle}, Value={value}");
        DataManager.Sound = value;
        // AudioManager now listens to DataManager.OnSoundStateChanged automatically
    }

    public void OnMusicToggled()
    {
        bool value = musicToggle != null && musicToggle.Value;
        Debug.Log($"[ProfileAndSettingsPanel] OnMusicToggled called. musicToggle={musicToggle}, Value={value}");
        DataManager.Music = value;
        // AudioManager now listens to DataManager.OnMusicStateChanged automatically
    }

    public void OnHapticToggled(bool value)
    {
        DataManager.Vibration = value;
        HapticManager.SetHapticsActive(value);
    }

    #region External Links

    private void InitializeLinks()
    {
        // Clear first to be safe
        RemoveLinkListeners();

        if (tosButton) tosButton.onClick.AddListener(OpenToS);
        if (privacyButton) privacyButton.onClick.AddListener(OpenPrivacy);
        if (telegramButton) telegramButton.onClick.AddListener(OpenTelegram);
        if (xButton) xButton.onClick.AddListener(OpenX);
        if (instagramButton) instagramButton.onClick.AddListener(OpenInstagram);
        if (youtubeButton) youtubeButton.onClick.AddListener(OpenYoutube);
    }

    private void RemoveLinkListeners()
    {
        if (tosButton) tosButton.onClick.RemoveListener(OpenToS);
        if (privacyButton) privacyButton.onClick.RemoveListener(OpenPrivacy);
        if (telegramButton) telegramButton.onClick.RemoveListener(OpenTelegram);
        if (xButton) xButton.onClick.RemoveListener(OpenX);
        if (instagramButton) instagramButton.onClick.RemoveListener(OpenInstagram);
        if (youtubeButton) youtubeButton.onClick.RemoveListener(OpenYoutube);
    }

    public void OpenToS() => OpenUrlSafely(tosUrl);
    public void OpenPrivacy() => OpenUrlSafely(privacyUrl);
    public void OpenTelegram() => OpenUrlSafely(telegramUrl);
    public void OpenX() => OpenUrlSafely(xUrl);
    public void OpenInstagram() => OpenUrlSafely(instagramUrl);
    public void OpenYoutube() => OpenUrlSafely(youtubeUrl);

    private void OpenUrlSafely(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
        else
        {
            Debug.LogWarning("[ProfileAndSettingsPanel] URL is empty.");
        }
    }

    #endregion

    #endregion

    #region Profile Logic

    private void InitializeProfile()
    {
        // 1. Setup Inputs
        if (usernameInput != null)
        {
            usernameInput.onSubmit.RemoveListener(OnUsernameSubmit);
            usernameInput.onSubmit.AddListener(OnUsernameSubmit);
            
            // Clean state
            usernameInput.textComponent.color = Color.white; 
            usernameInput.interactable = true;
        }

        // 2. Load Cached Values immediately
        LoadAndDisplayCachedStats();

        // 3. Fetch Fresh Data & Animate
        _ = RefreshProfileAsync();
    }
    
    private void LoadAndDisplayCachedStats()
    {
        double lastCurrency = GetCachedCurrency();
        float lastPremium = GetCachedPremiumCurrency();

        if (currencyText) currencyText.text = lastCurrency.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (premiumCurrencyText) premiumCurrencyText.text = lastPremium.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task RefreshProfileAsync()
    {
        try
        {
            if (UserDatabaseManager.Instance)
                _data = await UserDatabaseManager.Instance.LoadUserData();
            
            ApplyToUI();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileAndSettingsPanel] Refresh error: {e.Message}");
        }
    }

    private void ApplyToUI()
    {
        if (_data == null)
        {
            if (emailText) emailText.text = "-";
            if (usernameInput) usernameInput.text = string.Empty;
            if (usernameLabel) usernameLabel.text = "-";
            _originalUsername = string.Empty;
            return;
        }

        // Email
        if (emailText) emailText.text = string.IsNullOrEmpty(_data.mail) ? "-" : _data.mail;

        // Username
        _originalUsername = _data.username ?? string.Empty;
        if (usernameInput) 
        {
            // Only update text if user is NOT currently typing/focused to avoid overriding
            if(!usernameInput.isFocused)
                usernameInput.text = _originalUsername;
        }
        if (usernameLabel) usernameLabel.text = string.IsNullOrEmpty(_originalUsername) ? "-" : _originalUsername;

        // Currency Animation
        double prevCurrency = GetCachedCurrency();
        double currentCurrency = _data.currency;
        
        // Premium Currency Animation
        double prevPremium = GetCachedPremiumCurrency();
        double currentPremium = _data.premiumCurrency;

        // Animate Currency
        if (Math.Abs(prevCurrency - currentCurrency) > 0.001f)
        {
            StartAnim(currencyText, prevCurrency, currentCurrency, 0.5f, "F2", ref _currencyAnimRoutine);
            SaveCachedCurrency(currentCurrency);
        }
        else
        {
            if(currencyText) currencyText.text = currentCurrency.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Animate Premium Currency
        if (Math.Abs(prevPremium - currentPremium) > 0.001f)
        {
             StartAnim(premiumCurrencyText, prevPremium, currentPremium, 0.5f, "0", ref _premiumCurrencyAnimRoutine);
             SaveCachedPremiumCurrency((float)currentPremium);
        }
        else
        {
            if(premiumCurrencyText) premiumCurrencyText.text = currentPremium.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    #region Currency Persistence & Helpers

    private double GetCachedCurrency()
    {
        string s = PlayerPrefs.GetString(PREF_KEY_CURRENCY, "0");
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        return 0d;
    }

    private void SaveCachedCurrency(double val)
    {
        PlayerPrefs.SetString(PREF_KEY_CURRENCY, val.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private float GetCachedPremiumCurrency()
    {
        return PlayerPrefs.GetFloat(PREF_KEY_PREM_CURRENCY, 0f);
    }
    
    private void SaveCachedPremiumCurrency(float val)
    {
        PlayerPrefs.SetFloat(PREF_KEY_PREM_CURRENCY, val);
        PlayerPrefs.Save();
    }

    private void StartAnim(TextMeshProUGUI tmp, double startVal, double endVal, float duration, string format, ref Coroutine currRoutine)
    {
        if(tmp == null) return;
        if (currRoutine != null) StopCoroutine(currRoutine);
        if (this.isActiveAndEnabled)
        {
            currRoutine = StartCoroutine(AnimateCountUp(tmp, startVal, endVal, duration, format));
        }
        else
        {
            tmp.text = endVal.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private System.Collections.IEnumerator AnimateCountUp(TextMeshProUGUI tmp, double startVal, double endVal, float duration, string format)
    {
        if (tmp == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            double current = startVal + (endVal - startVal) * progress;
            tmp.text = current.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            yield return null;
        }
        tmp.text = endVal.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion

    #region Username Logic

    private void OnUsernameSubmit(string newName)
    {
        newName = newName.Trim();
        if (string.Equals(newName, _originalUsername, StringComparison.Ordinal))
            return;

        // Validation
        string localErr = ValidateLocal(newName);
        if (localErr != null)
        {
            // Fail immediately
            StartFeedbackCoroutine(false, localErr); 
            return;
        }

        // Start Process
        _ = SubmitUsernameChange(newName);
    }

    private async Task SubmitUsernameChange(string newName)
    {
        // 1. Show "Please wait"
        if (usernameInput != null)
        {
            if (_usernameFeedbackRoutine != null) StopCoroutine(_usernameFeedbackRoutine);
            usernameInput.text = "Please wait...";
            usernameInput.textComponent.color = Color.gray;
            usernameInput.interactable = false; // block inputs
        }
        
        // 2. Server Request
        bool success = await ChangeUsernameAsync(newName);

        // 3. Handle Result
        if (success)
        {
            _originalUsername = newName;
            if (_data != null) _data.username = newName;
            if (usernameLabel) usernameLabel.text = newName;

            StartFeedbackCoroutine(true);
        }
        else
        {
            StartFeedbackCoroutine(false);
        }
    }

    private void StartFeedbackCoroutine(bool success, string overrideMsg = null)
    {
        if (usernameInput == null) return;

        if (_usernameFeedbackRoutine != null) StopCoroutine(_usernameFeedbackRoutine);
        _usernameFeedbackRoutine = StartCoroutine(Co_UsernameFeedback(success, overrideMsg));
    }

    private System.Collections.IEnumerator Co_UsernameFeedback(bool success, string overrideMsg = null)
    {
        // Re-enable input so user sees the color (though interactable=false might dim it, 
        // usually we want to see the text clearly. Let's re-enable interactable but keep focus off or just keep it simple)
        // If interactable is false, TMP often applies a tint. 
        // Let's enable it so the color is vibrant, but user can't type yet because we are in delay.
        usernameInput.interactable = false; 

        // 1. Show Result
        if (success)
        {
            usernameInput.text = "Username Changed";
            usernameInput.textComponent.color = Color.green;
        }
        else
        {
            usernameInput.text = string.IsNullOrEmpty(overrideMsg) ? "Name Change Failed" : overrideMsg;
            usernameInput.textComponent.color = Color.red;
        }
        
        // 2. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 3. Restore
        usernameInput.interactable = true;
        usernameInput.textComponent.color = Color.white;
        
        // Always revert to _originalUsername (which is updated if success)
        usernameInput.text = _originalUsername;
    }

    private string ValidateLocal(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Empty";
        if (name.Length < 3) return "Too short";
        if (name.Length > 20) return "Too long";
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
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Profile] Username change error: {e.Message}");
            return false;
        }
    }

    #endregion

    #endregion
}
