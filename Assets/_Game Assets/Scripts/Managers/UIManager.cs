using System;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [Header("UI Elements")]
    public UIMainMenu mainMenu;
    public UIGamePlay gamePlay;
    public UILevelEnd levelEnd;
    public UIOverlay overlay;
    
    public UISettings settings;

    public UIManager Initialize()
    {
        GameManager.Instance.OnGameStateChanged += OnGameStateChanged;

        return this;
    }

    private void OnGameStateChanged()
    {
        switch (GameManager.Instance.GameState)
        {
            case GameState.Loading:
                break;
            case GameState.Ready:
                if (GameSettingsData.Instance.skipReadyState)
                    return;
                
                mainMenu.gameObject.SetActive(true);
                gamePlay.gameObject.SetActive(false);
                levelEnd.gameObject.SetActive(false);
                break;
            case GameState.Gameplay:
                mainMenu.gameObject.SetActive(false);
                gamePlay.gameObject.SetActive(true);
                levelEnd.gameObject.SetActive(false);
                break;
            case GameState.Complete:
                mainMenu.gameObject.SetActive(false);
                gamePlay.gameObject.SetActive(false);
                levelEnd.gameObject.SetActive(true);
                levelEnd.Show(true);
                break;
            case GameState.Fail:
                mainMenu.gameObject.SetActive(false);
                gamePlay.gameObject.SetActive(false);
                levelEnd.gameObject.SetActive(true);
                levelEnd.Show(false);
                break;
            default:
                break;
        }
    }
}
