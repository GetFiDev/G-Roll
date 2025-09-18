using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISetNamePanel : MonoBehaviour
{
    [Header("Refs")]
    public UserDataEditHandler handler; // LoginHandler buraya da atayabilir (opsiyonel)
    public TMP_InputField nameInput;
    public Button doneButton;

    [Header("Rules")]
    public int minLength = 1;

    void Awake()
    {
        if (doneButton) doneButton.interactable = false;
        if (nameInput) nameInput.onValueChanged.AddListener(Validate);
    }

    void OnDestroy()
    {
        if (nameInput) nameInput.onValueChanged.RemoveListener(Validate);
    }

    void Validate(string text)
    {
        if (!doneButton) return;
        var t = (text ?? "").Trim();
        doneButton.interactable = t.Length >= minLength;
    }

    public string CurrentName => (nameInput?.text ?? "").Trim();

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
