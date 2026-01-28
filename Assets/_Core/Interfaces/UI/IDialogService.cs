using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.UI
{
    /// <summary>
    /// Modal dialog yönetimi için service interface.
    /// Çeşitli dialog tipleri için generic metotlar sunar.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Onay dialog'u gösterir (Evet/Hayır).
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="message">Dialog mesajı</param>
        /// <returns>True: onaylandı, False: reddedildi</returns>
        UniTask<bool> ShowConfirmAsync(string title, string message);

        /// <summary>
        /// Onay dialog'u gösterir (özelleştirilebilir buton metinleri).
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="message">Dialog mesajı</param>
        /// <param name="confirmText">Onay butonu metni</param>
        /// <param name="cancelText">İptal butonu metni</param>
        /// <returns>True: onaylandı, False: reddedildi</returns>
        UniTask<bool> ShowConfirmationAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel");

        /// <summary>
        /// Retry dialog'u gösterir.
        /// </summary>
        /// <param name="message">Hata mesajı</param>
        /// <returns>True: retry, False: cancel</returns>
        UniTask<bool> ShowRetryAsync(string message);

        /// <summary>
        /// Text input dialog'u gösterir.
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="placeholder">Input placeholder metni</param>
        /// <param name="defaultValue">Varsayılan değer</param>
        /// <returns>Girilen text veya null (iptal edilirse)</returns>
        UniTask<string> ShowInputAsync(string title, string placeholder, string defaultValue = "");

        /// <summary>
        /// Bilgi dialog'u gösterir (tek butonlu).
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="message">Dialog mesajı</param>
        UniTask ShowAlertAsync(string title, string message);

        /// <summary>
        /// Bilgi dialog'u gösterir (tek butonlu, özelleştirilebilir buton metni).
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="message">Dialog mesajı</param>
        /// <param name="buttonText">Buton metni</param>
        UniTask ShowAlertAsync(string title, string message, string buttonText);

        /// <summary>
        /// Çoklu seçenek dialog'u gösterir.
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="options">Seçenekler</param>
        /// <returns>Seçilen seçeneğin indeksi (-1 iptal için)</returns>
        UniTask<int> ShowOptionsAsync(string title, params string[] options);

        /// <summary>
        /// Loading dialog'u gösterir.
        /// </summary>
        /// <param name="message">Loading mesajı</param>
        /// <returns>Dialog'u kapatmak için kullanılacak handle</returns>
        ILoadingDialogHandle ShowLoading(string message = "Loading...");

        /// <summary>
        /// Progress dialog'u gösterir.
        /// </summary>
        /// <param name="title">Dialog başlığı</param>
        /// <param name="cancellable">İptal edilebilir mi?</param>
        /// <returns>Progress'i güncellemek ve kapatmak için kullanılacak handle</returns>
        IProgressDialogHandle ShowProgress(string title, bool cancellable = false);

        /// <summary>
        /// Popup gösterir (generic).
        /// </summary>
        /// <typeparam name="T">Popup tipi</typeparam>
        /// <param name="parameters">Popup parametreleri</param>
        UniTask<T> ShowPopupAsync<T>(object parameters = null) where T : class;

        /// <summary>
        /// Popup gösterir (string ID ile).
        /// </summary>
        /// <typeparam name="T">Sonuç tipi</typeparam>
        /// <param name="popupId">Popup ID'si</param>
        /// <param name="parameters">Popup parametreleri</param>
        UniTask<T> ShowPopupAsync<T>(string popupId, object parameters) where T : class;

        /// <summary>
        /// Onay dialog'u gösterir (4 parametreli overload - eski API uyumluluğu).
        /// </summary>
        UniTask<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText);
    }

    /// <summary>
    /// Loading dialog handle - kapatmak için
    /// </summary>
    public interface ILoadingDialogHandle
    {
        void Close();
        void UpdateMessage(string message);
    }

    /// <summary>
    /// Progress dialog handle - progress güncellemek ve kapatmak için
    /// </summary>
    public interface IProgressDialogHandle
    {
        void UpdateProgress(float progress, string message = null);
        void Close();
        bool IsCancelled { get; }
    }
}
