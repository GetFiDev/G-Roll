using UnityEngine;
using UnityEngine.UI;

public class UIBottomPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Image homeIcon;    
    [SerializeField] private Image shopIcon;
    [SerializeField] private Image referralIcon;
    [SerializeField] private Image rankingIcon;
    [SerializeField] private Image achievementsIcon;



    [Header("Settings")]
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color selectedColor;

    [Header("Channels")]
    [SerializeField] private VoidEventChannelSO requestStartGameplay;

    public UITopPanel topPanel;
    public static UIBottomPanel Instance;
    public void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(this);
        }
    }


    public void OnClickHomeButton()
    {
        ChangeAllColorToDefault();
        
        homeIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Home);
    }

    public void OnClickPlayButton()
    {
        ChangeAllColorToDefault();

        // Phase değişimini GameManager yönetsin; UI sadece isteği yayınlar
        if (requestStartGameplay != null)
            requestStartGameplay.Raise();
    }

    public void OnClickShopButton()
    {
        ChangeAllColorToDefault();

        shopIcon.color = selectedColor;

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Shop);
        NotificationBadgeManager.Instance.MarkAllShopItemsSeen();
    }

    public void OnClickReferralButton()
    {
        ChangeAllColorToDefault();

        referralIcon.color = selectedColor;

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Referral);
    }
    
    public void OnClickAchievementsButton()
    {
        ChangeAllColorToDefault();

        achievementsIcon.color = selectedColor;

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Task);
        _ = NotificationBadgeManager.Instance.RefreshAchievementBadges();
    }

    public void OnClickRankingButton()
    {
        ChangeAllColorToDefault();
        
        rankingIcon.color = selectedColor;
        
        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Ranking);
    }

    public void ChangeAllColorToDefault()
    {
        homeIcon.color = defaultColor;
        shopIcon.color = defaultColor;
        referralIcon.color = defaultColor;
        rankingIcon.color = defaultColor;
        achievementsIcon.color = defaultColor;
    }
}
