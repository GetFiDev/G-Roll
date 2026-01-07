using UnityEngine;
using UnityEngine.UI;
using NetworkingData;

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
    
    private void OnEnable()
    {
        if (UserDatabaseManager.Instance != null)
        {
            UserDatabaseManager.Instance.OnUserDataSaved += OnUserDataUpdated;
        }
    }

    private void OnDisable()
    {
        if (UserDatabaseManager.Instance != null)
        {
            UserDatabaseManager.Instance.OnUserDataSaved -= OnUserDataUpdated;
        }
    }

    private void OnUserDataUpdated(UserData data)
    {
        // User requested a quick toggle to force refresh logic that relies on OnEnable
        gameObject.SetActive(false);
        gameObject.SetActive(true);
        
        if (statsDisplayer != null)
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
