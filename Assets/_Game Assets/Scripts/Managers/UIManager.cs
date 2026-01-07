using System;
using System.Collections;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [Header("UI Elements")]
    public UIMainMenu mainMenu;
    public UIGamePlay gamePlay;
    public UILevelEnd levelEnd;
    public UIOverlay overlay;
    public ProfileAndSettingsPanel profileAndSettingsPanel;
    public UINewHighScorePanel newHighScorePanel;

    public UIGameplayLoading gameplayLoading;
    public InsufficientEnergyPanel insufficientEnergyPanel;
    public UIIAPShopPanel iapShopPanel; // IAP Shop Integration

    [SerializeField] private PhaseEventChannelSO phaseChanged;

    [SerializeField] private CanvasGroup fullscreenFader;
    [SerializeField] private float fullscreenFadeDuration = 0.5f;
    [SerializeField] private bool disableFaderOnComplete = true;

    public UIManager Initialize()
    {
        return this;
    }


    
    private void Start()
    {
        // Ensure UI starts in a clean "Boot" state (all gameplay panels hidden)
        OnPhaseChanged(GamePhase.Boot);
    }
    
    private void OnEnable()
    {
        StopAllCoroutines();

        if (fullscreenFader != null)
        {
            fullscreenFader.alpha = 1f;
            fullscreenFader.blocksRaycasts = true;
            fullscreenFader.interactable = true;
            StartCoroutine(FadeOutFaderCoroutine());
        }

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
                Fade(overlay, true); // Strict Flow: Overlay (TopPanel + Login) must be visible on boot
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
                // Don't force hide gameplayLoading here. It was opened by GameManager just before this phase change.
                // It will close itself when loading completes (UIGameplayLoading logic).
                // Fade(gameplayLoading, false); 
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

    public void ShowIAPShop()
    {
        if (mainMenu != null)
        {
            mainMenu.ShowPanel(UIMainMenu.PanelType.IAPShop);
        }
        else
        {
            Debug.LogError("[UIManager] mainMenu is not assigned!");
        }
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

    /// <summary>
    /// Executes a transition: Fade Out -> Action -> Fade In.
    /// Useful for switching phases or large UI changes without pop-ins.
    /// </summary>
    public void Transition(Action onMidTransition)
    {
        StartCoroutine(TransitionCoroutine(onMidTransition));
    }

    private IEnumerator TransitionCoroutine(Action onMidTransition)
    {
        // 1. Activate Fader
        if (fullscreenFader == null)
        {
            onMidTransition?.Invoke();
            yield break;
        }

        fullscreenFader.gameObject.SetActive(true);
        fullscreenFader.blocksRaycasts = true;
        fullscreenFader.interactable = true;

        // 2. Fade In (Alpha 0 -> 1) i.e., Screen becomes Blocked/Black
        float t = 0f;
        float startAlpha = fullscreenFader.alpha;
        
        while (t < fullscreenFadeDuration)
        {
            t += Time.deltaTime;
            float normalized = t / fullscreenFadeDuration;
            fullscreenFader.alpha = Mathf.Lerp(startAlpha, 1f, normalized);
            yield return null;
        }
        fullscreenFader.alpha = 1f;

        // 3. Perform the Action (Swap panels, load scenes, etc.)
        onMidTransition?.Invoke();

        // 4. Wait a frame to ensure UI updates have processed
        yield return null;

        // 5. Fade Out (Alpha 1 -> 0) i.e., Screen becomes Clear
        t = 0f;
        while (t < fullscreenFadeDuration)
        {
            t += Time.deltaTime;
            float normalized = t / fullscreenFadeDuration;
            fullscreenFader.alpha = Mathf.Lerp(1f, 0f, normalized);
            yield return null;
        }
        fullscreenFader.alpha = 0f;

        // 6. Cleanup
        if (disableFaderOnComplete)
            fullscreenFader.gameObject.SetActive(false);
        
        fullscreenFader.blocksRaycasts = false;
        fullscreenFader.interactable = false;
    }

    private IEnumerator FadeOutFaderCoroutine()
    {
        fullscreenFader.gameObject.SetActive(true);
        fullscreenFader.alpha = 1f;
        float t = 0f;
        while (t < fullscreenFadeDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / fullscreenFadeDuration);
            if (fullscreenFader != null)
                fullscreenFader.alpha = Mathf.Lerp(1f, 0f, normalized);
            yield return null;
        }

        if (fullscreenFader != null)
        {
            fullscreenFader.alpha = 0f;
            fullscreenFader.blocksRaycasts = false;
            fullscreenFader.interactable = false;

            if (disableFaderOnComplete)
                fullscreenFader.gameObject.SetActive(false);
        }
    }
}
