using TMPro;
using UnityEngine;
using UnityEngine.Events;
public class UIGameplaySettings : MonoBehaviour
{
    [SerializeField] private CanvasGroup settingsPanel;
    
    
    [Header("Resume Countdown")]
    [SerializeField] private CanvasGroup countdownGroup;   // overlay root
    [SerializeField] private TextMeshProUGUI countdownText; // 3,2,1,GO

    [Header("Run Summary UI")]
    [SerializeField] private TextMeshProUGUI earnAmountText;   // "10.0" text in the panel
    [SerializeField] private string earnFormat = "{0:0.0}";
    [SerializeField] private GameplayLogicApplier logicApplier;

    [Header("Events")]
    [SerializeField] private UnityEvent onEndGame;   // invoked when user confirms End Game
    [SerializeField] private UnityEvent onResumeGame; // invoked when gameplay actually resumes

    public void Pause()
    {

        if (earnAmountText == null) return;
        if (string.IsNullOrEmpty(earnFormat))
        {
            earnAmountText.text = logicApplier.Coins.ToString("0.0");
        }
        else
        {
            earnAmountText.text = string.Format(earnFormat, logicApplier.Coins);
        }        Time.timeScale = 0f;

        if (settingsPanel != null)
        {
            settingsPanel.gameObject.SetActive(true);
            settingsPanel.alpha = 1f;
            settingsPanel.interactable = true;
            settingsPanel.blocksRaycasts = true;
        }
    }

    public void Resume()
    {
        // FIX #3: Geri sayım kaldırıldı
        // Direk oyun ekranına dön, kamera durgun, ilk swipe ile devam
        
        StopAllCoroutines();
        
        // Geri sayım overlay'ini GÖSTERMEMİZ
        if (countdownGroup) countdownGroup.gameObject.SetActive(false);
        
        // Pause panelini kapat
        if (settingsPanel)
        {
            settingsPanel.alpha = 0f;
            settingsPanel.interactable = false;
            settingsPanel.blocksRaycasts = false;
        }
        
        // Time.timeScale = 0 kalacak! Kamera ve oyun durmalı
        // İlk swipe gelene kadar durgun kalacak
        Time.timeScale = 0f;
        
        // GameplayLogicApplier'a "first input bekle" moduna geçmesini söyle
        if (logicApplier != null)
        {
            logicApplier.PrepareForPauseResume();
        }
        
        // onResumeGame invoke EdİLMEYECEK - oyun henüz devam etmedi
        // İlk input geldiğinde GameplayLogicApplier.OnFirstInputAfterPauseResume() çağrılacak
    }

    public void EndGame()
    {
        // User chose to finish the run.
        Time.timeScale = 1f;

        // Hide pause/settings panel (keep GameObject active so this behaviour stays valid)
        if (settingsPanel)
        {
            settingsPanel.alpha = 0f;
            settingsPanel.interactable = false;
            settingsPanel.blocksRaycasts = false;
        }

        // Stop any countdown overlay that might be running
        StopAllCoroutines();
        if (countdownGroup) countdownGroup.gameObject.SetActive(false);

        // Notify listeners (gameplay controller, etc.)
        GameplayManager.Instance.StartFailFlow();
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
        if (settingsPanel)
        {
            settingsPanel.alpha = 0f;
            settingsPanel.interactable = false;
            settingsPanel.blocksRaycasts = false;
        }
        Time.timeScale = 1f;
        if (onResumeGame != null) onResumeGame.Invoke();
    }


    // Backward-compat helpers (optional)
    public void Show()  => Pause();
    public void Hide()  => Resume();
}
