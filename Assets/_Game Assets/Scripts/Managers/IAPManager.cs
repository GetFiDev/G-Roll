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

    // --- Robust Initialization State ---
    private TaskCompletionSource<bool> _initTcs;
    private Coroutine _timeoutCoroutine;
    private int _retryCount = 0;
    private bool _isInitializing = false;
    private readonly object _initLock = new object();
    
    private const int MAX_RETRIES = 3;
    private const float INIT_TIMEOUT_SEC = 30f;
    private const float BASE_BACKOFF_SEC = 2f;

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
        
        // Reset all initialization state to ensure clean start
        _controller = null;
        _extensions = null;
        _initTcs = null;
        _retryCount = 0;
        _isInitializing = false;
        _initializeStarted = false;
        
        LoadCatalog();
        Debug.Log($"[IAPManager] Awake complete. Instance={this.GetHashCode()}");
    }

    /// <summary>
    /// Ensures IAPManager is initialized before making purchases.
    /// Safe to call multiple times - will only initialize once.
    /// Use this method when IAP shop opens or before purchases.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (IsInitialized()) return;
        await InitializeAsync();
    }

    /// <summary>
    /// Explicit initialization called by AppFlowManager or EnsureInitializedAsync.
    /// Now properly awaits until initialization completes or all retries are exhausted.
    /// </summary>
    public async Task InitializeAsync()
    {
        Debug.Log("[IAPManager] InitializeAsync called.");
        
        if (IsInitialized())
        {
            Debug.Log("[IAPManager] Already initialized.");
            return;
        }

        // If already initializing, wait for the existing task
        lock (_initLock)
        {
            if (_isInitializing && _initTcs != null && !_initTcs.Task.IsCompleted)
            {
                Debug.Log("[IAPManager] Initialization already in progress, waiting...");
            }
            else
            {
                _isInitializing = true;
                _initTcs = new TaskCompletionSource<bool>();
                _retryCount = 0;
            }
        }

        // Wait for the initialization task (whether we started it or someone else did)
        _ = InitializeWithRetryAsync();
        
        try
        {
            await _initTcs.Task;
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAPManager] InitializeAsync exception: {e}");
        }
    }

    /// <summary>
    /// Fire-and-forget initialization for backward compatibility (e.g., retry from purchase button)
    /// </summary>
    public void InitializePurchasing()
    {
        if (IsInitialized()) return;
        
        lock (_initLock)
        {
            if (_isInitializing) 
            {
                Debug.Log("[IAPManager] Initialization already in progress...");
                return;
            }
            _isInitializing = true;
            _initTcs = new TaskCompletionSource<bool>();
            _retryCount = 0;
        }
        
        _ = InitializeWithRetryAsync();
    }

    private async Task InitializeWithRetryAsync()
    {
        // Start Unity IAP initialization (only once)
        StartUnityIAPInit();
        
        while (_retryCount < MAX_RETRIES && !IsInitialized())
        {
            _retryCount++;
            Debug.Log($"[IAPManager] Waiting for initialization... attempt {_retryCount}/{MAX_RETRIES}");
            
            // Wait for either success or timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(INIT_TIMEOUT_SEC));
            
            // Create a task that completes when initialization succeeds
            var initCompletedTask = WaitForInitializationAsync();
            
            var completedTask = await Task.WhenAny(initCompletedTask, timeoutTask);
            
            if (IsInitialized())
            {
                Debug.Log("[IAPManager] Initialization successful!");
                _initTcs?.TrySetResult(true);
                _isInitializing = false;
                return;
            }
            
            // Not initialized yet, log and continue waiting
            if (_retryCount < MAX_RETRIES)
            {
                float backoffSec = BASE_BACKOFF_SEC * Mathf.Pow(2, _retryCount - 1); // 2, 4, 8 seconds
                Debug.LogWarning($"[IAPManager] Still waiting for callback... will check again in {backoffSec:0.0}s");
                await Task.Delay(TimeSpan.FromSeconds(backoffSec));
            }
        }
        
        // All retries exhausted
        if (!IsInitialized())
        {
            Debug.LogError($"[IAPManager] Initialization did not complete after {MAX_RETRIES} wait cycles. IAP may not work.");
            _initTcs?.TrySetResult(false);
            _isInitializing = false;
        }
    }

    private async Task WaitForInitializationAsync()
    {
        // Poll until initialized or timeout (this is a simple polling approach)
        float elapsed = 0f;
        while (!IsInitialized() && elapsed < INIT_TIMEOUT_SEC)
        {
            await Task.Delay(100); // Check every 100ms
            elapsed += 0.1f;
        }
    }

    private bool _initializeStarted = false;

    // Static state that persists across Editor play sessions (for FakeStore)
    private static IStoreController s_cachedController;
    private static IExtensionProvider s_cachedExtensions;
    private static bool s_unityIAPInitialized;

    private void StartUnityIAPInit()
    {
        // Prevent calling Initialize multiple times in the same session
        if (_initializeStarted)
        {
            Debug.Log("[IAPManager] Initialize already called, skipping...");
            return;
        }
        _initializeStarted = true;
        
        Debug.Log($"[IAPManager] [{this.GetHashCode()}] Starting Unity IAP initialization...");
        
#if UNITY_EDITOR
        // In Editor, Unity IAP's static state persists between play sessions.
        // If we already initialized in a previous play, reuse the cached controller.
        if (s_unityIAPInitialized && s_cachedController != null && s_cachedExtensions != null)
        {
            Debug.Log("[IAPManager] Reusing cached Unity IAP controller from previous Editor session.");
            OnInitialized(s_cachedController, s_cachedExtensions);
            return;
        }
#endif

        var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
        module.useFakeStoreAlways = true;
        // Use DeveloperUser mode - no UI dialogs, auto-succeeds
        module.useFakeStoreUIMode = FakeStoreUIMode.DeveloperUser;
#endif
        var builder = ConfigurationBuilder.Instance(module);

        // Add products
        void AddP(string id, ProductType t) {
            Debug.Log($"[IAPManager] Adding Product: {id} ({t})");
            builder.AddProduct(id, t);
        }

        // Subscriptions
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

    public bool IsInitialized()
    {
        return _controller != null && _extensions != null;
    }

    public bool IsInitializing()
    {
        return _isInitializing;
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
                
                // Fallback / Fix logic for missing currency symbol
                bool hasSymbol = false;
                if (!string.IsNullOrEmpty(price))
                {
                    foreach (char c in price)
                    {
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
                    string symbol = iso;

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
                // Optionally show a UI message
            }
            else
            {
                Debug.LogWarning("[IAPManager] Buy failed: Not initialized. Attempting to initialize...");
                InitializePurchasing();
            }
            return;
        }

        // Debounce (1 second)
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
                 Debug.LogWarning("[IAPManager] Restore failed: Extensions reference missing.");
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

    // --- IStoreListener Callbacks ---

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log($"[IAPManager] [{this.GetHashCode()}] OnInitialized CALLBACK RECEIVED! Products: {controller?.products?.all?.Length ?? 0}");
        _controller = controller;
        _extensions = extensions;
        _retryCount = 0;
        _initializeStarted = false; // Reset so we can reinit if needed later
        
#if UNITY_EDITOR
        // Cache for reuse across Editor play sessions
        s_cachedController = controller;
        s_cachedExtensions = extensions;
        s_unityIAPInitialized = true;
#endif
        
        // Stop any pending timeout coroutine
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }
        
        // Signal completion
        _initTcs?.TrySetResult(true);
        _isInitializing = false;
        
        Debug.Log($"[IAPManager] Initialization successful! IsInitialized={IsInitialized()}");
        OnIAPInitialized?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        OnInitializeFailed(error, null);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"[IAPManager] [{this.GetHashCode()}] OnInitializeFailed CALLBACK: {error}. Message: {message}");
        _initializeStarted = false; // Reset so we can retry
        // Don't set TCS here - let the retry loop handle it
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
        if (!string.IsNullOrEmpty(product.transactionID) && _processedTransactionIDs.Contains(product.transactionID))
        {
             Debug.LogWarning($"[IAPManager] Ignoring duplicate ProcessPurchase for TransactionID: {product.transactionID}");
             return PurchaseProcessingResult.Pending;
        }
        
        if (!string.IsNullOrEmpty(product.transactionID))
        {
            _processedTransactionIDs.Add(product.transactionID);
        }

        Debug.Log($"[IAPManager] ProcessPurchase: {product.definition.id} (TxID: {product.transactionID})");
        
        // Start verification
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

        Debug.LogError($"[IAPManager] Purchase Failed: {productId}, Reason: {reason}, Desc: {message}");
        if (UIManager.Instance && UIManager.Instance.overlay) UIManager.Instance.overlay.HideProcessingPanel();
        OnPurchaseFailedEvent?.Invoke($"Purchase failed: {message}");
    }

    private async void VerifyAndConfirm(Product product)
    {
        try
        {
            var receipt = product.receipt;
            
            var result = await IAPRemoteService.VerifyPurchaseAsync(product.definition.id, receipt);

            if (result.success)
            {
                Debug.Log($"[IAPManager] Server verified purchase of {product.definition.id}. Confirming...");
                
                // Grant logic is handled by server (database update).
                if (UserInventoryManager.Instance) await UserInventoryManager.Instance.RefreshAsync();
                
                // Refresh UserData to sync subscription status
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
}
