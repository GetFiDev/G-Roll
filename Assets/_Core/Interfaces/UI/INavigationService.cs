using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.UI
{
    /// <summary>
    /// UI navigasyon yönetimi için service interface.
    /// Screen geçişleri ve popup yönetimini yapar.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Mevcut aktif screen
        /// </summary>
        IUIScreen CurrentScreen { get; }

        /// <summary>
        /// Geri gidilebilir mi?
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Navigation stack derinliği
        /// </summary>
        int StackDepth { get; }

        /// <summary>
        /// Yeni screen'e geçiş yapar (stack'e ekler).
        /// </summary>
        /// <typeparam name="T">Screen tipi</typeparam>
        /// <param name="parameters">Screen parametreleri</param>
        UniTask PushScreenAsync<T>(object parameters = null) where T : IUIScreen;

        /// <summary>
        /// Mevcut screen'den öncekine döner.
        /// </summary>
        UniTask PopScreenAsync();

        /// <summary>
        /// Geri döner (PopScreenAsync ile aynı, eski API uyumluluğu).
        /// </summary>
        UniTask GoBack();

        /// <summary>
        /// Belirtilen screen'e navigate eder (string-based, eski API uyumluluğu).
        /// </summary>
        UniTask NavigateTo(string screenId, object parameters = null, bool clearStack = false);

        /// <summary>
        /// Root screen'e kadar tüm stack'i temizler.
        /// </summary>
        UniTask PopToRootAsync();

        /// <summary>
        /// Mevcut screen'i yenisiyle değiştirir (stack depth aynı kalır).
        /// </summary>
        UniTask ReplaceScreenAsync<T>(object parameters = null) where T : IUIScreen;

        /// <summary>
        /// Popup gösterir.
        /// </summary>
        /// <typeparam name="T">Popup tipi</typeparam>
        /// <param name="parameters">Popup parametreleri</param>
        /// <returns>Popup instance'ı</returns>
        UniTask<T> ShowPopupAsync<T>(object parameters = null) where T : IUIPopup;

        /// <summary>
        /// Belirtilen tipteki popup'ı kapatır.
        /// </summary>
        UniTask HidePopupAsync<T>() where T : IUIPopup;

        /// <summary>
        /// Belirtilen popup instance'ını kapatır.
        /// Popup'ın kendi Close() metodundan çağrılır.
        /// </summary>
        UniTask HidePopupAsync(IUIPopup popup);

        /// <summary>
        /// Tüm popup'ları kapatır.
        /// </summary>
        UniTask HideAllPopupsAsync();

        /// <summary>
        /// Screen değiştiğinde tetiklenen event.
        /// </summary>
        event Action<IUIScreen> OnScreenChanged;
    }

    /// <summary>
    /// UI Screen base interface
    /// </summary>
    public interface IUIScreen
    {
        string ScreenId { get; }
        bool IsVisible { get; }
        UniTask ShowAsync(object parameters);
        UniTask HideAsync();
    }

    /// <summary>
    /// UI Popup base interface
    /// </summary>
    public interface IUIPopup
    {
        string PopupId { get; }
        bool IsVisible { get; }
        UniTask ShowAsync(object parameters);
        UniTask HideAsync();
        event Action OnClosed;
    }
}
