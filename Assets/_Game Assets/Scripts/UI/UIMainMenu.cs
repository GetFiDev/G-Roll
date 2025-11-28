using System;
using System.Collections.Generic;
using UnityEngine;

public class UIMainMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private UIHomePanel homePanel;
    [SerializeField] private UIMetaSettingsPanel settingsPanel;
    [SerializeField] private UIShopPanel shopPanel;
    [SerializeField] private UIReferralPanel referralPanel;
    [SerializeField] private UIRankingPanel rankingPanel;
    [SerializeField] private UITaskPanel TaskPanel;
    [SerializeField] private UIProfilePanel profilePanel;
    [SerializeField] private UIAutoPilotPanel autoPilotpanel;
    [SerializeField] private UIElitePassPanel elitePassPanel;



    [Header("Elements")]
    [SerializeField] private UITopPanel topPanel;
    [SerializeField] private UIBottomPanel bottomPanel;

    public enum PanelType
    {
        Home, Settings, Shop, Referral, Customization, Ranking, Task, Profile, ElitePass, AutoPilot
    }

    private Dictionary<PanelType, UIFadePanel> panels;

    private void Awake()
    {
        // Helper to get or add UIFadePanel
        UIFadePanel GetOrAdd(GameObject go)
        {
            if (go == null) return null;
            var fade = go.GetComponent<UIFadePanel>();
            if (fade == null) fade = go.AddComponent<UIFadePanel>();
            return fade;
        }

        panels = new Dictionary<PanelType, UIFadePanel>
        {
            { PanelType.Home, GetOrAdd(homePanel.gameObject) },
            { PanelType.Settings, GetOrAdd(settingsPanel.gameObject) },
            { PanelType.Shop, GetOrAdd(shopPanel.gameObject) },
            { PanelType.Referral, GetOrAdd(referralPanel.gameObject) },
            { PanelType.Ranking, GetOrAdd(rankingPanel.gameObject) },
            { PanelType.Task, GetOrAdd(TaskPanel.gameObject) },
            { PanelType.Profile, GetOrAdd(profilePanel.gameObject) },
            { PanelType.ElitePass, GetOrAdd(elitePassPanel.gameObject) },
            { PanelType.AutoPilot, GetOrAdd(autoPilotpanel.gameObject) }
        };
    }

    public void ShowPanel(PanelType type)
    {
        foreach (var kvp in panels)
        {
            if (kvp.Key == type)
                kvp.Value.Show();
            else
                kvp.Value.Hide();
        }
    }

    public void TogglePanel(PanelType type)
    {
        // Toggle logic: if active, hide. If inactive, show (and hide others? usually yes for main panels)
        // Assuming Toggle is for overlays like Settings/ElitePass on top of others?
        // Or is it switching main views? 
        // Based on usage (OnElitePassButtonClick), it seems to be an overlay or modal.
        // Let's check if it's currently visible.
        
        // Note: We can't easily check 'isVisible' from outside without casting, 
        // but we can check gameObject.activeSelf as a proxy if UIFadePanel manages it correctly.
        
        var panel = panels[type];
        if (panel.gameObject.activeSelf) 
        {
            panel.Hide();
        }
        else 
        {
            panel.Show();
            // Optional: Hide others if this is a main panel switch? 
            // For now, keep original behavior (just toggle this one).
        };
    }

    public void OnElitePassButtonClick()
    {
        TogglePanel(PanelType.ElitePass);
    }
    public void OnAutoPilotInfoButtonClick()
    {
        TogglePanel(PanelType.AutoPilot);
    }
    
}
