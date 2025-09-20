using UnityEngine;

public class UISettings : MonoBehaviour
{
    [SerializeField] private CanvasGroup settingsPanel;
    
    [SerializeField] private ToggleButton hapticToggle;
    [SerializeField] private ToggleButton soundToggle;
    [SerializeField] private ToggleButton musicToggle;
        
    private void Start()
    {
        hapticToggle.SetValue(DataManager.Vibration);
        soundToggle.SetValue(DataManager.Sound);
        musicToggle.SetValue(DataManager.Music);
    }
    
    public void Show()
    {
        GameManager.Instance.PauseGame();
        settingsPanel.gameObject.SetActive(true);
    }

    public void Hide()
    {
        GameManager.Instance.ResumeGame();
        settingsPanel.gameObject.SetActive(false);
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
}
