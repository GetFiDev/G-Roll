using DG.Tweening;
using TMPro;
using UnityEngine;

public class UILoginPanel : MonoBehaviour
{
    [SerializeField] private GameObject manuelLoginPanel;

    [SerializeField] private TextMeshProUGUI loginErrorText;
    
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
        gameObject.SetActive(false);
    }

    private void ShowErrorText()
    {
        loginErrorText.DOKill();
        
        loginErrorText.color = Color.red;
        loginErrorText.DOFade(0, .5f).SetDelay(.5f).SetEase(Ease.OutCubic);
    }
}