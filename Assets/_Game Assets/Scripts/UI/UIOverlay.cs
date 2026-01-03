using UnityEngine;

public class UIOverlay : MonoBehaviour
{
    [SerializeField] private GameObject elitePassPurchasePanel;
    [SerializeField] private GameObject purchaseCompletedPanel;

    public void ShowProcessingPanel()
    {
        if (elitePassPurchasePanel != null) elitePassPurchasePanel.SetActive(true);
    }

    public void HideProcessingPanel()
    {
        if (elitePassPurchasePanel != null) elitePassPurchasePanel.SetActive(false);
    }

    public void ShowCompletedPanel()
    {
        if (purchaseCompletedPanel != null) purchaseCompletedPanel.SetActive(true);
    }

}
