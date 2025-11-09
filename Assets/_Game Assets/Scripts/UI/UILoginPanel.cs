using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UILoginPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject loginGroup;
    [SerializeField] private GameObject registerGroup;


    [SerializeField] private GameObject manuelLoginPanel;

    private void Awake()
    {
        gameObject.SetActive(true);
    }

    public void ShowRegisterGroup()
    {
        DeactivateAllLoginGroups();
        registerGroup.SetActive(true);
    }
    public void ShowLoginGroup()
    {
        DeactivateAllLoginGroups();
        loginGroup.SetActive(true);
    }
    public void DeactivateAllLoginGroups()
    {
        loginGroup.SetActive(false);
        registerGroup.SetActive(false);
    }
    public void TurnBackToAuthPanel()
    {
        loginGroup.SetActive(false);
        registerGroup.SetActive(false);
    }

    public void CloseManualLoginPanel()
    {
        canvasGroup.DOKill();
        canvasGroup.DOFade(0, 0.25f).SetDelay(.75f).SetEase(Ease.OutCubic)
            .OnComplete(() => gameObject.SetActive(false));
    }

    public void ShowManuelLoginPanelButtonClick()
    {
        DeactivateAllLoginGroups();
        ShowLoginGroup();
        manuelLoginPanel.SetActive(true);
    }

}