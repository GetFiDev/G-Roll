using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Application
{
    /// <summary>
    /// Player stats servisi implementasyonu.
    /// Oyun istatistiklerini yonetir ve server ile sync eder.
    /// </summary>
    public class PlayerStatsService : IPlayerStatsService
    {
        #region Dependencies

        private readonly IPlayerStatsRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        #endregion

        #region State

        private PlayerStats _currentStats;
        private bool _isLoaded;
        private readonly object _stateLock = new();

        #endregion

        #region Properties

        public PlayerStats CurrentStats
        {
            get { lock (_stateLock) return _currentStats; }
        }

        public bool IsLoaded
        {
            get { lock (_stateLock) return _isLoaded; }
        }

        #endregion

        #region Events

        public event Action<PlayerStats> OnStatsUpdated;
        public event Action<int, int> OnNewHighScore;

        #endregion

        #region Constructor

        [Inject]
        public PlayerStatsService(
            IPlayerStatsRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
            _currentStats = new PlayerStats();
        }

        #endregion

        #region Methods

        public async UniTask<OperationResult<PlayerStats>> RefreshAsync()
        {
            try
            {
                _logger.Log("[PlayerStatsService] Refreshing stats");

                var response = await _remoteService.GetStatsAsync();

                if (!response.Success)
                {
                    _logger.LogWarning($"[PlayerStatsService] Refresh failed: {response.ErrorMessage}");
                    return OperationResult<PlayerStats>.Failed(response.ErrorMessage);
                }

                lock (_stateLock)
                {
                    _currentStats = MapToPlayerStats(response.Stats);
                    _isLoaded = true;
                }

                PublishStatsUpdated();

                _logger.Log($"[PlayerStatsService] Stats loaded. HighScore: {_currentStats.HighScore}, Games: {_currentStats.TotalGamesPlayed}");
                return OperationResult<PlayerStats>.Succeeded(_currentStats);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PlayerStatsService] Refresh error: {ex.Message}");
                return OperationResult<PlayerStats>.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult> UpdateStatAsync(string statKey, int value)
        {
            if (string.IsNullOrWhiteSpace(statKey))
            {
                return OperationResult.ValidationError("Stat key is required");
            }

            try
            {
                _logger.Log($"[PlayerStatsService] Updating stat: {statKey} = {value}");

                var response = await _remoteService.UpdateStatAsync(statKey, value);

                if (!response.Success)
                {
                    _logger.LogWarning($"[PlayerStatsService] Update failed: {response.ErrorMessage}");
                    return OperationResult.Failed(response.ErrorMessage);
                }

                // Refresh to get latest state
                await RefreshAsync();

                _logger.Log($"[PlayerStatsService] Stat updated: {statKey} = {response.NewValue}");
                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PlayerStatsService] Update error: {ex.Message}");
                return OperationResult.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult> RecordGameEndAsync(GameEndStats gameStats)
        {
            if (gameStats == null)
            {
                return OperationResult.ValidationError("Game stats required");
            }

            try
            {
                _logger.Log($"[PlayerStatsService] Recording game end. Score: {gameStats.Score}");

                int previousHighScore;
                lock (_stateLock)
                {
                    previousHighScore = _currentStats?.HighScore ?? 0;
                }

                var response = await _remoteService.RecordGameEndAsync(gameStats);

                if (!response.Success)
                {
                    _logger.LogWarning($"[PlayerStatsService] Record failed: {response.ErrorMessage}");
                    return OperationResult.Failed(response.ErrorMessage);
                }

                // Update local state
                lock (_stateLock)
                {
                    _currentStats = MapToPlayerStats(response.UpdatedStats);
                }

                PublishStatsUpdated();

                // Check for new high score
                if (response.IsNewHighScore)
                {
                    OnNewHighScore?.Invoke(response.PreviousHighScore, response.NewHighScore);
                    _messageBus?.Publish(new NewHighScoreAchievedMessage(
                        response.PreviousHighScore,
                        response.NewHighScore));

                    _logger.Log($"[PlayerStatsService] New high score! {response.PreviousHighScore} -> {response.NewHighScore}");
                }

                _logger.Log("[PlayerStatsService] Game end recorded successfully");
                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PlayerStatsService] Record error: {ex.Message}");
                return OperationResult.NetworkError(ex);
            }
        }

        public void ClearCache()
        {
            lock (_stateLock)
            {
                _currentStats = new PlayerStats();
                _isLoaded = false;
            }

            _logger.Log("[PlayerStatsService] Cache cleared");
        }

        #endregion

        #region Helpers

        private void PublishStatsUpdated()
        {
            PlayerStats statsCopy;
            lock (_stateLock)
            {
                statsCopy = _currentStats;
            }

            OnStatsUpdated?.Invoke(statsCopy);
            _messageBus?.Publish(new PlayerStatsUpdatedMessage(
                statsCopy.TotalGamesPlayed,
                statsCopy.HighScore,
                statsCopy.TotalCoinsEarned));
        }

        private static PlayerStats MapToPlayerStats(PlayerStatsData data)
        {
            return new PlayerStats
            {
                TotalGamesPlayed = data.TotalGamesPlayed,
                TotalScore = data.TotalScore,
                HighScore = data.HighScore,
                TotalCoinsEarned = data.TotalCoinsEarned,
                TotalDistance = data.TotalDistance,
                TotalDeaths = data.TotalDeaths,
                TotalPlayTimeSeconds = data.TotalPlayTimeSeconds,
                LastPlayedAt = data.LastPlayedAt
            };
        }

        #endregion
    }
}
