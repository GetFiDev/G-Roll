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

    public UIGameplayLoading gameplayLoading;
    [SerializeField] private PhaseEventChannelSO phaseChanged;

    public UIManager Initialize()
    {
        phaseChanged.OnEvent += OnPhaseChanged;

        return this;
    }

    private void OnDisable()
    {
        phaseChanged.OnEvent -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Boot:
                mainMenu.gameObject.SetActive(false);
                gamePlay.gameObject.SetActive(false);
                levelEnd.gameObject.SetActive(false);
                gameplayLoading.gameObject.SetActive(false);
                break;
            case GamePhase.Meta:
                mainMenu.gameObject.SetActive(true);
                gamePlay.gameObject.SetActive(false);
                levelEnd.gameObject.SetActive(false);
                gameplayLoading.gameObject.SetActive(false);
                break;
            case GamePhase.Gameplay:
                mainMenu.gameObject.SetActive(false);
                gamePlay.gameObject.SetActive(true);
                levelEnd.gameObject.SetActive(false);
                gameplayLoading.gameObject.SetActive(false);
                break;
            default:
                break;
        }
    }

    public void ShowGameplayLoading()
    {
        mainMenu.gameObject.SetActive(false);
        gamePlay.gameObject.SetActive(false);
        levelEnd.gameObject.SetActive(false);
        gameplayLoading.gameObject.SetActive(true);
        gameplayLoading.LoadTheGameplay();
    }

    public void ShowLevelEnd(bool success)
    {
        mainMenu.gameObject.SetActive(false);
        gamePlay.gameObject.SetActive(false);
        levelEnd.gameObject.SetActive(true);
        gameplayLoading.gameObject.SetActive(false);
        levelEnd.ShowSequence(success);
    }
}
