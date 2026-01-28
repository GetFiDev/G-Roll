using System;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using VContainer;

namespace GRoll.Gameplay.Scoring
{
    /// <summary>
    /// Manages score and collectibles during gameplay.
    /// </summary>
    public class ScoreManager : IScoreManager
    {
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private int _currentScore;
        private int _coinsCollected;
        private int _currentCombo;
        private float _distanceTraveled;
        private float _playerSpeed = 8f;

        public event Action<int> OnScoreChanged;
        public event Action<int> OnComboChanged;
        public event Action<int> OnCoinsChanged;

        [Inject]
        public ScoreManager(IMessageBus messageBus, IGRollLogger logger)
        {
            _messageBus = messageBus;
            _logger = logger;
        }

        public int CurrentScore => _currentScore;
        public int CoinsCollected => _coinsCollected;
        public int CurrentCombo => _currentCombo;
        public float DistanceTraveled => _distanceTraveled;

        public void Reset()
        {
            _currentScore = 0;
            _coinsCollected = 0;
            _currentCombo = 0;
            _distanceTraveled = 0;

            OnScoreChanged?.Invoke(_currentScore);
            OnComboChanged?.Invoke(_currentCombo);
            OnCoinsChanged?.Invoke(_coinsCollected);

            _logger.Log("[ScoreManager] Score reset");
        }

        public void AddCombo(int amount = 1)
        {
            _currentCombo += amount;
            OnComboChanged?.Invoke(_currentCombo);
        }

        public void ResetCombo()
        {
            _currentCombo = 0;
            OnComboChanged?.Invoke(_currentCombo);
        }

        public void AddScore(int amount)
        {
            if (amount <= 0) return;

            _currentScore += amount;
            OnScoreChanged?.Invoke(_currentScore);

            _messageBus.Publish(new ScoreChangedMessage(_currentScore, amount));
        }

        public void AddCollectible(string type, int value)
        {
            switch (type.ToLower())
            {
                case "coin":
                    _coinsCollected += value;
                    OnCoinsChanged?.Invoke(_coinsCollected);
                    AddScore(value * 10);
                    break;

                case "gem":
                    AddScore(value * 50);
                    break;

                case "powerup":
                    AddScore(value * 25);
                    break;

                default:
                    AddScore(value);
                    break;
            }

            _messageBus.Publish(new CollectibleCollectedMessage(type, value));
            _logger.Log($"[ScoreManager] Collected {type}: {value}");
        }

        public void UpdateDistanceScore(float deltaTime)
        {
            // Calculate distance based on player speed
            var distance = _playerSpeed * deltaTime;
            _distanceTraveled += distance;

            // Award score for distance (1 point per unit)
            var distanceScore = (int)distance;
            if (distanceScore > 0)
            {
                _currentScore += distanceScore;
            }
        }

        public SessionData CollectSessionData()
        {
            return new SessionData
            {
                Score = _currentScore,
                CoinsCollected = _coinsCollected,
                Distance = (int)_distanceTraveled
            };
        }

        public void SetPlayerSpeed(float speed)
        {
            _playerSpeed = speed;
        }
    }

    /// <summary>
    /// Message published when score changes.
    /// </summary>
    public readonly struct ScoreChangedMessage : IMessage
    {
        public int NewScore { get; }
        public int Delta { get; }

        public ScoreChangedMessage(int newScore, int delta)
        {
            NewScore = newScore;
            Delta = delta;
        }
    }

    /// <summary>
    /// Message published when a collectible is collected.
    /// </summary>
    public readonly struct CollectibleCollectedMessage : IMessage
    {
        public string Type { get; }
        public int Value { get; }

        public CollectibleCollectedMessage(string type, int value)
        {
            Type = type;
            Value = value;
        }
    }
}
