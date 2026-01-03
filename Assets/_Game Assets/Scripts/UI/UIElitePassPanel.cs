using UnityEngine;
using UnityEngine.UI;

public class UIElitePassPanel : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private IAPManager.IAPProductType annualProductType = IAPManager.IAPProductType.ElitePassAnnual;
    [SerializeField] private IAPManager.IAPProductType monthlyProductType = IAPManager.IAPProductType.ElitePassMonthly;

    [Header("UI References")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button subscribeButton;
    
    [Space]
    [Header("Fake Toggle - Annual")]
    [SerializeField] private Button annualButton;
    [SerializeField] private GameObject annualToggleImage; // The checkmark or visual indicator

    [Header("Fake Toggle - Monthly")]
    [SerializeField] private Button monthlyButton;
    [SerializeField] private GameObject monthlyToggleImage; // The checkmark or visual indicator

    [Header("Toggle Visuals")]
    [SerializeField] private Sprite activeButtonSprite;
    [SerializeField] private Sprite inactiveButtonSprite;

    private IAPManager.IAPProductType selectedProductType;

    private void OnEnable()
    {
        SelectAnnual();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Bind Buttons
        if (closeButton)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }
        if (subscribeButton)
        {
            subscribeButton.onClick.RemoveListener(OnSubscribeClicked);
            subscribeButton.onClick.AddListener(OnSubscribeClicked);
        }

        if (annualButton)
        {
             annualButton.onClick.RemoveListener(SelectAnnual);
             annualButton.onClick.AddListener(SelectAnnual);
        }
        if (monthlyButton)
        {
             monthlyButton.onClick.RemoveListener(SelectMonthly);
             monthlyButton.onClick.AddListener(SelectMonthly);
        }
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private void SelectAnnual()
    {
        selectedProductType = annualProductType;
        UpdateToggleVisuals(isAnnual: true);
    }

    private void SelectMonthly()
    {
        selectedProductType = monthlyProductType;
        UpdateToggleVisuals(isAnnual: false);
    }

    private void UpdateToggleVisuals(bool isAnnual)
    {
        // Annual Visuals
        if (annualToggleImage) annualToggleImage.SetActive(isAnnual);
        if (annualButton) annualButton.image.sprite = isAnnual ? activeButtonSprite : inactiveButtonSprite;

        // Monthly Visuals
        if (monthlyToggleImage) monthlyToggleImage.SetActive(!isAnnual);
        if (monthlyButton) monthlyButton.image.sprite = !isAnnual ? activeButtonSprite : inactiveButtonSprite;
    }

    private bool isWaitingForInitialization = false;

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks if panel is destroyed mid-init
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnIAPInitialized -= OnIAPInitializedForPurchase; // Safe removal
        }
    }

    private void OnSubscribeClicked()
    {
        if (IAPManager.Instance != null)
        {
            if (!IAPManager.Instance.IsInitialized())
            {
                if (isWaitingForInitialization) return; // Already waiting

                isWaitingForInitialization = true;
                IAPManager.Instance.OnIAPInitialized += OnIAPInitializedForPurchase;

                if (!IAPManager.Instance.IsInitializing())
                {
                    Debug.LogWarning("[UIElitePassPanel] IAPManager not initialized. Attempting to initialize...");
                    IAPManager.Instance.InitializePurchasing();
                }
                else
                {
                    Debug.Log("[UIElitePassPanel] IAPManager is initializing. Waiting for completion...");
                }
                return;
            }

            PerformPurchase();
        }
    }

    private void OnIAPInitializedForPurchase()
    {
        if (IAPManager.Instance != null)
        {
            isWaitingForInitialization = false;
            IAPManager.Instance.OnIAPInitialized -= OnIAPInitializedForPurchase;
            Debug.Log("[UIElitePassPanel] IAP Initialized. Retrying purchase...");
            PerformPurchase();
        }
    }

    private void PerformPurchase()
    {
        Debug.Log("[UIElitePassPanel] PerformPurchase called.");
        if (IAPManager.Instance != null)
        {
             string id = IAPManager.Instance.GetProductId(selectedProductType);
             if (!string.IsNullOrEmpty(id))
             {
                 Debug.Log($"[UIElitePassPanel] Purchasing: {selectedProductType} ({id})");
                 IAPManager.Instance.PurchaseProduct(id);
             }
             else
             {
                 Debug.LogError($"[UIElitePassPanel] PID not found for {selectedProductType}");
             }
        }
    }
}
