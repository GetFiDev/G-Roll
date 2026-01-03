using System;
using System.Collections.Generic;
using UnityEngine;

public class UIMainMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private UIHomePanel homePanel;

    [SerializeField] private UIShopPanel shopPanel;
    [SerializeField] private UIReferralPanel referralPanel;
    [SerializeField] private UIRankingPanel rankingPanel;
    [SerializeField] private UITaskPanel TaskPanel;

    [SerializeField] private ProfileAndSettingsPanel profileAndSettingsPanel;
    [SerializeField] private UIAutoPilotPanel autoPilotpanel;
    [SerializeField] private UIElitePassPanel elitePassPanel;
    [SerializeField] private UIIAPShopPanel iapShopPanel; // Added IAP Shop Panel



    [Header("Elements")]
    [SerializeField] private UITopPanel topPanel;
    [SerializeField] private UIBottomPanel bottomPanel;

    public enum PanelType
    {
        Home, Shop, Referral, Customization, Ranking, Task, ProfileAndSettings, ElitePass, AutoPilot, IAPShop
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
            { PanelType.Shop, GetOrAdd(shopPanel.gameObject) },
            { PanelType.Referral, GetOrAdd(referralPanel.gameObject) },
            { PanelType.Ranking, GetOrAdd(rankingPanel.gameObject) },
            { PanelType.Task, GetOrAdd(TaskPanel.gameObject) },
            { PanelType.ProfileAndSettings, GetOrAdd(profileAndSettingsPanel.gameObject) },
            { PanelType.ElitePass, GetOrAdd(elitePassPanel.gameObject) },
            { PanelType.AutoPilot, GetOrAdd(autoPilotpanel.gameObject) },
            { PanelType.IAPShop, GetOrAdd(iapShopPanel.gameObject) }
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

    public async void OnElitePassButtonClick()
    {
        try
        {
            // AutopilotService statik bir servis; durumu almak için GetStatusAsync kullanılmalı
            var status = await AutopilotService.GetStatusAsync();

            // Kullanıcı elite pass sahibiyse panel açılmayacak
            if (status != null && status.isElite)
            {
                return;
            }

            // Elite değilse panel toggle yapılacak
            TogglePanel(PanelType.ElitePass);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UIMainMenu] Failed to get autopilot status: {ex.Message}");
            // İstersen burada fallback olarak yine de paneli açabiliriz.
            // TogglePanel(PanelType.ElitePass);
        }
    }
    public void OnAutoPilotInfoButtonClick()
    {
        TogglePanel(PanelType.AutoPilot);
    }

    public void OnIAPShopButtonClick()
    {
        ShowPanel(PanelType.IAPShop);
    }

    public void OnTaskButtonClick()
    {
        ShowPanel(PanelType.Task);
    }
}
