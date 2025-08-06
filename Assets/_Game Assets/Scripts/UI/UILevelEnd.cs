using UnityEngine;

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