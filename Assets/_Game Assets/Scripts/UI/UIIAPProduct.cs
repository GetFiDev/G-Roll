using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIIAPProduct : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Select the product from the defined Enum")]
    [SerializeField] private IAPManager.IAPProductType productType;

    [Header("UI References")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TextMeshProUGUI priceText;
    
    // Optional: Title/Desc references if needed later
    // [SerializeField] private TextMeshProUGUI titleText;
    // [SerializeField] private TextMeshProUGUI descriptionText;

    private void Start()
    {
        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveListener(OnBuyClicked); 
            purchaseButton.onClick.AddListener(OnBuyClicked);
        }
        else
        {
            Debug.LogWarning($"[UIIAPProduct] Purchase Button not assigned on {gameObject.name}");
        }

        // Wait for IAPManager initialization if needed
        if (IAPManager.Instance != null)
        {
            if (IAPManager.Instance.IsInitialized())
            {
                RefreshPrice();
            }
            else
            {
                // Show default price immediately
                RefreshPrice();
                IAPManager.Instance.OnIAPInitialized += RefreshPrice;
            }
        }
    }

    private void OnDestroy()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnIAPInitialized -= RefreshPrice;
        }
    }

    private void RefreshPrice()
    {
        if (IAPManager.Instance == null) return;

        string id = IAPManager.Instance.GetProductId(productType);
        if (string.IsNullOrEmpty(id)) return;

        string price = IAPManager.Instance.GetLocalizedPrice(id);
        if (priceText != null)
        {
            priceText.text = price;
        }
        else
        {
             // Optional: Debug log if text is missing, or silent fail
        }
    }

    private float _lastPurchaseTime;

    private void OnBuyClicked()
    {
        if (Time.time - _lastPurchaseTime < 1.0f) return;
        _lastPurchaseTime = Time.time;

        if (IAPManager.Instance != null)
        {
            string id = IAPManager.Instance.GetProductId(productType);
            if (!string.IsNullOrEmpty(id))
            {
                IAPManager.Instance.PurchaseProduct(id);
            }
            else
            {
                Debug.LogError($"[UIIAPProduct] Invalid product type selection: {productType}");
            }
        }
        else
        {
            Debug.LogError("[UIIAPProduct] IAPManager Instance is null");
        }
    }
}
