using UnityEngine;
using NetworkingData;

public class ShopItemVisibilityHandler : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The primary product type this object represents.")]
    [SerializeField] private IAPManager.IAPProductType productType;

    [Tooltip("Alternative product types that also satisfy ownership (e.g., Bundles containing the item).")]
    [SerializeField] private IAPManager.IAPProductType[] alternativeProductTypes;

    private void OnEnable()
    {
        CheckVisibility();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (UserInventoryManager.Instance) UserInventoryManager.Instance.OnInventoryChanged += CheckVisibility;
        if (UserDatabaseManager.Instance) UserDatabaseManager.Instance.OnUserDataSaved += OnUserDataUpdated;
        if (IAPManager.Instance)
        {
             IAPManager.Instance.OnPurchaseSuccess += OnPurchaseSuccess;
             IAPManager.Instance.OnIAPInitialized += CheckVisibility;
        }
    }

    private void UnsubscribeEvents()
    {
        if (UserInventoryManager.Instance) UserInventoryManager.Instance.OnInventoryChanged -= CheckVisibility;
        if (UserDatabaseManager.Instance) UserDatabaseManager.Instance.OnUserDataSaved -= OnUserDataUpdated;
        if (IAPManager.Instance)
        {
             IAPManager.Instance.OnPurchaseSuccess -= OnPurchaseSuccess;
             IAPManager.Instance.OnIAPInitialized -= CheckVisibility;
        }
    }

    private void OnUserDataUpdated(UserData data) => CheckVisibility();
    
    private void OnPurchaseSuccess(string id)
    {
        // Optimistic check: if the purchased ID matches our type, hide immediately
        if (IsMatchingProduct(id))
        {
            gameObject.SetActive(false);
        }
        else
        {
            CheckVisibility();
        }
    }

    private bool IsMatchingProduct(string productId)
    {
        if (IAPManager.Instance == null) return false;

        string myId = IAPManager.Instance.GetProductId(productType);
        if (string.Equals(productId, myId, System.StringComparison.Ordinal)) return true;

        // Defensive: If this is RemoveAds, also match RemoveAdsBundle
        if (productType == IAPManager.IAPProductType.RemoveAds)
        {
             string bundleId = IAPManager.Instance.GetProductId(IAPManager.IAPProductType.RemoveAdsBundle);
             if (string.Equals(productId, bundleId, System.StringComparison.Ordinal)) return true;
        }

        if (alternativeProductTypes != null)
        {
            foreach (var alt in alternativeProductTypes)
            {
                string altId = IAPManager.Instance.GetProductId(alt);
                if (string.Equals(productId, altId, System.StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    public void CheckVisibility()
    {
        if (IsOwned())
        {
            gameObject.SetActive(false);
        }
    }

    private bool IsOwned()
    {
        // 1. Elite Pass / Subscription Check
        if (IsElitePassType(productType))
        {
            if (UserDatabaseManager.Instance != null && 
                UserDatabaseManager.Instance.currentUserData != null && 
                UserDatabaseManager.Instance.currentUserData.hasElitePass)
            {
                return true;
            }
        }

        // 2. Remove Ads Logic (Specific handle for single vs bundle)
        if (IsRemoveAdsType(productType))
        {
             if (CheckRemoveAdsOwnership()) return true;
        }

        // 3. Inventory Ownership Check
        if (UserInventoryManager.Instance != null)
        {
            // Check primary
            if (CheckInventory(productType)) return true;

            // Check alternatives
            if (alternativeProductTypes != null)
            {
                foreach (var alt in alternativeProductTypes)
                {
                    if (CheckInventory(alt)) return true;
                }
            }
        }

        return false;
    }

    private bool CheckRemoveAdsOwnership()
    {
        // Debug logging to diagnose "yine çalışmadı"
        bool iapInitialized = IAPManager.Instance != null && IAPManager.Instance.IsInitialized();
        if (!iapInitialized) 
        {
            Debug.Log("[ShopItemVisibility] RemoveAds Check: IAP Not Initialized yet.");
        }

        bool localOwned = false;
        // 1. Check Local IAP Receipt (No Server Round-trip needed)
        if (iapInitialized)
        {
            if (IAPManager.Instance.IsProductOwnedLocally(IAPManager.ID_RemoveAdsBundle)) 
            {
                Debug.Log("[ShopItemVisibility] Ownership Found: RemoveAdsBundle (Local Receipt)");
                localOwned = true;
            }
            if (IAPManager.Instance.IsProductOwnedLocally(IAPManager.ID_RemoveAds)) 
            {
                Debug.Log("[ShopItemVisibility] Ownership Found: RemoveAds (Local Receipt)");
                localOwned = true;
            }
        }

        if (localOwned) return true;

        // 2. Check UserData flag (Backend authoritative / Cross-device sync)
        if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
        {
            if (UserDatabaseManager.Instance.currentUserData.removeAds)
            {
                Debug.Log("[ShopItemVisibility] Ownership Found: UserData.removeAds is TRUE (Server)");
                return true;
            }
            else
            {
                Debug.Log("[ShopItemVisibility] UserData.removeAds is FALSE");
            }
        }
        else
        {
            Debug.Log("[ShopItemVisibility] UserData is null or Manager missing");
        }

        // 3. Fallback: Check Inventory (Legacy or side-effect)
        if (UserInventoryManager.Instance != null)
        {
             if (UserInventoryManager.Instance.IsOwned(IAPManager.ID_RemoveAds)) 
             {
                 Debug.Log("[ShopItemVisibility] Ownership Found: Inventory (removeads)");
                 return true;
             }
             if (UserInventoryManager.Instance.IsOwned(IAPManager.ID_RemoveAdsBundle)) 
             {
                 Debug.Log("[ShopItemVisibility] Ownership Found: Inventory (removeadsbundle)");
                 return true;
             }
        }
        
        Debug.Log("[ShopItemVisibility] RemoveAds Ownership Check FAILED - Item should be visible.");
        return false;
    }

    private bool IsRemoveAdsType(IAPManager.IAPProductType type)
    {
        return type == IAPManager.IAPProductType.RemoveAds || 
               type == IAPManager.IAPProductType.RemoveAdsBundle;
    }

    private bool CheckInventory(IAPManager.IAPProductType type)
    {
        if (IAPManager.Instance == null) return false;
        string id = IAPManager.Instance.GetProductId(type);
        return UserInventoryManager.Instance.IsOwned(id);
    }

    private bool IsElitePassType(IAPManager.IAPProductType type)
    {
        return type == IAPManager.IAPProductType.ElitePass ||
               type == IAPManager.IAPProductType.ElitePassMonthly ||
               type == IAPManager.IAPProductType.ElitePassAnnual;
    }
}
