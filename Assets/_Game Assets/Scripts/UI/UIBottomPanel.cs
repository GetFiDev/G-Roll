using UnityEngine;
using UnityEngine.UI;

public class UIBottomPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Image homeIcon;
    
    [SerializeField] private Button settingsButton;
    [SerializeField] private Image settingsIcon;
    
    [SerializeField] private Button shopButton;
    [SerializeField] private Image shopIcon;

    [SerializeField] private Button referralButton;
    [SerializeField] private Image referralIcon;

    [Header("Settings")]
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color selectedColor;

    private void Start()
    {
        OnClickHomeButton();
    }

    public void OnClickHomeButton()
    {
        ChangeAllColorToDefault();
        
        homeIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Home);
    }

    public void OnClickSettingsButton()
    {
        ChangeAllColorToDefault();
        
        settingsIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Settings);
    }

    public void OnClickPlayButton()
    {
        ChangeAllColorToDefault();
        
        GameManager.Instance.LevelStart();
    }

    public void OnClickShopButton()
    {
        ChangeAllColorToDefault();
        
        shopIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Shop);
    }

    public void OnClickReferralButton()
    {
        ChangeAllColorToDefault();
        
        referralIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Referral);
    }

    private void ChangeAllColorToDefault()
    {
        homeIcon.color = defaultColor;
        settingsIcon.color = defaultColor;
        shopIcon.color = defaultColor;
        referralIcon.color = defaultColor;
    }
}
