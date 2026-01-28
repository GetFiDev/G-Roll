using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using VContainer;

// Alias to avoid ambiguity with Unity.Purchasing types
using CorePurchaseFailedEventArgs = GRoll.Core.Interfaces.Services.PurchaseFailedEventArgs;
using CorePurchaseCompletedEventArgs = GRoll.Core.Interfaces.Services.PurchaseCompletedEventArgs;

namespace GRoll.Infrastructure.Services
{
    /// <summary>
    /// IAP service implementation using Unity IAP.
    /// Manages in-app purchases with server-side verification.
    /// </summary>
    public class IAPService : IIAPService, IDetailedStoreListener
    {
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;
        private readonly IIAPRemoteService _iapRemoteService;

        private IStoreController _storeController;
        private IExtensionProvider _extensionProvider;
        private bool _isInitialized;
        private readonly List<IAPProduct> _products = new();

        // Current purchase tracking
        private UniTaskCompletionSource<PurchaseResult> _currentPurchaseSource;
        private string _currentPurchaseProductId;

        public bool IsInitialized => _isInitialized;
        public IReadOnlyList<IAPProduct> Products => _products;

        // Events using Core types (not Unity types)
        private event Action<CorePurchaseCompletedEventArgs> _onPurchaseCompleted;
        private event Action<CorePurchaseFailedEventArgs> _onPurchaseFailed;

        event Action<CorePurchaseCompletedEventArgs> IIAPService.OnPurchaseCompleted
        {
            add => _onPurchaseCompleted += value;
            remove => _onPurchaseCompleted -= value;
        }

        event Action<CorePurchaseFailedEventArgs> IIAPService.OnPurchaseFailed
        {
            add => _onPurchaseFailed += value;
            remove => _onPurchaseFailed -= value;
        }

        [Inject]
        public IAPService(
            IMessageBus messageBus,
            IGRollLogger logger,
            IIAPRemoteService iapRemoteService)
        {
            _messageBus = messageBus;
            _logger = logger;
            _iapRemoteService = iapRemoteService;
        }

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.Log("[IAPService] Already initialized");
                return;
            }

            _logger.Log("[IAPService] Initializing Unity IAP...");

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            // Define products - these should match your IAP catalog
            // Consumables (Diamonds)
            builder.AddProduct("com.groll.diamond5", ProductType.Consumable);
            builder.AddProduct("com.groll.diamond10", ProductType.Consumable);
            builder.AddProduct("com.groll.diamond25", ProductType.Consumable);
            builder.AddProduct("com.groll.diamond60", ProductType.Consumable);
            builder.AddProduct("com.groll.diamond150", ProductType.Consumable);
            builder.AddProduct("com.groll.diamond400", ProductType.Consumable);
            builder.AddProduct("com.groll.diamond1000", ProductType.Consumable);

            // Non-Consumables
            builder.AddProduct("com.groll.removeads", ProductType.NonConsumable);

            // Subscriptions
            builder.AddProduct("com.groll.elitepass", ProductType.Subscription);
            builder.AddProduct("com.groll.elitepass_annual", ProductType.Subscription);

            UnityPurchasing.Initialize(this, builder);

            // Wait for initialization (with timeout)
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (!_isInitialized && DateTime.UtcNow - startTime < timeout)
            {
                await UniTask.Delay(100);
            }

            if (!_isInitialized)
            {
                _logger.LogWarning("[IAPService] Initialization timed out");
            }
        }

        #region IDetailedStoreListener Implementation

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _logger.Log("[IAPService] Unity IAP initialized successfully");

            _storeController = controller;
            _extensionProvider = extensions;

            // Populate products list from store
            PopulateProductsList();

            _isInitialized = true;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            _logger.LogError($"[IAPService] Initialization failed: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            _logger.LogError($"[IAPService] Initialization failed: {error} - {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var productId = args.purchasedProduct.definition.id;
            _logger.Log($"[IAPService] Processing purchase: {productId}");

            // Verify with server asynchronously
            VerifyAndCompletePurchase(args.purchasedProduct).Forget();

            // Return Pending - we'll confirm after server verification
            return PurchaseProcessingResult.Pending;
        }

        // Explicit implementation for IStoreListener (simple version)
        void IStoreListener.OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            HandlePurchaseFailure(product, failureReason, failureReason.ToString());
        }

        // Explicit implementation for IDetailedStoreListener (detailed version)
        void IDetailedStoreListener.OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            HandlePurchaseFailure(product, failureDescription.reason, failureDescription.message);
        }

        private void HandlePurchaseFailure(Product product, PurchaseFailureReason failureReason, string message)
        {
            _logger.LogWarning($"[IAPService] Purchase failed: {product.definition.id} - {failureReason} - {message}");

            var reason = MapFailureReason(failureReason);

            _onPurchaseFailed?.Invoke(new CorePurchaseFailedEventArgs
            {
                ProductId = product.definition.id,
                Reason = reason,
                ErrorMessage = message
            });

            // Complete the current purchase task
            if (_currentPurchaseSource != null && _currentPurchaseProductId == product.definition.id)
            {
                if (failureReason == PurchaseFailureReason.UserCancelled)
                {
                    _currentPurchaseSource.TrySetResult(PurchaseResult.Cancelled());
                }
                else
                {
                    _currentPurchaseSource.TrySetResult(PurchaseResult.Failed(reason, message));
                }
                _currentPurchaseSource = null;
                _currentPurchaseProductId = null;
            }
        }

        #endregion

        #region IIAPService Implementation

        public async UniTask<PurchaseResult> PurchaseAsync(string productId)
        {
            _logger.Log($"[IAPService] Purchasing: {productId}");

            if (!_isInitialized)
            {
                _logger.LogWarning("[IAPService] IAP not initialized");
                return PurchaseResult.Failed(PurchaseFailReason.Unknown, "IAP not initialized");
            }

            var product = _storeController.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                _logger.LogWarning($"[IAPService] Product not found or unavailable: {productId}");
                return PurchaseResult.Failed(PurchaseFailReason.ProductUnavailable, "Product not found");
            }

            // Set up completion source for this purchase
            _currentPurchaseSource = new UniTaskCompletionSource<PurchaseResult>();
            _currentPurchaseProductId = productId;

            // Initiate purchase
            _storeController.InitiatePurchase(product);

            // Wait for completion
            return await _currentPurchaseSource.Task;
        }

        public async UniTask<RestoreResult> RestorePurchasesAsync()
        {
            _logger.Log("[IAPService] Restoring purchases...");

            if (!_isInitialized)
            {
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = "IAP not initialized"
                };
            }

            var result = new RestoreResult
            {
                Success = true,
                RestoredProductIds = new List<string>()
            };

            var tcs = new UniTaskCompletionSource<RestoreResult>();

#if UNITY_IOS
            var apple = _extensionProvider.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((success, error) =>
            {
                if (success)
                {
                    _logger.Log("[IAPService] Restore completed successfully");
                    // Restored products will be processed via ProcessPurchase
                    foreach (var product in _storeController.products.all)
                    {
                        if (product.hasReceipt &&
                            (product.definition.type == ProductType.NonConsumable ||
                             product.definition.type == ProductType.Subscription))
                        {
                            result.RestoredProductIds.Add(product.definition.id);
                        }
                    }
                    tcs.TrySetResult(result);
                }
                else
                {
                    _logger.LogWarning($"[IAPService] Restore failed: {error}");
                    result.Success = false;
                    result.ErrorMessage = error;
                    tcs.TrySetResult(result);
                }
            });
#elif UNITY_ANDROID
            var google = _extensionProvider.GetExtension<IGooglePlayStoreExtensions>();
            google.RestoreTransactions((success, error) =>
            {
                if (success)
                {
                    _logger.Log("[IAPService] Restore completed successfully");
                    foreach (var product in _storeController.products.all)
                    {
                        if (product.hasReceipt &&
                            (product.definition.type == ProductType.NonConsumable ||
                             product.definition.type == ProductType.Subscription))
                        {
                            result.RestoredProductIds.Add(product.definition.id);
                        }
                    }
                    tcs.TrySetResult(result);
                }
                else
                {
                    _logger.LogWarning($"[IAPService] Restore failed: {error}");
                    result.Success = false;
                    result.ErrorMessage = error;
                    tcs.TrySetResult(result);
                }
            });
#else
            // Editor or other platforms - just return success
            _logger.Log("[IAPService] Restore not supported on this platform");
            result.Success = true;
            tcs.TrySetResult(result);
#endif

            return await tcs.Task;
        }

        public IAPProduct GetProduct(string productId)
        {
            return _products.Find(p => p.ProductId == productId);
        }

        public async UniTask<bool> IsSubscriptionActiveAsync(string productId)
        {
            _logger.Log($"[IAPService] Checking subscription status: {productId}");

            if (!_isInitialized)
            {
                _logger.LogWarning("[IAPService] IAP not initialized");
                return false;
            }

            var product = _storeController.products.WithID(productId);
            if (product == null || product.definition.type != ProductType.Subscription)
            {
                return false;
            }

            if (!product.hasReceipt)
            {
                return false;
            }

#if UNITY_IOS || UNITY_ANDROID
            try
            {
                var subscriptionManager = new SubscriptionManager(product, null);
                var info = subscriptionManager.getSubscriptionInfo();

                if (info.isSubscribed() == Result.True)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[IAPService] Subscription check error: {ex.Message}");
            }
#endif

            await UniTask.CompletedTask;
            return false;
        }

        #endregion

        #region Private Methods

        private void PopulateProductsList()
        {
            _products.Clear();

            foreach (var product in _storeController.products.all)
            {
                if (!product.availableToPurchase) continue;

                var iapProduct = new IAPProduct
                {
                    ProductId = product.definition.id,
                    LocalizedTitle = product.metadata.localizedTitle,
                    LocalizedDescription = product.metadata.localizedDescription,
                    LocalizedPrice = product.metadata.localizedPriceString,
                    Price = product.metadata.localizedPrice,
                    CurrencyCode = product.metadata.isoCurrencyCode,
                    ProductType = MapProductType(product.definition.type),
                    Content = GetProductContent(product.definition.id)
                };

                _products.Add(iapProduct);
            }

            _logger.Log($"[IAPService] Loaded {_products.Count} products");
        }

        private IAPProductContent GetProductContent(string productId)
        {
            // Map product IDs to content
            var content = new IAPProductContent();

            if (productId.Contains("diamond"))
            {
                if (productId.EndsWith("5")) content.Diamonds = 5;
                else if (productId.EndsWith("10")) content.Diamonds = 10;
                else if (productId.EndsWith("25")) content.Diamonds = 25;
                else if (productId.EndsWith("60")) content.Diamonds = 60;
                else if (productId.EndsWith("150")) content.Diamonds = 150;
                else if (productId.EndsWith("400")) content.Diamonds = 400;
                else if (productId.EndsWith("1000")) content.Diamonds = 1000;
            }
            else if (productId.Contains("elitepass"))
            {
                content.IsElitePass = true;
                content.Diamonds = 400; // Elite pass includes diamonds
            }

            return content;
        }

        private async UniTaskVoid VerifyAndCompletePurchase(Product product)
        {
            var productId = product.definition.id;
            var receipt = product.receipt;

            try
            {
                // Verify with server
                var verifyResult = await _iapRemoteService.VerifyPurchaseAsync(productId, receipt);

                if (verifyResult.Success)
                {
                    _logger.Log($"[IAPService] Purchase verified: {productId}");

                    // Confirm the purchase
                    _storeController.ConfirmPendingPurchase(product);

                    var transactionId = product.transactionID;
                    var iapProduct = GetProduct(productId);

                    // Notify listeners
                    _onPurchaseCompleted?.Invoke(new CorePurchaseCompletedEventArgs
                    {
                        ProductId = productId,
                        TransactionId = transactionId,
                        Content = iapProduct?.Content
                    });

                    _messageBus.Publish(new IAPPurchaseCompletedMessage(productId, transactionId));

                    // Complete the current purchase task
                    if (_currentPurchaseSource != null && _currentPurchaseProductId == productId)
                    {
                        _currentPurchaseSource.TrySetResult(PurchaseResult.Succeeded(productId, transactionId));
                        _currentPurchaseSource = null;
                        _currentPurchaseProductId = null;
                    }
                }
                else
                {
                    _logger.LogWarning($"[IAPService] Server verification failed: {verifyResult.ErrorMessage}");

                    _onPurchaseFailed?.Invoke(new CorePurchaseFailedEventArgs
                    {
                        ProductId = productId,
                        Reason = PurchaseFailReason.VerificationFailed,
                        ErrorMessage = verifyResult.ErrorMessage
                    });

                    // Complete the current purchase task with failure
                    if (_currentPurchaseSource != null && _currentPurchaseProductId == productId)
                    {
                        _currentPurchaseSource.TrySetResult(
                            PurchaseResult.Failed(PurchaseFailReason.VerificationFailed, verifyResult.ErrorMessage));
                        _currentPurchaseSource = null;
                        _currentPurchaseProductId = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[IAPService] Verification error: {ex.Message}");

                _onPurchaseFailed?.Invoke(new CorePurchaseFailedEventArgs
                {
                    ProductId = productId,
                    Reason = PurchaseFailReason.NetworkError,
                    ErrorMessage = ex.Message
                });

                // Complete the current purchase task with failure
                if (_currentPurchaseSource != null && _currentPurchaseProductId == productId)
                {
                    _currentPurchaseSource.TrySetResult(
                        PurchaseResult.Failed(PurchaseFailReason.NetworkError, ex.Message));
                    _currentPurchaseSource = null;
                    _currentPurchaseProductId = null;
                }
            }
        }

        private static IAPProductType MapProductType(ProductType unityType)
        {
            return unityType switch
            {
                ProductType.Consumable => IAPProductType.Consumable,
                ProductType.NonConsumable => IAPProductType.NonConsumable,
                ProductType.Subscription => IAPProductType.Subscription,
                _ => IAPProductType.Consumable
            };
        }

        private static PurchaseFailReason MapFailureReason(PurchaseFailureReason reason)
        {
            return reason switch
            {
                PurchaseFailureReason.UserCancelled => PurchaseFailReason.Cancelled,
                PurchaseFailureReason.PaymentDeclined => PurchaseFailReason.PaymentDeclined,
                PurchaseFailureReason.ProductUnavailable => PurchaseFailReason.ProductUnavailable,
                PurchaseFailureReason.PurchasingUnavailable => PurchaseFailReason.ProductUnavailable,
                PurchaseFailureReason.SignatureInvalid => PurchaseFailReason.VerificationFailed,
                PurchaseFailureReason.DuplicateTransaction => PurchaseFailReason.AlreadyOwned,
                PurchaseFailureReason.ExistingPurchasePending => PurchaseFailReason.AlreadyOwned,
                _ => PurchaseFailReason.Unknown
            };
        }

        #endregion
    }

    /// <summary>
    /// Message when IAP purchase is completed
    /// </summary>
    public readonly struct IAPPurchaseCompletedMessage : IMessage
    {
        public string ProductId { get; }
        public string TransactionId { get; }

        public IAPPurchaseCompletedMessage(string productId, string transactionId)
        {
            ProductId = productId;
            TransactionId = transactionId;
        }
    }
}
