using System.Collections.Generic;
using UnityEngine;

public class UIMainMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private UIHomePanel homePanel;
    [SerializeField] private UISettingsPanel settingsPanel;
    [SerializeField] private UIShopPanel shopPanel;
    [SerializeField] private UIReferralPanel referralPanel;

    [Header("Elements")]
    [SerializeField] private UITopPanel topPanel;
    [SerializeField] private UIBottomPanel bottomPanel;
    
    public enum PanelType
    {
        Home,
        Settings,
        Shop,
        Referral
    }
    
    private Dictionary<PanelType, GameObject> panels;

    private void Awake()
    {
        panels = new Dictionary<PanelType, GameObject>
        {
            { PanelType.Home, homePanel.gameObject },
            { PanelType.Settings, settingsPanel.gameObject },
            { PanelType.Shop, shopPanel.gameObject },
            { PanelType.Referral, referralPanel.gameObject }
        };
    }

    public void ShowPanel(PanelType type)
    {
        foreach (var panel in panels.Values)
            panel.SetActive(false);

        panels[type].SetActive(true);
    }
}
