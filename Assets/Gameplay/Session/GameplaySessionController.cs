using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using VContainer;

namespace GRoll.Gameplay.Session
{
    /// <summary>
    /// Controls gameplay session lifecycle.
    /// Coordinates with SessionService for server-side session management.
    /// </summary>
    public class GameplaySessionController : IGameplaySessionController
    {
        private readonly ISessionService _sessionService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private bool _isPaused;
        private DateTime _pauseStartTime;
        private TimeSpan _totalPauseDuration;
        private DateTime _sessionStartTime;

        [Inject]
        public GameplaySessionController(
            ISessionService sessionService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _sessionService = sessionService;
            _messageBus = messageBus;
            _logger = logger;
        }

        public bool IsSessionActive => _sessionService.IsSessionActive;
        public bool IsPaused => _isPaused;
        public string CurrentSessionId => _sessionService.CurrentSessionId;

        public async UniTask<OperationResult> BeginSessionAsync(GameMode mode)
        {
            _logger.Log($"[GameplaySession] Beginning session for mode: {mode}");

            // Reset tracking
            _isPaused = false;
            _totalPauseDuration = TimeSpan.Zero;
            _sessionStartTime = DateTime.UtcNow;

            // Request session from server
            var result = await _sessionService.RequestSessionAsync(mode);

            if (result.IsSuccess)
            {
                _messageBus.Publish(new GamePhaseChangedMessage(GamePhase.Meta, GamePhase.Gameplay));
                _logger.Log($"[GameplaySession] Session started: {result.Data.SessionId}");
                return OperationResult.Success();
            }

            _logger.LogWarning($"[GameplaySession] Failed to start session: {result.Message}");
            return OperationResult.RolledBack(result.Message);
        }

        public async UniTask<OperationResult<SessionResult>> EndSessionAsync(SessionData data)
        {
            if (!IsSessionActive)
            {
                return OperationResult<SessionResult>.ValidationError("No active session");
            }

            // Calculate actual play time (excluding pauses)
            var totalDuration = DateTime.UtcNow - _sessionStartTime;
            var playDuration = totalDuration - _totalPauseDuration;
            data.DurationSeconds = (int)playDuration.TotalSeconds;

            _logger.Log($"[GameplaySession] Ending session. Score: {data.Score}, Duration: {data.DurationSeconds}s");

            // Submit to server
            var result = await _sessionService.SubmitSessionAsync(data);

            if (result.IsSuccess)
            {
                _messageBus.Publish(new GamePhaseChangedMessage(GamePhase.Gameplay, GamePhase.Meta));
                return result;
            }

            _logger.LogWarning($"[GameplaySession] Failed to end session: {result.Message}");
            return result;
        }

        public void PauseSession()
        {
            if (!IsSessionActive || _isPaused) return;

            _isPaused = true;
            _pauseStartTime = DateTime.UtcNow;

            _messageBus.Publish(new GameplayPausedMessage());
            _logger.Log("[GameplaySession] Session paused");
        }

        public void ResumeSession()
        {
            if (!IsSessionActive || !_isPaused) return;

            _isPaused = false;
            _totalPauseDuration += DateTime.UtcNow - _pauseStartTime;

            _messageBus.Publish(new GameplayResumedMessage());
            _logger.Log("[GameplaySession] Session resumed");
        }

        public void CancelSession()
        {
            if (!IsSessionActive) return;

            _sessionService.CancelSession();
            _messageBus.Publish(new GamePhaseChangedMessage(GamePhase.Gameplay, GamePhase.Meta));

            _logger.Log("[GameplaySession] Session cancelled");
        }
    }

    /// <summary>
    /// Message published when gameplay is paused.
    /// </summary>
    public readonly struct GameplayPausedMessage : IMessage { }

    /// <summary>
    /// Message published when gameplay is resumed.
    /// </summary>
    public readonly struct GameplayResumedMessage : IMessage { }
}
