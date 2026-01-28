using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Gameplay
{
    /// <summary>
    /// Session service implementation.
    /// PESSIMISTIC pattern kullanır çünkü session yönetimi kritiktir.
    /// Thread-safe.
    /// </summary>
    public class SessionService : ISessionService
    {
        private readonly ISessionRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        // Thread safety
        private readonly object _stateLock = new();
        private SessionState _currentState = SessionState.None;
        private string _currentSessionId;
        private SessionInfo _currentSessionInfo;

        public event Action<SessionStateChangedMessage> OnSessionStateChanged;

        [Inject]
        public SessionService(
            ISessionRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #region ISessionService Implementation

        public bool IsSessionActive
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState == SessionState.Active;
                }
            }
        }

        public string CurrentSessionId
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentSessionId;
                }
            }
        }

        public SessionState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }

        /// <summary>
        /// Session request - PESSIMISTIC
        /// Server'dan onay alınmadan session başlatılmaz.
        /// Thread-safe.
        /// </summary>
        public async UniTask<OperationResult<SessionInfo>> RequestSessionAsync(GameMode mode)
        {
            // Thread-safe validation and state change
            lock (_stateLock)
            {
                if (_currentState == SessionState.Active || _currentState == SessionState.Requesting)
                    return OperationResult<SessionInfo>.ValidationError("Session already active or requesting");

                // Update state to requesting
                SetStateUnsafe(SessionState.Requesting);
            }

            _logger.Log($"[Session] Requesting session for mode: {mode}");

            try
            {
                var response = await _remoteService.RequestSessionAsync(mode);

                if (response.Success)
                {
                    lock (_stateLock)
                    {
                        _currentSessionId = response.SessionInfo.SessionId;
                        _currentSessionInfo = response.SessionInfo;
                        SetStateUnsafe(SessionState.Active);
                    }

                    _logger.Log($"[Session] Session started: {response.SessionInfo.SessionId}");
                    return OperationResult<SessionInfo>.Success(response.SessionInfo);
                }
                else
                {
                    lock (_stateLock)
                    {
                        SetStateUnsafe(SessionState.Failed);
                    }
                    _logger.LogWarning($"[Session] Request failed: {response.ErrorMessage}");
                    return OperationResult<SessionInfo>.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    SetStateUnsafe(SessionState.Failed);
                }
                _logger.LogError($"[Session] Request error: {ex.Message}");
                return OperationResult<SessionInfo>.NetworkError(ex);
            }
        }

        /// <summary>
        /// Session başlatır (RequestSessionAsync alias).
        /// </summary>
        public UniTask<OperationResult<SessionInfo>> StartSessionAsync(GameMode mode)
        {
            return RequestSessionAsync(mode);
        }

        /// <summary>
        /// Session submit - PESSIMISTIC
        /// Sonuçlar server'a gönderilip onaylanana kadar UI'da bekleme gösterilir.
        /// Thread-safe.
        /// </summary>
        public async UniTask<OperationResult<SessionResult>> SubmitSessionAsync(SessionData data)
        {
            string sessionIdToSubmit;

            // Thread-safe validation and state change
            lock (_stateLock)
            {
                if (_currentState != SessionState.Active)
                    return OperationResult<SessionResult>.ValidationError("No active session");

                if (string.IsNullOrEmpty(_currentSessionId))
                    return OperationResult<SessionResult>.ValidationError("Session ID is missing");

                sessionIdToSubmit = _currentSessionId;

                // Update state to submitting
                SetStateUnsafe(SessionState.Submitting);
            }

            // Update session data with current session ID
            data.SessionId = sessionIdToSubmit;

            _logger.Log($"[Session] Submitting session: {sessionIdToSubmit}, Score: {data.Score}");

            try
            {
                var response = await _remoteService.SubmitSessionAsync(data);

                if (response.Success)
                {
                    lock (_stateLock)
                    {
                        SetStateUnsafe(SessionState.Completed);

                        // Clear session data
                        _currentSessionId = null;
                        _currentSessionInfo = null;
                    }

                    _logger.Log($"[Session] Session completed. Final score: {response.Result.FinalScore}");
                    return OperationResult<SessionResult>.Success(response.Result);
                }
                else
                {
                    lock (_stateLock)
                    {
                        SetStateUnsafe(SessionState.Failed);
                    }
                    _logger.LogWarning($"[Session] Submit failed: {response.ErrorMessage}");
                    return OperationResult<SessionResult>.RolledBack(response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    SetStateUnsafe(SessionState.Failed);
                }
                _logger.LogError($"[Session] Submit error: {ex.Message}");
                return OperationResult<SessionResult>.NetworkError(ex);
            }
        }

        /// <summary>
        /// Session'ı sonlandırır (basit skor ve coin ile).
        /// </summary>
        public async UniTask<OperationResult> EndSessionAsync(string sessionId, int score, int coins, bool success)
        {
            var data = new SessionData
            {
                SessionId = sessionId,
                Score = score,
                CoinsCollected = coins,
                EndReason = success ? "completed" : "died"
            };

            var result = await SubmitSessionAsync(data);

            if (result.IsSuccess)
                return OperationResult.Success();
            else
                return OperationResult.RolledBack(result.Message);
        }

        /// <summary>
        /// Aktif session'ı iptal eder.
        /// Thread-safe.
        /// </summary>
        public void CancelSession()
        {
            string sessionIdToCancel;

            lock (_stateLock)
            {
                if (_currentState != SessionState.Active)
                {
                    _logger.LogWarning("[Session] Cannot cancel - no active session");
                    return;
                }

                sessionIdToCancel = _currentSessionId;
                SetStateUnsafe(SessionState.Cancelled);

                _currentSessionId = null;
                _currentSessionInfo = null;
            }

            _logger.Log($"[Session] Session cancelled: {sessionIdToCancel}");

            // Fire and forget - notify server (with exception logging)
            if (!string.IsNullOrEmpty(sessionIdToCancel))
            {
                CancelSessionOnServerAsync(sessionIdToCancel).Forget();
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Server'a cancel isteği gönderir. Hataları loglar.
        /// </summary>
        private async UniTaskVoid CancelSessionOnServerAsync(string sessionId)
        {
            try
            {
                await _remoteService.CancelSessionAsync(sessionId);
                _logger.Log($"[Session] Server notified of cancellation: {sessionId}");
            }
            catch (Exception ex)
            {
                // Fire-and-forget but log errors
                _logger.LogError($"[Session] Failed to notify server of cancellation: {ex.Message}");
            }
        }

        /// <summary>
        /// State değiştirir ve event publish eder.
        /// UYARI: Bu metod thread-safe DEĞİLDİR, caller lock içinde çağırmalıdır.
        /// </summary>
        private void SetStateUnsafe(SessionState newState)
        {
            if (_currentState == newState) return;

            var previousState = _currentState;
            _currentState = newState;

            var message = new SessionStateChangedMessage(previousState, newState, _currentSessionId);

            // Event publish'i lock dışında yapmak daha iyi olurdu ama
            // bu durumda state tutarsızlığı olabilir. Bu trade-off kabul edilebilir.
            OnSessionStateChanged?.Invoke(message);
            _messageBus.Publish(message);

            _logger.Log($"[Session] State changed: {previousState} -> {newState}");
        }

        #endregion
    }
}
