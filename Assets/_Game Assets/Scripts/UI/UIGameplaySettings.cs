using TMPro;
using UnityEngine;

public class UIGameplaySettings : MonoBehaviour
{
    [SerializeField] private CanvasGroup settingsPanel;
    
    
    [Header("Resume Countdown")]
    [SerializeField] private CanvasGroup countdownGroup;   // overlay root
    [SerializeField] private TextMeshProUGUI countdownText; // 3,2,1,GO
        

    public void Pause()
    {
        // Global pause (gameplay domain bağımsız)
        Time.timeScale = 0f;
        if (settingsPanel) settingsPanel.gameObject.SetActive(true);
    }

    public void Resume()
    {
        if (settingsPanel) settingsPanel.gameObject.SetActive(false);
        StopAllCoroutines();
        StartCoroutine(Co_ResumeCountdown());
    }

    private System.Collections.IEnumerator Co_ResumeCountdown()
    {
        if (countdownGroup) countdownGroup.gameObject.SetActive(true);
        if (countdownText) countdownText.text = "3";
        yield return new WaitForSecondsRealtime(1f);
        if (countdownText) countdownText.text = "2";
        yield return new WaitForSecondsRealtime(1f);
        if (countdownText) countdownText.text = "1";
        yield return new WaitForSecondsRealtime(1f);
        if (countdownText) countdownText.text = "GO";
        yield return new WaitForSecondsRealtime(0.5f);
        if (countdownGroup) countdownGroup.gameObject.SetActive(false);
        Time.timeScale = 1f;
    }


    // Backward-compat helpers (optional)
    public void Show()  => Pause();
    public void Hide()  => Resume();
}
