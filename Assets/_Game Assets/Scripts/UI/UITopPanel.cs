using UnityEngine;
using UnityEngine.UI;

public class UITopPanel : MonoBehaviour
{
    public UIBottomPanel bottomPanel;
    [SerializeField] private UserStatsDisplayer statsDisplayer;

    void Awake()
    {
        statsDisplayer = GetComponent<UserStatsDisplayer>();
    }


    public void Initialize()
    {
        statsDisplayer.RefreshUserStats();
    }
    
    public void OnSettingsButtonClick()
    {
        bottomPanel.ChangeAllColorToDefault();

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Settings);
    }

    public void OnProfileButtonClick()
    {
        bottomPanel.ChangeAllColorToDefault();

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Profile);
    }

}
