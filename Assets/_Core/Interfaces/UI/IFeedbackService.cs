using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.UI
{
    /// <summary>
    /// Kullanıcı feedback'i için service interface.
    /// Toast, haptic ve dialog işlemlerini yönetir.
    /// </summary>
    public interface IFeedbackService
    {
        #region Toast Messages

        /// <summary>
        /// Bilgi toast'ı gösterir.
        /// </summary>
        void ShowToast(string message, ToastType type = ToastType.Info);

        /// <summary>
        /// Başarı toast'ı gösterir.
        /// </summary>
        void ShowSuccessToast(string message);

        /// <summary>
        /// Hata toast'ı gösterir.
        /// </summary>
        void ShowErrorToast(string message);

        /// <summary>
        /// Uyarı toast'ı gösterir.
        /// </summary>
        void ShowWarningToast(string message);

        #endregion

        #region Haptic Feedback

        /// <summary>
        /// Haptic feedback çalar.
        /// </summary>
        void PlayHaptic(HapticType type);

        /// <summary>
        /// Başarı haptic'i çalar.
        /// </summary>
        void PlaySuccessHaptic();

        /// <summary>
        /// Hata haptic'i çalar.
        /// </summary>
        void PlayErrorHaptic();

        /// <summary>
        /// Seçim haptic'i çalar (buton tap, vs.)
        /// </summary>
        void PlaySelectionHaptic();

        #endregion

        #region Dialogs

        /// <summary>
        /// Onay dialog'u gösterir.
        /// </summary>
        /// <param name="title">Başlık</param>
        /// <param name="message">Mesaj</param>
        /// <param name="confirmText">Onay butonu metni</param>
        /// <param name="cancelText">İptal butonu metni</param>
        /// <returns>True: onaylandı, False: iptal edildi</returns>
        UniTask<bool> ShowConfirmationDialogAsync(
            string title,
            string message,
            string confirmText = "OK",
            string cancelText = "Cancel");

        /// <summary>
        /// Retry dialog'u gösterir.
        /// </summary>
        /// <param name="message">Hata mesajı</param>
        /// <param name="onRetry">Retry callback</param>
        UniTask ShowRetryDialogAsync(string message, Func<UniTask> onRetry);

        /// <summary>
        /// Bilgi dialog'u gösterir (tek butonlu).
        /// </summary>
        UniTask ShowAlertAsync(string title, string message, string buttonText = "OK");

        #endregion
    }

    /// <summary>
    /// Toast tipleri
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Haptic tipleri
    /// </summary>
    public enum HapticType
    {
        /// <summary>Hafif dokunma feedback'i</summary>
        Light,

        /// <summary>Orta şiddetli feedback</summary>
        Medium,

        /// <summary>Ağır feedback</summary>
        Heavy,

        /// <summary>Başarı feedback'i</summary>
        Success,

        /// <summary>Uyarı feedback'i</summary>
        Warning,

        /// <summary>Hata feedback'i</summary>
        Error,

        /// <summary>Seçim feedback'i (kısa, hafif)</summary>
        Selection
    }
}
