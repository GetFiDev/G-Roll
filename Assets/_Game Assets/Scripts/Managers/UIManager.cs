using System;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [Header("UI Elements")]
    public UIMainMenu mainMenu;
    public UIGamePlay gamePlay;
    public UILevelEnd levelEnd;
    public UIOverlay overlay;
    public UIMetaSettingsPanel metaSettingsPanel;
    public UINewHighScorePanel newHighScorePanel;

    public UIGameplayLoading gameplayLoading;
    [SerializeField] private PhaseEventChannelSO phaseChanged;

    public UIManager Initialize()
    {
        return this;
    }

    private void OnEnable()
    {
        if (phaseChanged != null)
            phaseChanged.OnEvent += OnPhaseChanged;
        else
            Debug.LogError("[UIManager] phaseChanged (PhaseEventChannelSO) is not assigned.");
    }

    private void OnDisable()
    {
        if (phaseChanged != null)
            phaseChanged.OnEvent -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Boot:
                if (mainMenu)        mainMenu.gameObject.SetActive(false);
                if (gamePlay)        gamePlay.gameObject.SetActive(false);
                if (levelEnd) levelEnd.gameObject.SetActive(false);
                if (overlay) overlay.gameObject.SetActive(false);
                if (gameplayLoading) gameplayLoading.gameObject.SetActive(false);
                break;
            case GamePhase.Meta:
                if (mainMenu)        mainMenu.gameObject.SetActive(true);
                if (gamePlay)        gamePlay.gameObject.SetActive(false);
                if (levelEnd) levelEnd.gameObject.SetActive(false);
                if (overlay) overlay.gameObject.SetActive(true);
                if (gameplayLoading) gameplayLoading.gameObject.SetActive(false);
                break;
            case GamePhase.Gameplay:
                if (mainMenu)        mainMenu.gameObject.SetActive(false);
                if (gamePlay)        gamePlay.gameObject.SetActive(true);
                if (levelEnd) levelEnd.gameObject.SetActive(false);
                if (overlay) overlay.gameObject.SetActive(false);
                if (gameplayLoading) gameplayLoading.gameObject.SetActive(false);
                break;
            default:
                break;
        }
    }

    public void ShowGameplayLoading()
    {
        if (mainMenu)        mainMenu.gameObject.SetActive(false);
        if (gamePlay)        gamePlay.gameObject.SetActive(true);   // HUD açık kalsın; overlay üstüne çıkar
        if (levelEnd)        levelEnd.gameObject.SetActive(false);
        if (gameplayLoading) gameplayLoading.gameObject.SetActive(true);
        // UI tarafı yüklemeyi başlatmaz; GameplayManager mapLoader.Load() çağırır
    }

    public void ShowLevelEnd(bool success)
    {
        if (mainMenu)        mainMenu.gameObject.SetActive(false);
        if (gamePlay)        gamePlay.gameObject.SetActive(false);
        if (levelEnd)        levelEnd.gameObject.SetActive(true);
        if (gameplayLoading) gameplayLoading.gameObject.SetActive(false);

        if (levelEnd)
            levelEnd.ShowSequence(success);
        else
            Debug.LogError("[UIManager] levelEnd is not assigned.");
    }

    public void ShowNewHighScore(double score, Action onClosed)
    {
        if (newHighScorePanel)
        {
            // Clean previous listeners to avoid duplicates if reused
            newHighScorePanel.OnClosed -= OnHighScorePanelClosed;
            newHighScorePanel.OnClosed += OnHighScorePanelClosed;
            
            // Store the callback temporarily or use a lambda wrapper if simpler, 
            // but here we need to bridge the event.
            // Simpler approach: Pass the callback to the panel? 
            // Or just handle it here. 
            // Let's use a local wrapper.
            
            Action wrapper = null;
            wrapper = () => {
                newHighScorePanel.OnClosed -= wrapper;
                onClosed?.Invoke();
            };
            newHighScorePanel.OnClosed += wrapper;

            newHighScorePanel.Show(score);
        }
        else
        {
            onClosed?.Invoke(); // Fallback if panel missing
        }
    }

    private void OnHighScorePanelClosed() { } // Dummy target for -= check if needed, or just use lambda above
}
