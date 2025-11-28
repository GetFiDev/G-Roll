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
        // Helper to fade
        void Fade(Component comp, bool show)
        {
            if (comp == null) return;
            var fp = comp.GetComponent<UIFadePanel>();
            if (fp == null) fp = comp.gameObject.AddComponent<UIFadePanel>();
            
            if (show) fp.Show();
            else fp.Hide();
        }

        switch (phase)
        {
            case GamePhase.Boot:
                Fade(mainMenu, false);
                Fade(gamePlay, false);
                Fade(levelEnd, false);
                Fade(overlay, false);
                Fade(gameplayLoading, false);
                break;
            case GamePhase.Meta:
                Fade(mainMenu, true);
                Fade(gamePlay, false);
                Fade(levelEnd, false);
                Fade(overlay, true);
                Fade(gameplayLoading, false);
                break;
            case GamePhase.Gameplay:
                Fade(mainMenu, false);
                Fade(gamePlay, true);
                Fade(levelEnd, false);
                Fade(overlay, false);
                Fade(gameplayLoading, false);
                break;
            default:
                break;
        }
    }

    public void ShowGameplayLoading()
    {
        void Fade(Component comp, bool show)
        {
            if (comp == null) return;
            var fp = comp.GetComponent<UIFadePanel>();
            if (fp == null) fp = comp.gameObject.AddComponent<UIFadePanel>();
            if (show) fp.Show(); else fp.Hide();
        }

        Fade(mainMenu, false);
        Fade(gamePlay, true); // HUD stays visible? Original code said yes.
        Fade(levelEnd, false);
        Fade(gameplayLoading, true);
    }

    public void ShowLevelEnd(bool success)
    {
        void Fade(Component comp, bool show)
        {
            if (comp == null) return;
            var fp = comp.GetComponent<UIFadePanel>();
            if (fp == null) fp = comp.gameObject.AddComponent<UIFadePanel>();
            if (show) fp.Show(); else fp.Hide();
        }

        Fade(mainMenu, false);
        Fade(gamePlay, false);
        Fade(levelEnd, true);
        Fade(gameplayLoading, false);

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
