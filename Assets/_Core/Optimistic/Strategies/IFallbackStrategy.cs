using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Optimistic.Strategies
{
    /// <summary>
    /// Fallback stratejileri interface'i.
    /// Network hataları ve server failure durumlarında kullanılır.
    /// </summary>
    public interface IFallbackStrategy
    {
        /// <summary>
        /// Retry: Belirli bir süre sonra tekrar dene.
        /// Exponential backoff ile retry yapar.
        /// </summary>
        /// <param name="operation">Retry edilecek operation</param>
        /// <param name="maxAttempts">Maksimum deneme sayısı</param>
        /// <param name="initialDelay">İlk bekleme süresi</param>
        /// <returns>True ise başarılı, false ise tüm retry'lar başarısız</returns>
        UniTask<bool> RetryAsync(Func<UniTask<bool>> operation, int maxAttempts = 3, TimeSpan? initialDelay = null);

        /// <summary>
        /// Cache Fallback: Local cache'den devam et.
        /// Network olmadığında cached data kullanır.
        /// </summary>
        void UseCachedData();

        /// <summary>
        /// Graceful Degradation: Özelliği geçici olarak devre dışı bırak.
        /// Kritik hatalarda feature'ı disable eder.
        /// </summary>
        /// <param name="featureName">Devre dışı bırakılacak feature adı</param>
        void DisableFeature(string featureName);

        /// <summary>
        /// User Notification: Kullanıcıya durumu bildir.
        /// </summary>
        /// <param name="notification">Bildirim bilgisi</param>
        void NotifyUser(FallbackNotification notification);
    }

    /// <summary>
    /// Fallback bildirim bilgisi
    /// </summary>
    public class FallbackNotification
    {
        /// <summary>Bildirim tipi</summary>
        public FallbackNotificationType Type { get; set; }

        /// <summary>Bildirim mesajı</summary>
        public string Message { get; set; }

        /// <summary>Retry callback (opsiyonel)</summary>
        public Action OnRetry { get; set; }

        /// <summary>Dismiss callback (opsiyonel)</summary>
        public Action OnDismiss { get; set; }

        /// <summary>Auto-dismiss süresi (saniye, 0 = manuel)</summary>
        public float AutoDismissSeconds { get; set; }

        public static FallbackNotification Info(string message)
            => new() { Type = FallbackNotificationType.Info, Message = message, AutoDismissSeconds = 3f };

        public static FallbackNotification Warning(string message)
            => new() { Type = FallbackNotificationType.Warning, Message = message, AutoDismissSeconds = 5f };

        public static FallbackNotification Error(string message, Action onRetry = null)
            => new() { Type = FallbackNotificationType.Error, Message = message, OnRetry = onRetry };

        public static FallbackNotification Critical(string message)
            => new() { Type = FallbackNotificationType.Critical, Message = message, AutoDismissSeconds = 0 };
    }

    /// <summary>
    /// Fallback bildirim tipleri
    /// </summary>
    public enum FallbackNotificationType
    {
        /// <summary>Bilgilendirme - kısa toast</summary>
        Info,

        /// <summary>Uyarı - dikkat çekici toast</summary>
        Warning,

        /// <summary>Hata - retry seçenekli</summary>
        Error,

        /// <summary>Kritik - blokleyici dialog</summary>
        Critical
    }
}
