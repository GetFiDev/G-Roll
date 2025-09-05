using UnityEngine;

public class UIShopPanel : MonoBehaviour
{
    [SerializeField] private GameObject referralActive, referralInactive;
    [SerializeField] private GameObject referralPanel;

    [SerializeField] private GameObject coreActive, coreInactive;
    [SerializeField] private GameObject corePanel;

    [SerializeField] private GameObject proActive, proInactive;
    [SerializeField] private GameObject proPanel;

    [SerializeField] private GameObject bullActive, bullInactive;
    [SerializeField] private GameObject bullPanel;
    
    private void Start()
    {
        OnReferralButtonClicked();
    }

    public void OnReferralButtonClicked()
    {
        CloseAll();
        
        referralPanel.SetActive(true);
        referralActive.SetActive(true);
        referralInactive.SetActive(false);
    }

    public void OnCoreButtonClicked()
    {
        CloseAll();
        
        corePanel.SetActive(true);
        coreActive.SetActive(true);
        coreInactive.SetActive(false);
    }
    
    public void OnProButtonClicked()
    {
        CloseAll();
        
        proPanel.SetActive(true);
        proActive.SetActive(true);
        proInactive.SetActive(false);
    }

    public void OnBullButtonClicked()
    {
        CloseAll();
        
        bullPanel.SetActive(true);
        bullActive.SetActive(true);
        bullInactive.SetActive(false);
    }

    private void CloseAll()
    {
        referralPanel.SetActive(false);
        corePanel.SetActive(false);
        proPanel.SetActive(false);
        bullPanel.SetActive(false);
        
        referralActive.SetActive(false);
        coreActive.SetActive(false);
        proActive.SetActive(false);
        bullActive.SetActive(false);
        
        referralInactive.SetActive(true);
        coreInactive.SetActive(true);
        proInactive.SetActive(true);
        bullInactive.SetActive(true);
    }
}
