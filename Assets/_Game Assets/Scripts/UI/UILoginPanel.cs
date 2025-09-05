using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UILoginPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject manuelLoginPanel;

    [SerializeField] private TextMeshProUGUI loginErrorText;

    private static bool isShown = false;

    private void Awake()
    {
        if (isShown)
        {
            gameObject.SetActive(false);
        }
        
        isShown = true;
    }

    public void SignInWithGoogleButtonClick()
    {
        ShowErrorText();
    }

    public void SignInWithAppleButtonClick()
    {
        ShowErrorText();
    }

    public void ShowManuelLoginPanelButtonClick()
    {
        manuelLoginPanel.SetActive(true);
    }

    public void ManuelLoginButtonClick()
    {
        canvasGroup.DOKill();
        canvasGroup.DOFade(0, 0.25f).SetDelay(.75f).SetEase(Ease.OutCubic)
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void ShowErrorText()
    {
        loginErrorText.DOKill();

        loginErrorText.color = Color.red;
        loginErrorText.DOFade(0, .5f).SetDelay(.5f).SetEase(Ease.OutCubic);
    }
}