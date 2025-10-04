using UnityEngine;
using UnityEngine.UI;

public class UIBottomPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Image homeIcon;
    
    [SerializeField] private Image settingsIcon;
    
    [SerializeField] private Image shopIcon;

    [SerializeField] private Image referralIcon;

    [Header("Settings")]
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color selectedColor;

    [Header("Channels")]
    [SerializeField] private VoidEventChannelSO requestStartGameplay;

    public UITopPanel topPanel;


    private void Start()
    {
        OnClickHomeButton();
    }

    public void OnClickHomeButton()
    {
        ChangeAllColorToDefault();
        topPanel.ChangeAllColorToDefault();
        
        homeIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Home);
    }

    public void OnClickCustomizationButton()
    {
        ChangeAllColorToDefault();
        topPanel.ChangeAllColorToDefault();
        
        settingsIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Customization);
    }

    public void OnClickPlayButton()
    {
        ChangeAllColorToDefault();
        topPanel.ChangeAllColorToDefault();

        // Phase değişimini GameManager yönetsin; UI sadece isteği yayınlar
        if (requestStartGameplay != null)
            requestStartGameplay.Raise();
    }

    public void OnClickShopButton()
    {
        ChangeAllColorToDefault();
        topPanel.ChangeAllColorToDefault();
        
        shopIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Shop);
    }

    public void OnClickRankingButton()
    {
        ChangeAllColorToDefault();
        topPanel.ChangeAllColorToDefault();
        
        referralIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Ranking);
    }

    public void ChangeAllColorToDefault()
    {
        homeIcon.color = defaultColor;
        settingsIcon.color = defaultColor;
        shopIcon.color = defaultColor;
        referralIcon.color = defaultColor;
    }
}
