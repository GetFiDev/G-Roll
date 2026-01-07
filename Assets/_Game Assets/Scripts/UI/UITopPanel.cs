using UnityEngine;
using UnityEngine.UI;

public class UITopPanel : MonoBehaviour
{
    public UIBottomPanel bottomPanel;
    public UserStatsDisplayer statsDisplayer;
    public static UITopPanel Instance;

    void Awake()
    {
        statsDisplayer = GetComponent<UserStatsDisplayer>();
        if(Instance==null) Instance=this;
        else{
            Destroy(this.gameObject);
        }
    }

    public void Initialize()
    {
        statsDisplayer.RefreshUserStats();
    }
    
    public void OnSettingsButtonClick()
    {
        bottomPanel.ChangeAllColorToDefault();

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.ProfileAndSettings);
    }

    public void OnProfileButtonClick()
    {
        bottomPanel.ChangeAllColorToDefault();

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.ProfileAndSettings);
    }

    public void OnShopButtonClick()
    {
        // Resets bottom panel colors if needed, though shop behaves as an overlay usually.
        // bottomPanel.ChangeAllColorToDefault(); 
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.IAPShop);
    }

}
