using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UILevelEnd : MonoBehaviour
{
    [Header("Success Panel")] [SerializeField]
    private GameObject successPanel;

    [SerializeField] private List<Sprite> successIcons;
    [SerializeField] private Image successIconImage;
    [SerializeField] private GameObject successShine;

    [Header("Fail Panel")] [SerializeField]
    private GameObject failPanel;

    [SerializeField] private List<Sprite> failIcons;
    [SerializeField] private Image failIconImage;

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
        successIconImage.sprite = successIcons.RandomItem();
        successIconImage.transform.localScale = Vector3.zero;
        successIconImage.transform.DOScale(1, .2f)
            .SetDelay(.25f)
            .SetEase(Ease.OutBack);

        successShine.transform.localScale = Vector3.zero;
        successShine.transform.DOScale(1, .5f)
            .SetDelay(.25f);

        successPanel.SetActive(true);
    }

    private void ShowFailPanel()
    {
        failPanel.SetActive(true);
    }

    public void OnNextLevelButtonClick()
    {
        LevelManager.ReloadScene();
    }

    public void OnRestartLevelButtonClick()
    {
        LevelManager.ReloadScene();
    }
}