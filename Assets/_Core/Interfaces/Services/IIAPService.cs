using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// In-App Purchase yönetimi için service interface.
    /// </summary>
    public interface IIAPService
    {
        /// <summary>
        /// IAP sistemi başlatıldı mı?
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Mevcut ürünler
        /// </summary>
        IReadOnlyList<IAPProduct> Products { get; }

        /// <summary>
        /// IAP sistemini başlatır.
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// Ürün satın alır.
        /// </summary>
        /// <param name="productId">Ürün ID'si</param>
        UniTask<PurchaseResult> PurchaseAsync(string productId);

        /// <summary>
        /// Önceki satın alımları geri yükler.
        /// </summary>
        UniTask<RestoreResult> RestorePurchasesAsync();

        /// <summary>
        /// Belirtilen ürünün bilgisini döner.
        /// </summary>
        IAPProduct GetProduct(string productId);

        /// <summary>
        /// Subscription aktif mi kontrol eder.
        /// </summary>
        UniTask<bool> IsSubscriptionActiveAsync(string productId);

        /// <summary>
        /// Satın alma tamamlandığında tetiklenen event.
        /// </summary>
        event Action<PurchaseCompletedEventArgs> OnPurchaseCompleted;

        /// <summary>
        /// Satın alma başarısız olduğunda tetiklenen event.
        /// </summary>
        event Action<PurchaseFailedEventArgs> OnPurchaseFailed;
    }

    /// <summary>
    /// IAP ürün bilgisi
    /// </summary>
    public class IAPProduct
    {
        public string ProductId { get; set; }
        public string LocalizedTitle { get; set; }
        public string LocalizedDescription { get; set; }
        public string LocalizedPrice { get; set; }
        public decimal Price { get; set; }
        public string CurrencyCode { get; set; }
        public IAPProductType ProductType { get; set; }

        /// <summary>
        /// Ürün içeriği (ne kazanılacak)
        /// </summary>
        public IAPProductContent Content { get; set; }
    }

    /// <summary>
    /// IAP ürün tipleri
    /// </summary>
    public enum IAPProductType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    /// <summary>
    /// Ürün içeriği
    /// </summary>
    public class IAPProductContent
    {
        public int Diamonds { get; set; }
        public int Coins { get; set; }
        public bool IsElitePass { get; set; }
        public List<string> ItemIds { get; set; }
    }

    /// <summary>
    /// Satın alma sonucu
    /// </summary>
    public class PurchaseResult
    {
        public bool Success { get; set; }
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public PurchaseFailReason? FailReason { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Satın alma kullanıcı tarafından iptal edildi mi?
        /// </summary>
        public bool WasCancelled => FailReason == PurchaseFailReason.Cancelled;

        public static PurchaseResult Succeeded(string productId, string transactionId) => new()
        {
            Success = true,
            ProductId = productId,
            TransactionId = transactionId
        };

        public static PurchaseResult Failed(PurchaseFailReason reason, string message = null) => new()
        {
            Success = false,
            FailReason = reason,
            ErrorMessage = message
        };

        public static PurchaseResult Cancelled() => new()
        {
            Success = false,
            FailReason = PurchaseFailReason.Cancelled
        };
    }

    /// <summary>
    /// Satın alma başarısızlık sebepleri
    /// </summary>
    public enum PurchaseFailReason
    {
        Cancelled,
        PaymentDeclined,
        ProductUnavailable,
        NetworkError,
        VerificationFailed,
        AlreadyOwned,
        Unknown
    }

    /// <summary>
    /// Geri yükleme sonucu
    /// </summary>
    public class RestoreResult
    {
        public bool Success { get; set; }
        public List<string> RestoredProductIds { get; set; } = new();
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Satın alma tamamlandı event args
    /// </summary>
    public class PurchaseCompletedEventArgs
    {
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public IAPProductContent Content { get; set; }
    }

    /// <summary>
    /// Satın alma başarısız event args
    /// </summary>
    public class PurchaseFailedEventArgs
    {
        public string ProductId { get; set; }
        public PurchaseFailReason Reason { get; set; }
        public string ErrorMessage { get; set; }
    }
}
