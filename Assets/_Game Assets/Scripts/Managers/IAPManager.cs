using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using System.Threading.Tasks;

public class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    public static IAPManager Instance { get; private set; }

    private IStoreController _controller;
    private IExtensionProvider _extensions;

    public enum IAPProductType
    {
        ElitePass,
        ElitePassMonthly,
        ElitePassAnnual,
        Diamond5,
        Diamond10,
        Diamond25,
        Diamond60,
        Diamond150,
        Diamond400,
        Diamond1000,
        RemoveAds,
        RemoveAdsBundle
    }

    // --- Product IDs ---
    // Subscriptions
    public const string ID_ElitePass = "com.getfi.groll.elitepass"; // Monthly is the default/base
    public const string ID_ElitePassMonthly = "com.getfi.groll.elitepass";
    public const string ID_ElitePassAnnual = "com.getfi.groll.elitepassannual";

    // Consumables
    public const string ID_Diamond5 = "com.getfi.groll.diamond5";
    public const string ID_Diamond10 = "com.getfi.groll.diamond10";
    public const string ID_Diamond25 = "com.getfi.groll.diamond25";
    public const string ID_Diamond60 = "com.getfi.groll.diamond60";
    public const string ID_Diamond150 = "com.getfi.groll.diamond150";
    public const string ID_Diamond400 = "com.getfi.groll.diamond400";
    public const string ID_Diamond1000 = "com.getfi.groll.diamond1000";

    // Non-Consumables
    public const string ID_RemoveAds = "com.getfi.groll.removeads";
    public const string ID_RemoveAdsBundle = "com.getfi.groll.removeadsbundle";

    // Events
    public event Action OnIAPInitialized;
    public event Action<string> OnPurchaseSuccess; // productId
    public event Action<string> OnPurchaseFailedEvent; // message

    private bool _isInitializationStarted = false;

    // --- Default Catalog Parsing ---
    [Serializable]
    private class CatalogPriceData
    {
        public int[] data; // [major, minor, currency_code, ...]
        public double num;
    }

    [Serializable]
    private class CatalogProductDescription
    {
        public int googleLocale;
        public string title;
        public string description;
    }

    [Serializable]
    private class CatalogProduct
    {
        public string id;
        public CatalogProductDescription defaultDescription;
        public CatalogPriceData googlePrice;
    }

    [Serializable]
    private class CatalogData
    {
        public List<CatalogProduct> products;
    }

    private Dictionary<string, string> _defaultPrices = new Dictionary<string, string>();

    private void LoadCatalog()
    {
        try
        {
            var textAsset = Resources.Load<TextAsset>("IAPProductCatalog");
            if (textAsset != null)
            {
                var catalog = JsonUtility.FromJson<CatalogData>(textAsset.text);
                if (catalog != null && catalog.products != null)
                {
                    foreach (var p in catalog.products)
                    {
                        if (p.googlePrice != null)
                        {
                            // Assuming num is the price. We default to '$' prefix as planned.
                            // Ideally we could check currency code but that's complex here.
                            string priceStr = $"$ {p.googlePrice.num:0.00}";
                            _defaultPrices[p.id] = priceStr;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAPManager] Failed to load IAP catalog for defaults: {e}");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        LoadCatalog();
    }

    // Start() removed for strict initialization by AppFlowManager
    // private void Start() { }

    /// <summary>
    /// Explicit initialization called by AppFlowManager.
    /// </summary>
    public Task InitializeAsync()
    {
        Debug.Log("[IAPManager] InitializeAsync called.");
        InitializePurchasing();
        // Unity IAP init is callback-based. We don't await the callback here to avoid blocking chain.
        // The app can continue loading while IAP connects in background.
        return Task.CompletedTask;
    }

    public void InitializePurchasing()
    {
        if (IsInitialized()) return;

        // Strict Manual Initialization Rule
        // We assume no Codeless IAP is present. Checking .Instance might lazily create it!
        // if (CodelessIAPStoreListener.Instance != null) { ... }

        if (_isInitializationStarted) 
        {
             // Check if it's been too long? For now just log.
             Debug.Log("[IAPManager] Initialization already in progress...");
             return;
        }

        Debug.Log("[IAPManager] Initializing Unity IAP...");
        _isInitializationStarted = true;
        StartCoroutine(InitializationTimeout());

        var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
        module.useFakeStoreAlways = true;
#endif
        var builder = ConfigurationBuilder.Instance(module);

        // Debug Log Products
        void AddP(string id, ProductType t) {
            Debug.Log($"[IAPManager] Adding Product to Builder: {id} ({t})");
            builder.AddProduct(id, t);
        }

        // Subs
        AddP(ID_ElitePass, ProductType.Subscription);
        AddP(ID_ElitePassAnnual, ProductType.Subscription);

        // Consumables
        AddP(ID_Diamond5, ProductType.Consumable);
        AddP(ID_Diamond10, ProductType.Consumable);
        AddP(ID_Diamond25, ProductType.Consumable);
        AddP(ID_Diamond60, ProductType.Consumable);
        AddP(ID_Diamond150, ProductType.Consumable);
        AddP(ID_Diamond400, ProductType.Consumable);
        AddP(ID_Diamond1000, ProductType.Consumable);

        // Non-Consumables
        AddP(ID_RemoveAds, ProductType.NonConsumable);
        AddP(ID_RemoveAdsBundle, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    private IEnumerator InitializationTimeout()
    {
        yield return new WaitForSeconds(10f);
        if (_isInitializationStarted && !IsInitialized())
        {
            Debug.LogError("[IAPManager] Initialization Timed Out (10s)! Force-resetting state.");
            _isInitializationStarted = false;
            OnInitializeFailed(InitializationFailureReason.AppNotKnown, "Timeout");
        }
    }

    public bool IsInitialized()
    {
        return _controller != null && _extensions != null;
    }

    public bool IsInitializing()
    {
        return _isInitializationStarted;
    }

    public string GetProductId(IAPProductType type)
    {
        switch (type)
        {
            case IAPProductType.ElitePass: return ID_ElitePass;
            case IAPProductType.ElitePassMonthly: return ID_ElitePassMonthly;
            case IAPProductType.ElitePassAnnual: return ID_ElitePassAnnual;
            case IAPProductType.Diamond5: return ID_Diamond5;
            case IAPProductType.Diamond10: return ID_Diamond10;
            case IAPProductType.Diamond25: return ID_Diamond25;
            case IAPProductType.Diamond60: return ID_Diamond60;
            case IAPProductType.Diamond150: return ID_Diamond150;
            case IAPProductType.Diamond400: return ID_Diamond400;
            case IAPProductType.Diamond1000: return ID_Diamond1000;
            case IAPProductType.RemoveAds: return ID_RemoveAds;
            case IAPProductType.RemoveAdsBundle: return ID_RemoveAdsBundle;
            default: return "";
        }
    }

    public bool IsProductOwnedLocally(string productId)
    {
        if (!IsInitialized() || _controller == null) return false;
        var product = _controller.products.WithID(productId);
        return product != null && product.hasReceipt;
    }

    public string GetLocalizedPrice(string productId)
    {
        // 1. Try real store price if initialized
        if (IsInitialized())
        {
            var product = _controller.products.WithID(productId);
            if (product != null && product.metadata != null)
            {
                string price = product.metadata.localizedPriceString;
                
                // Fallback / Fix logic for missing currency symbol:
                // Some platforms/stores might return just "0.99" without a symbol.
                bool hasSymbol = false;
                if (!string.IsNullOrEmpty(price))
                {
                    foreach (char c in price)
                    {
                        // Check if character implies a currency (Symbol or Letter code like USD)
                        if (char.IsLetter(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.CurrencySymbol)
                        {
                            hasSymbol = true;
                            break;
                        }
                    }
                }

                if (!hasSymbol)
                {
                    string iso = product.metadata.isoCurrencyCode;
                    string symbol = iso; // Fallback to ISO code

                    // Common symbols map
                    switch (iso)
                    {
                        case "USD": symbol = "$"; break;
                        case "EUR": symbol = "€"; break;
                        case "TRY": symbol = "₺"; break;
                        case "GBP": symbol = "£"; break;
                        case "JPY": symbol = "¥"; break;
                    }

                    if (string.IsNullOrEmpty(price))
                        return $"{symbol} {product.metadata.localizedPrice:0.00}";
                    
                    return $"{symbol} {price}"; 
                }

                return price;
            }
        }

        // 2. Fallback to default catalog price
        if (_defaultPrices.TryGetValue(productId, out string defaultPrice))
        {
            return defaultPrice;
        }

        return "???";
    }

    private Dictionary<string, float> _lastPurchaseInitiationTimes = new Dictionary<string, float>();

    public void PurchaseProduct(string productId)
    {
        if (!IsInitialized())
        {
            if (IsInitializing())
            {
                Debug.LogWarning("[IAPManager] Buy failed: Initialization in progress. Please wait...");
            }
            else
            {
                Debug.LogWarning("[IAPManager] Buy failed: Not initialized. Attempting to initialize...");
                InitializePurchasing();
            }
            return;
        }

        // Debounce at Manager level as well (1 second)
        if (_lastPurchaseInitiationTimes.TryGetValue(productId, out float lastTime))
        {
            if (Time.time - lastTime < 1.0f)
            {
                Debug.LogWarning($"[IAPManager] Ignoring duplicate purchase request for {productId}");
                return;
            }
        }
        _lastPurchaseInitiationTimes[productId] = Time.time;

        var product = _controller.products.WithID(productId);
        if (product != null && product.availableToPurchase)
        {
            Debug.Log($"[IAPManager] Initiating purchase for: {productId}");
            if (UIManager.Instance && UIManager.Instance.overlay) UIManager.Instance.overlay.ShowProcessingPanel();
            _controller.InitiatePurchase(product);
        }
        else
        {
            Debug.LogError("[IAPManager] Buy failed: Product not found or unavailable.");
            OnPurchaseFailedEvent?.Invoke("Product unavailable");
        }
    }

    public void RestorePurchases()
    {
        if (!IsInitialized())
        {
            Debug.LogWarning("[IAPManager] Restore failed: Not initialized.");
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            if (_extensions == null)
            {
                 Debug.LogWarning("[IAPManager] Restore failed: Extensions reference missing (Codeless initialization?).");
                 return;
            }

            Debug.Log("[IAPManager] Restoring purchases...");
            var apple = _extensions.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((result, error) =>
            {
                Debug.Log($"[IAPManager] Restore result: {result}. Error: {error}");
            });
        }
        else
        {
            Debug.Log("[IAPManager] RestorePurchases is not applicable on this platform (Android/fake).");
        }
    }

    // --- IStoreListener ---

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("[IAPManager] Initialization Successful!");
        _controller = controller;
        _extensions = extensions;
        _isInitializationStarted = false;
        OnIAPInitialized?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        OnInitializeFailed(error, null);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"[IAPManager] [{this.GetHashCode()}] Initialization Failed: {error}. Message: {message}");
        _isInitializationStarted = false;
    }

    private float _lastCallbackTime;
    private HashSet<string> _processedTransactionIDs = new HashSet<string>();

    /// <summary>
    /// Process the purchase. Verify with server before confirming.
    /// </summary>
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        var product = args.purchasedProduct;

        // Idempotency: Ensure we don't process the same transaction twice.
        // This is robust against duplicate callbacks regardless of timing.
        if (!string.IsNullOrEmpty(product.transactionID) && _processedTransactionIDs.Contains(product.transactionID))
        {
             Debug.LogWarning($"[IAPManager] Ignoring duplicate ProcessPurchase for TransactionID: {product.transactionID}");
             return PurchaseProcessingResult.Pending;
        }
        
        if (!string.IsNullOrEmpty(product.transactionID))
        {
            _processedTransactionIDs.Add(product.transactionID);
        }

        Debug.Log($"[IAPManager] [{this.GetHashCode()}] ProcessPurchase: {product.definition.id} (TxID: {product.transactionID})");
        
        // Start verification coroutine/async task
        VerifyAndConfirm(product);

        return PurchaseProcessingResult.Pending;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        HandlePurchaseFailure(product.definition.id, failureReason.ToString(), "Legacy FailureReason");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
         HandlePurchaseFailure(product.definition.id, failureDescription.reason.ToString(), failureDescription.message);
    }

    private void HandlePurchaseFailure(string productId, string reason, string message)
    {
        if (Time.realtimeSinceStartup - _lastCallbackTime < 0.2f) return;
        _lastCallbackTime = Time.realtimeSinceStartup;

        Debug.LogError($"[IAPManager] [{this.GetHashCode()}] Purchase Failed: {productId}, Reason: {reason}, Desc: {message}");
        if (UIManager.Instance && UIManager.Instance.overlay) UIManager.Instance.overlay.HideProcessingPanel();
        OnPurchaseFailedEvent?.Invoke($"Purchase failed: {message}");
    }

    private async void VerifyAndConfirm(Product product)
    {
        try
        {
            // Call Remote Service
            // Note: receipt might vary by platform.
            var receipt = product.receipt;
            
            var result = await IAPRemoteService.VerifyPurchaseAsync(product.definition.id, receipt);

            if (result.success)
            {
                Debug.Log($"[IAPManager] Server verified purchase of {product.definition.id}. Confirming...");
                
                // Grant logic is handled by server (database update).
                // Client might need to refresh local inventory/stats.
                if (UserInventoryManager.Instance) await UserInventoryManager.Instance.RefreshAsync();
                
                // Refresh UserData to sync subscription status (e.g. Elite Pass)
                if (UserDatabaseManager.Instance) await UserDatabaseManager.Instance.LoadUserData();
                
                // Confirm to Unity IAP so it doesn't queue it again
                _controller.ConfirmPendingPurchase(product);

                OnPurchaseSuccess?.Invoke(product.definition.id);

                if (UIManager.Instance && UIManager.Instance.overlay)
                {
                    UIManager.Instance.overlay.HideProcessingPanel();
                    UIManager.Instance.overlay.ShowCompletedPanel();
                }

                await Task.Delay(500);
                if (UITopPanel.Instance) UITopPanel.Instance.Initialize();
            }
            else
            {
                Debug.LogError($"[IAPManager] Server verification failed: {result.message}");
                // IMPORTANT: Do NOT confirm if verification fails, so it retries later? 
                // Or confirm to unblock the queue if it's a hard error?
                // For now, let's NOT confirm, so player can try restore or the app retries on restart.
                // For now, let's NOT confirm, so player can try restore or the app retries on restart.
                OnPurchaseFailedEvent?.Invoke($"Verification failed: {result.message}");
                if (UIManager.Instance && UIManager.Instance.overlay) UIManager.Instance.overlay.HideProcessingPanel();
            }
        }
        catch (Exception e)
        {
             Debug.LogError($"[IAPManager] Exception during verification: {e}");
             OnPurchaseFailedEvent?.Invoke("Verification error");
             if (UIManager.Instance && UIManager.Instance.overlay) UIManager.Instance.overlay.HideProcessingPanel();
        }
    }
    private string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }
}
