using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Gameplay
{
    /// <summary>
    /// Game state service implementation.
    /// Manages game phase transitions (Boot/Meta/Gameplay).
    /// Replaces old GameManager's phase management.
    /// </summary>
    public class GameStateService : IGameStateService
    {
        private readonly ISessionService _sessionService;
        private readonly IEnergyService _energyService;
        private readonly IAuthService _authService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private GamePhase _currentPhase = GamePhase.Boot;
        private GameMode _currentMode = GameMode.Endless;
        private string _currentSessionId;

        public GamePhase CurrentPhase => _currentPhase;
        public GameMode CurrentMode => _currentMode;
        public bool IsInGameplay => _currentPhase == GamePhase.Gameplay;
        public bool IsInMeta => _currentPhase == GamePhase.Meta;

        public event Action<GamePhaseChangedEventArgs> OnPhaseChanged;

        [Inject]
        public GameStateService(
            ISessionService sessionService,
            IEnergyService energyService,
            IAuthService authService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _sessionService = sessionService;
            _energyService = energyService;
            _authService = authService;
            _messageBus = messageBus;
            _logger = logger;
        }

        public async UniTask SetPhaseAsync(GamePhase phase)
        {
            if (_currentPhase == phase)
            {
                _logger.LogWarning($"[GameStateService] Already in phase: {phase}");
                return;
            }

            var previousPhase = _currentPhase;
            _currentPhase = phase;

            _logger.LogInfo($"[GameStateService] Phase: {previousPhase} -> {phase}");

            // Notify listeners
            var args = new GamePhaseChangedEventArgs
            {
                PreviousPhase = previousPhase,
                NewPhase = phase,
                Mode = _currentMode
            };

            OnPhaseChanged?.Invoke(args);

            // Publish to message bus
            _messageBus.Publish(new GamePhaseChangedMessage(previousPhase, phase));

            await UniTask.CompletedTask;
        }

        public async UniTask<GameStartResult> StartGameplayAsync(GameMode mode)
        {
            _logger.LogInfo($"[GameStateService] Starting gameplay with mode: {mode}");

            // Check auth
            if (!_authService.IsAuthenticated)
            {
                _logger.LogError("[GameStateService] Cannot start gameplay - not authenticated");
                return GameStartResult.Failed(GameStartFailReason.NotAuthenticated, "User not authenticated");
            }

            // Check energy for Chapter mode
            if (mode == GameMode.Chapter)
            {
                if (!_energyService.HasEnoughEnergy(1))
                {
                    _logger.LogWarning("[GameStateService] Insufficient energy for Chapter mode");
                    return GameStartResult.Failed(GameStartFailReason.InsufficientEnergy, "Not enough energy");
                }
            }

            // Request session from server
            try
            {
                var sessionResult = await _sessionService.StartSessionAsync(mode);

                if (!sessionResult.IsSuccess)
                {
                    _logger.LogError($"[GameStateService] Session start failed: {sessionResult.ErrorMessage}");
                    return GameStartResult.Failed(GameStartFailReason.SessionCreationFailed, sessionResult.ErrorMessage);
                }

                _currentSessionId = sessionResult.Data.SessionId;
                _currentMode = mode;

                _logger.LogInfo($"[GameStateService] Session started: {_currentSessionId}");

                // Transition to Gameplay phase
                await SetPhaseAsync(GamePhase.Gameplay);

                // Publish session started message
                _messageBus.Publish(new GameplaySessionStartedMessage(_currentSessionId, mode));

                return GameStartResult.Succeeded(_currentSessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[GameStateService] Failed to start gameplay: {ex.Message}");
                return GameStartResult.Failed(GameStartFailReason.NetworkError, ex.Message);
            }
        }

        public async UniTask ReturnToMetaAsync()
        {
            _logger.LogInfo("[GameStateService] Returning to Meta");

            // Clear session
            _currentSessionId = null;

            // Transition to Meta phase
            await SetPhaseAsync(GamePhase.Meta);

            // Publish return to meta message
            _messageBus.Publish(new ReturnToMetaMessage());
        }

        /// <summary>
        /// End the current gameplay session and submit results.
        /// Called by GameplayManager when game ends.
        /// </summary>
        public async UniTask<bool> EndSessionAsync(int score, int coins, bool success)
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                _logger.LogWarning("[GameStateService] No active session to end");
                return false;
            }

            try
            {
                _logger.LogInfo($"[GameStateService] Ending session: {_currentSessionId}, Score: {score}, Coins: {coins}");

                var result = await _sessionService.EndSessionAsync(_currentSessionId, score, coins, success);

                if (result.IsSuccess)
                {
                    _logger.LogInfo("[GameStateService] Session ended successfully");
                    _messageBus.Publish(new GameplaySessionEndedMessage(_currentSessionId, score, coins, success));
                }
                else
                {
                    _logger.LogError($"[GameStateService] Session end failed: {result.ErrorMessage}");
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[GameStateService] Failed to end session: {ex.Message}");
                return false;
            }
        }
    }
}
