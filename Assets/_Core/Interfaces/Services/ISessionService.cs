using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Gameplay session yönetimi için service interface.
    /// Session request, submit ve cancel işlemlerini yönetir.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>
        /// Aktif session var mı?
        /// </summary>
        bool IsSessionActive { get; }

        /// <summary>
        /// Mevcut session ID'si
        /// </summary>
        string CurrentSessionId { get; }

        /// <summary>
        /// Session state
        /// </summary>
        SessionState CurrentState { get; }

        /// <summary>
        /// Yeni session başlatmak için server'dan token ister.
        /// </summary>
        /// <param name="mode">Oyun modu</param>
        UniTask<OperationResult<SessionInfo>> RequestSessionAsync(GameMode mode);

        /// <summary>
        /// Session başlatır (RequestSessionAsync ile aynı, alias).
        /// </summary>
        /// <param name="mode">Oyun modu</param>
        UniTask<OperationResult<SessionInfo>> StartSessionAsync(GameMode mode);

        /// <summary>
        /// Session sonuçlarını server'a gönderir.
        /// </summary>
        /// <param name="data">Session verileri (skor, süre, vs.)</param>
        UniTask<OperationResult<SessionResult>> SubmitSessionAsync(SessionData data);

        /// <summary>
        /// Session'ı sonlandırır (basit skor ve coin ile).
        /// </summary>
        UniTask<OperationResult> EndSessionAsync(string sessionId, int score, int coins, bool success);

        /// <summary>
        /// Aktif session'ı iptal eder.
        /// </summary>
        void CancelSession();

        /// <summary>
        /// Session state değiştiğinde tetiklenen event.
        /// </summary>
        event Action<SessionStateChangedMessage> OnSessionStateChanged;
    }

    /// <summary>
    /// Session başlatma bilgisi
    /// </summary>
    public class SessionInfo
    {
        public string SessionId { get; set; }
        public string Token { get; set; }
        public long StartedAt { get; set; }
        public int MaxDurationSeconds { get; set; }
    }

    /// <summary>
    /// Session verileri (submit için)
    /// </summary>
    public class SessionData
    {
        public string SessionId { get; set; }
        public int Score { get; set; }
        public int DurationSeconds { get; set; }
        public int CoinsCollected { get; set; }
        public int Distance { get; set; }
        public string EndReason { get; set; } // "completed", "died", "quit"
    }

    /// <summary>
    /// Session sonucu
    /// </summary>
    public class SessionResult
    {
        public bool IsValid { get; set; }
        public int FinalScore { get; set; }
        public int CoinsEarned { get; set; }
        public int ExperienceEarned { get; set; }
        public bool IsNewHighScore { get; set; }
        public int LeaderboardRank { get; set; }
    }

}
