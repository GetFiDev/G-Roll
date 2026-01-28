using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Oyun durumu yönetimi için service interface.
    /// Game phase (Boot/Meta/Gameplay) geçişlerini yönetir.
    /// EventChannelSO yerine MessageBus kullanır.
    ///
    /// Not: GamePhase ve GameMode enum'ları GRoll.Core namespace'inde tanımlıdır (Enums.cs).
    /// </summary>
    public interface IGameStateService
    {
        /// <summary>
        /// Mevcut oyun fazı
        /// </summary>
        Core.GamePhase CurrentPhase { get; }

        /// <summary>
        /// Mevcut oyun modu (Endless, Chapter, vs.)
        /// </summary>
        Core.GameMode CurrentMode { get; }

        /// <summary>
        /// Oyun aktif mi? (Gameplay fazında mı?)
        /// </summary>
        bool IsInGameplay { get; }

        /// <summary>
        /// Meta ekranında mı?
        /// </summary>
        bool IsInMeta { get; }

        /// <summary>
        /// Belirtilen faza geçiş yapar.
        /// </summary>
        UniTask SetPhaseAsync(Core.GamePhase phase);

        /// <summary>
        /// Gameplay'e başlar.
        /// Session request, energy check vs. işlemleri yapar.
        /// </summary>
        /// <param name="mode">Oyun modu</param>
        /// <returns>Başarılı mı?</returns>
        UniTask<GameStartResult> StartGameplayAsync(Core.GameMode mode);

        /// <summary>
        /// Gameplay'den çıkar ve Meta'ya döner.
        /// </summary>
        UniTask ReturnToMetaAsync();

        /// <summary>
        /// Phase değiştiğinde tetiklenen event.
        /// Not: IMessageBus üzerinden GamePhaseChangedMessage de publish edilir.
        /// </summary>
        event Action<GamePhaseChangedEventArgs> OnPhaseChanged;
    }

    /// <summary>
    /// Oyun başlatma sonucu
    /// </summary>
    public class GameStartResult
    {
        public bool Success { get; set; }
        public string SessionId { get; set; }
        public GameStartFailReason? FailReason { get; set; }
        public string ErrorMessage { get; set; }

        public static GameStartResult Succeeded(string sessionId) => new()
        {
            Success = true,
            SessionId = sessionId
        };

        public static GameStartResult Failed(GameStartFailReason reason, string message = null) => new()
        {
            Success = false,
            FailReason = reason,
            ErrorMessage = message
        };
    }

    /// <summary>
    /// Oyun başlatma başarısızlık sebepleri
    /// </summary>
    public enum GameStartFailReason
    {
        /// <summary>Yetersiz enerji</summary>
        InsufficientEnergy,

        /// <summary>Ağ hatası</summary>
        NetworkError,

        /// <summary>Session oluşturulamadı</summary>
        SessionCreationFailed,

        /// <summary>Kullanıcı giriş yapmamış</summary>
        NotAuthenticated,

        /// <summary>Bilinmeyen hata</summary>
        Unknown
    }

    /// <summary>
    /// Phase change event args
    /// </summary>
    public class GamePhaseChangedEventArgs
    {
        public Core.GamePhase PreviousPhase { get; set; }
        public Core.GamePhase NewPhase { get; set; }
        public Core.GameMode Mode { get; set; }
    }
}
