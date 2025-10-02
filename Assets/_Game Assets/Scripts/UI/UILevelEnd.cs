using UnityEngine;
using DG.Tweening;

public class UILevelEnd : MonoBehaviour
{
    [Header("Success Panel")] [SerializeField]
    private GameObject successPanel;

    [Header("Fail Panel")] [SerializeField]
    private GameObject failPanel;

    public void Show(bool isSuccess)
    {
        if (isSuccess)
        {
            ShowSuccessPanel();
        }
        else
        {
            ShowFailPanel();
        }
    }

    private void ShowSuccessPanel()
    {
        successPanel.GetComponent<CanvasGroup>().alpha = 0;
        successPanel.SetActive(true);
        successPanel.GetComponent<CanvasGroup>().DOFade(1, 0.5f);
    }

    private void ShowFailPanel()
    {
        failPanel.GetComponent<CanvasGroup>().alpha = 0;
        failPanel.SetActive(true);
        failPanel.GetComponent<CanvasGroup>().DOFade(1, 0.5f);
    }

    public void OnDieButtonClicked()
    {
        GameManager.Instance.ReturnToMetaScene();
    }

    public void OnRestartLevelButtonClick()
    {
        //LevelManager.ReloadScene();
    }
}