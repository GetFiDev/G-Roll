using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Uygulama başlatma ve akış yönetimi için service interface.
    /// Boot sequence'i orchestrate eder: Firebase Init -> Auth Check -> Profile Check -> Game Ready
    /// </summary>
    public interface IAppFlowService
    {
        /// <summary>
        /// Mevcut uygulama durumu
        /// </summary>
        AppFlowState CurrentState { get; }

        /// <summary>
        /// Uygulama tamamen hazır mı? (Ready state'e ulaşıldı mı?)
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Boot sequence'i başlatır.
        /// Bu metod sadece bir kez çağrılmalıdır (Boot Scene'de).
        /// </summary>
        UniTask StartBootSequenceAsync();

        /// <summary>
        /// Auth başarılı olduğunda çağrılır.
        /// Profile check'e geçiş yapar.
        /// </summary>
        UniTask OnAuthenticationSuccessAsync();

        /// <summary>
        /// Profil tamamlandığında çağrılır.
        /// Game data load'a geçiş yapar.
        /// </summary>
        UniTask OnProfileCompletedAsync();

        /// <summary>
        /// State değiştiğinde tetiklenen event.
        /// </summary>
        event Action<AppFlowStateChangedEventArgs> OnStateChanged;

        /// <summary>
        /// Boot sequence tamamlandığında tetiklenen event.
        /// </summary>
        event Action OnBootCompleted;

        /// <summary>
        /// Boot sequence başarısız olduğunda tetiklenen event.
        /// </summary>
        event Action<string> OnBootFailed;
    }

    /// <summary>
    /// Uygulama akış durumları
    /// </summary>
    public enum AppFlowState
    {
        /// <summary>Başlatılmadı</summary>
        None,

        /// <summary>Firebase ve core servislerin başlatılması</summary>
        Initializing,

        /// <summary>Auth durumu kontrol ediliyor</summary>
        CheckingAuth,

        /// <summary>Login bekleniyor (kullanıcı henüz giriş yapmamış)</summary>
        WaitingForAuth,

        /// <summary>Profil kontrol ediliyor</summary>
        CheckingProfile,

        /// <summary>Profil tamamlanması bekleniyor (username girilmemiş)</summary>
        WaitingForProfile,

        /// <summary>Oyun verileri yükleniyor</summary>
        LoadingGameData,

        /// <summary>Uygulama hazır, oynanabilir</summary>
        Ready,

        /// <summary>Hata durumu</summary>
        Error
    }

    /// <summary>
    /// App flow state change event args
    /// </summary>
    public class AppFlowStateChangedEventArgs
    {
        public AppFlowState PreviousState { get; set; }
        public AppFlowState NewState { get; set; }
        public string Message { get; set; }
    }
}
