using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIIAPShopPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button restoreButton; // iOS mainly, but can be visible on all
    [SerializeField] private GameObject loadingOverlay;

    private void Awake()
    {
        if (restoreButton != null)
        {
            restoreButton.onClick.AddListener(OnRestoreClicked);
        }
    }

    private void OnRestoreClicked()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.RestorePurchases();
        }
    }

    // Optional: could listen to IAPManager purchase events to show/hide loadingOverlay
}
