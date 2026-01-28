using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Gameplay.Scoring;
using GRoll.Gameplay.Session;
using GRoll.Gameplay.Spawning;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GRoll.Gameplay
{
    /// <summary>
    /// Orchestrates gameplay systems. Delegates to specialized controllers.
    /// Main coordinator between session, scoring, and spawning subsystems.
    /// </summary>
    public class GameplayManager : IStartable, ITickable, IDisposable
    {
        private readonly IGameplaySessionController _sessionController;
        private readonly IScoreManager _scoreManager;
        private readonly IPlayerSpawnController _spawnController;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private CompositeDisposable _subscriptions = new();

        public bool IsSessionActive => _sessionController.IsSessionActive;
        public bool IsPaused => _sessionController.IsPaused;
        public int CurrentScore => _scoreManager.CurrentScore;

        [Inject]
        public GameplayManager(
            IGameplaySessionController sessionController,
            IScoreManager scoreManager,
            IPlayerSpawnController spawnController,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _sessionController = sessionController;
            _scoreManager = scoreManager;
            _spawnController = spawnController;
            _messageBus = messageBus;
            _logger = logger;
        }

        public void Start()
        {
            // Subscribe to events
            _subscriptions.Add(_messageBus.Subscribe<GamePhaseChangedMessage>(OnPhaseChanged));
            _subscriptions.Add(_messageBus.Subscribe<CollectibleCollectedMessage>(OnCollectibleCollected));
            _subscriptions.Add(_messageBus.Subscribe<PlayerDeathMessage>(OnPlayerDeath));

            _logger.Log("[GameplayManager] Initialized");
        }

        public void Tick()
        {
            if (IsSessionActive && !IsPaused)
            {
                _scoreManager.UpdateDistanceScore(Time.deltaTime);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Begins a new gameplay session with the specified mode.
        /// </summary>
        public async UniTask<bool> BeginSessionAsync(GameMode mode)
        {
            _logger.Log($"[GameplayManager] Beginning session: {mode}");

            var result = await _sessionController.BeginSessionAsync(mode);

            if (result.IsSuccess)
            {
                await _spawnController.SpawnPlayerAsync();
                _scoreManager.Reset();

                _messageBus.Publish(new GameplayStartedMessage(mode));
                return true;
            }

            _logger.LogWarning($"[GameplayManager] Failed to begin session: {result.Message}");
            return false;
        }

        /// <summary>
        /// Ends the current gameplay session with the specified reason.
        /// </summary>
        public async UniTask EndSessionAsync(SessionEndReason reason)
        {
            if (!IsSessionActive)
            {
                _logger.LogWarning("[GameplayManager] No active session to end");
                return;
            }

            _logger.Log($"[GameplayManager] Ending session: {reason}");

            var sessionData = _scoreManager.CollectSessionData();
            var result = await _sessionController.EndSessionAsync(sessionData);

            _spawnController.DespawnPlayer();

            _messageBus.Publish(new GameplayEndedMessage(reason, sessionData));
        }

        /// <summary>
        /// Pauses the current gameplay session.
        /// </summary>
        public void PauseSession()
        {
            if (!IsSessionActive) return;

            _sessionController.PauseSession();
            _logger.Log("[GameplayManager] Session paused");
        }

        /// <summary>
        /// Resumes a paused gameplay session.
        /// </summary>
        public void ResumeSession()
        {
            if (!IsSessionActive) return;

            _sessionController.ResumeSession();
            _logger.Log("[GameplayManager] Session resumed");
        }

        /// <summary>
        /// Restarts the current session.
        /// </summary>
        public async UniTask RestartSessionAsync(GameMode mode)
        {
            if (IsSessionActive)
            {
                await EndSessionAsync(SessionEndReason.Restart);
            }

            await BeginSessionAsync(mode);
        }

        // ═══════════════════════════════════════════════════════════════
        // Event Handlers
        // ═══════════════════════════════════════════════════════════════

        private void OnPhaseChanged(GamePhaseChangedMessage msg)
        {
            if (msg.NewPhase == GamePhase.Meta && IsSessionActive)
            {
                // Force end session when returning to meta
                EndSessionAsync(SessionEndReason.ReturnToMenu).Forget();
            }
        }

        private void OnCollectibleCollected(CollectibleCollectedMessage msg)
        {
            _scoreManager.AddCollectible(msg.Type, msg.Value);
        }

        private void OnPlayerDeath(PlayerDeathMessage msg)
        {
            _logger.Log($"[GameplayManager] Player died: {msg.Reason}");
            EndSessionAsync(SessionEndReason.PlayerDied).Forget();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }

    /// <summary>
    /// Reasons for ending a gameplay session.
    /// </summary>
    public enum SessionEndReason
    {
        /// <summary>Player died/failed</summary>
        PlayerDied,

        /// <summary>Player returned to menu</summary>
        ReturnToMenu,

        /// <summary>Time limit reached</summary>
        TimeUp,

        /// <summary>Level/stage completed</summary>
        Completed,

        /// <summary>Player chose to restart</summary>
        Restart
    }

    /// <summary>
    /// Message published when gameplay starts.
    /// </summary>
    public readonly struct GameplayStartedMessage : IMessage
    {
        public GameMode Mode { get; }

        public GameplayStartedMessage(GameMode mode)
        {
            Mode = mode;
        }
    }

    /// <summary>
    /// Message published when gameplay ends.
    /// </summary>
    public readonly struct GameplayEndedMessage : IMessage
    {
        public SessionEndReason Reason { get; }
        public SessionData Data { get; }

        public GameplayEndedMessage(SessionEndReason reason, SessionData data)
        {
            Reason = reason;
            Data = data;
        }
    }
}
