using System;
using System.Collections.Generic;
using UnityEngine;

public class UIMainMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private UIHomePanel homePanel;
    [SerializeField] private UISettingsPanel settingsPanel;
    [SerializeField] private UIShopPanel shopPanel;
    [SerializeField] private UIReferralPanel referralPanel;
    [SerializeField] private UICustomizationPanel customizationPanel;
    [SerializeField] private UIRankingPanel rankingPanel;
    [SerializeField] private UITaskPanel TaskPanel;
    [SerializeField] private UIProfilePanel profilePanel;


    [Header("Elements")]
    [SerializeField] private UITopPanel topPanel;
    [SerializeField] private UIBottomPanel bottomPanel;

    public enum PanelType
    {
        Home, Settings, Shop, Referral, Customization, Ranking, Task, Profile
    }

    private Dictionary<PanelType, GameObject> panels;

    private void Awake()
    {
        panels = new Dictionary<PanelType, GameObject>
        {
            { PanelType.Home, homePanel.gameObject },
            { PanelType.Settings, settingsPanel.gameObject },
            { PanelType.Shop, shopPanel.gameObject },
            { PanelType.Referral, referralPanel.gameObject },
            { PanelType.Customization, customizationPanel.gameObject },
            { PanelType.Ranking, rankingPanel.gameObject },
            { PanelType.Task, TaskPanel.gameObject },
            { PanelType.Profile, profilePanel.gameObject }
        };
    }

    public void ShowPanel(PanelType type)
    {
        foreach (var panel in panels.Values)
            panel.SetActive(false);

        panels[type].SetActive(true);
    }

    public void OnReferralsPanelClick()
    {
        topPanel.ChangeAllColorToDefault();
        bottomPanel.ChangeAllColorToDefault();
        ShowPanel(PanelType.Referral);
    }

    public void OnTaskPanelClick()
    {
        topPanel.ChangeAllColorToDefault();
        bottomPanel.ChangeAllColorToDefault();
        ShowPanel(PanelType.Task);
    }
}
