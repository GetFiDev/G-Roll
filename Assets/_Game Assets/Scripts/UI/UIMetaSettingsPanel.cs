using UnityEngine;

public class UIMetaSettingsPanel : MonoBehaviour
{
    [SerializeField] private ToggleButton hapticToggle;
    [SerializeField] private ToggleButton soundToggle;
    [SerializeField] private ToggleButton musicToggle;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
    
    private void Start()
    {
        hapticToggle.SetValue(DataManager.Vibration);
        soundToggle.SetValue(DataManager.Sound);
        musicToggle.SetValue(DataManager.Music);
    }
    
    public void OnSoundToggled(bool value)
    {
        DataManager.Sound = value;
        
        GameManager.Instance.audioManager.UpdateAudioStates();
    }

    public void OnMusicToggled(bool value)
    {
        DataManager.Music = value;
        
        GameManager.Instance.audioManager.UpdateAudioStates();
    }

    public void OnHapticToggled(bool value)
    {
        DataManager.Vibration = value;
        
        HapticManager.SetHapticsActive(value);
    }

    public void OnPrivacyPolicyButtonClicked()
    {
        Application.OpenURL("https://www.google.com/");
    }
}
