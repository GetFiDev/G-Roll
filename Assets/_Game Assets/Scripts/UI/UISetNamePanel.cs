using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISetNamePanel : MonoBehaviour
{
    [Header("Refs")]
    public UserDataEditHandler handler; // LoginHandler buraya da atayabilir (opsiyonel)
    public TMP_InputField nameInput;
    public TMP_InputField referralInput; // User requested optional referral field
    public Button doneButton;

    [Header("Rules")]
    public int minLength = 1;

    void Awake()
    {
        if (doneButton) doneButton.interactable = false;
        if (nameInput) nameInput.onValueChanged.AddListener(Validate);
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
}
