using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UISetNamePanel : MonoBehaviour
{
    [Header("Refs")]
    public UserDataEditHandler handler; // LoginHandler buraya da atayabilir (opsiyonel)
    public TMP_InputField nameInput;
    public TMP_InputField referralInput; // User requested optional referral field
    public Button doneButton;

    [Header("Rules")]
    public int minLength = 1;

    // Store original input state for restoration
    private string _originalPlaceholder;
    private string _storedUserInput;
    private Coroutine _errorCoroutine;

    void Awake()
    {
        if (doneButton) doneButton.interactable = false;
        if (nameInput) 
        {
            nameInput.onValueChanged.AddListener(Validate);
            // Store original placeholder
            if (nameInput.placeholder is TMP_Text placeholderText)
                _originalPlaceholder = placeholderText.text;
        }
        // Referral is optional, need not validate? Or maybe just re-trigger validate?
        if (referralInput) referralInput.onValueChanged.AddListener((_) => Validate(nameInput.text));
    }

    void OnDestroy()
    {
        if (nameInput) nameInput.onValueChanged.RemoveListener(Validate);
        // Clean up referral listener if added
        if (referralInput) referralInput.onValueChanged.RemoveAllListeners();
    }

    void Validate(string text)
    {
        if (!doneButton) return;
        var t = (text ?? "").Trim();
        doneButton.interactable = t.Length >= minLength;
    }

    public string CurrentName => (nameInput?.text ?? "").Trim();
    public string CurrentReferral => (referralInput?.text ?? "").Trim();

    public void Open()
    {
        gameObject.SetActive(true);
        Validate(nameInput ? nameInput.text : "");
        nameInput?.ActivateInputField();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // --- Error Display Methods ---
    
    /// <summary>
    /// Show "Please wait..." in the input field while waiting for server response
    /// </summary>
    public void ShowWaiting()
    {
        if (nameInput == null) return;
        
        // Stop any existing error coroutine
        if (_errorCoroutine != null)
            StopCoroutine(_errorCoroutine);
        
        // Store user's input for restoration
        _storedUserInput = nameInput.text;
        
        // Disable input and button
        nameInput.interactable = false;
        if (doneButton) doneButton.interactable = false;
        
        // Show waiting message
        nameInput.text = "";
        if (nameInput.placeholder is TMP_Text placeholderText)
            placeholderText.text = "Please wait...";
    }

    /// <summary>
    /// Show error message in the input field
    /// </summary>
    public void ShowError(string errorCode)
    {
        if (nameInput == null) return;
        
        // Stop any existing error coroutine
        if (_errorCoroutine != null)
            StopCoroutine(_errorCoroutine);

        // Get localized error message
        string errorMessage = GetErrorMessage(errorCode);
        
        // Show error in placeholder
        nameInput.text = "";
        if (nameInput.placeholder is TMP_Text placeholderText)
            placeholderText.text = errorMessage;
        
        // Start coroutine to clear after 2 seconds
        _errorCoroutine = StartCoroutine(ClearErrorAfterDelay(2f));
    }

    /// <summary>
    /// Clear error and restore input field to normal state
    /// </summary>
    public void ClearError()
    {
        if (nameInput == null) return;
        
        // Restore original placeholder
        if (nameInput.placeholder is TMP_Text placeholderText)
            placeholderText.text = _originalPlaceholder ?? "Enter username";
        
        // Restore user's input if we had stored it
        if (!string.IsNullOrEmpty(_storedUserInput))
        {
            nameInput.text = _storedUserInput;
            _storedUserInput = null;
        }
        
        // Re-enable input
        nameInput.interactable = true;
        
        // Re-validate to update button state
        Validate(nameInput.text);
    }

    private IEnumerator ClearErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearError();
        _errorCoroutine = null;
    }

    private string GetErrorMessage(string errorCode)
    {
        return errorCode switch
        {
            "USERNAME_TAKEN" => "Username already exists",
            "USERNAME_INVALID_LENGTH" => "Username must be 3-20 characters",
            "USERNAME_INVALID_CHARS" => "Only letters, numbers, . _ - allowed",
            "USERNAME_BAD_WORD" => "Username contains inappropriate content",
            "PROFILE_ALREADY_COMPLETE" => "Profile already completed",
            "NO_LOGIN" => "Please login first",
            _ => "An error occurred, please try again"
        };
    }
}
