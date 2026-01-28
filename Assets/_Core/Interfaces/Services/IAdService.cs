using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Reklam yönetimi için service interface.
    /// Rewarded ve interstitial reklamları yönetir.
    /// </summary>
    public interface IAdService
    {
        /// <summary>
        /// Rewarded reklam hazır mı?
        /// </summary>
        bool IsRewardedAdReady { get; }

        /// <summary>
        /// Interstitial reklam hazır mı?
        /// </summary>
        bool IsInterstitialReady { get; }

        /// <summary>
        /// Reklamlar devre dışı mı? (Elite pass ile)
        /// </summary>
        bool AreAdsDisabled { get; }

        /// <summary>
        /// Rewarded reklam gösterir.
        /// </summary>
        /// <param name="placement">Reklam yerleşimi (analytics için)</param>
        /// <returns>Reklam başarıyla izlendi mi?</returns>
        UniTask<AdResult> ShowRewardedAdAsync(string placement);

        /// <summary>
        /// Interstitial reklam gösterir.
        /// </summary>
        /// <param name="placement">Reklam yerleşimi</param>
        UniTask<AdResult> ShowInterstitialAsync(string placement);

        /// <summary>
        /// Reklamları devre dışı bırakır (Elite pass satın alındığında).
        /// </summary>
        void DisableAds();

        /// <summary>
        /// Reklam durumu değiştiğinde tetiklenen event.
        /// </summary>
        event Action<AdReadyStateChangedEventArgs> OnAdReadyStateChanged;
    }

    /// <summary>
    /// Reklam sonucu
    /// </summary>
    public class AdResult
    {
        public bool Success { get; set; }
        public AdResultType ResultType { get; set; }
        public string ErrorMessage { get; set; }

        public static AdResult Rewarded() => new() { Success = true, ResultType = AdResultType.Rewarded };
        public static AdResult Skipped() => new() { Success = false, ResultType = AdResultType.Skipped };
        public static AdResult Failed(string error) => new() { Success = false, ResultType = AdResultType.Failed, ErrorMessage = error };
        public static AdResult NotReady() => new() { Success = false, ResultType = AdResultType.NotReady };
        public static AdResult AdsDisabled() => new() { Success = false, ResultType = AdResultType.AdsDisabled };
    }

    /// <summary>
    /// Reklam sonuç tipleri
    /// </summary>
    public enum AdResultType
    {
        /// <summary>Reklam başarıyla izlendi, ödül verildi</summary>
        Rewarded,

        /// <summary>Kullanıcı reklamı atladı</summary>
        Skipped,

        /// <summary>Reklam gösterimi başarısız</summary>
        Failed,

        /// <summary>Reklam hazır değil</summary>
        NotReady,

        /// <summary>Reklamlar devre dışı (Elite pass)</summary>
        AdsDisabled
    }

    /// <summary>
    /// Reklam hazır durumu değişiklik event args
    /// </summary>
    public class AdReadyStateChangedEventArgs
    {
        public AdType AdType { get; set; }
        public bool IsReady { get; set; }
    }

    /// <summary>
    /// Reklam tipleri
    /// </summary>
    public enum AdType
    {
        Rewarded,
        Interstitial,
        Banner
    }
}
