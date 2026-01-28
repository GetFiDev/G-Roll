using System;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using UnityEngine;

namespace GRoll.Gameplay.Services
{
    /// <summary>
    /// Gameplay hız, booster ve combo yönetimi servisi implementasyonu.
    /// Eski GameplayLogicApplier'ın tüm fonksiyonlarının yerini alır.
    /// </summary>
    public class GameplaySpeedService : IGameplaySpeedService
    {
        #region Fields

        private readonly IMessageBus _messageBus;

        // Speed settings
        private float _startSpeed = 5f;
        private float _maxSpeed = 20f;
        private float _speedMultiplierGameplay = 1f;
        private float _speedMultiplierPlayer = 1f;

        // Booster settings
        private float _boosterFill;
        private float _boosterDuration = 3f;
        private float _boosterSpeedMultiplier = 2f;
        private float _boosterTimer;
        private bool _isBoosterActive;

        // Combo settings
        private int _baseComboPower = 25;
        private int _currentComboPower;

        // State
        private bool _firstMoveNotified;

        #endregion

        #region Constructor

        public GameplaySpeedService(IMessageBus messageBus)
        {
            _messageBus = messageBus;
            _currentComboPower = _baseComboPower;
        }

        #endregion

        #region Speed Properties

        public float StartSpeed => _startSpeed;
        public float MaxSpeed => _maxSpeed;

        public float CurrentSpeed
        {
            get
            {
                float baseSpeed = _startSpeed;
                float multiplier = TotalSpeedMultiplier;
                if (_isBoosterActive)
                    multiplier *= _boosterSpeedMultiplier;
                return Mathf.Clamp(baseSpeed * multiplier, 0f, _maxSpeed);
            }
        }

        public float SpeedMultiplierGameplay => _speedMultiplierGameplay;
        public float SpeedMultiplierPlayer => _speedMultiplierPlayer;
        public float TotalSpeedMultiplier => _speedMultiplierGameplay * _speedMultiplierPlayer;

        // Legacy compatibility - PlayerSpeedMultiplier alias
        public float PlayerSpeedMultiplier => _speedMultiplierPlayer;

        #endregion

        #region Booster Properties

        public bool IsBoosterActive => _isBoosterActive;
        public float BoosterFill => _boosterFill;
        public float BoosterDuration => _boosterDuration;
        public float BoosterSpeedMultiplier => _boosterSpeedMultiplier;

        #endregion

        #region Combo Properties

        public int CurrentComboPower => _currentComboPower;
        public int BaseComboPower => _baseComboPower;
        public float ComboMultiplier => _currentComboPower > 0 ? 1f + (_currentComboPower / 100f) : 1f;

        #endregion

        #region Events

        public event Action<float> OnPlayerSpeedMultiplierChanged;
        public event Action<float> OnGameplaySpeedMultiplierChanged;
        public event Action OnBoosterActivated;
        public event Action OnBoosterDeactivated;
        public event Action<float> OnBoosterFillChanged;
        public event Action OnFirstPlayerMove;
        public event Action<int> OnComboPowerChanged;
        public event Action OnComboReset;

        #endregion

        #region Speed Methods

        public void SetGameplaySpeedMultiplier(float multiplier)
        {
            if (Mathf.Approximately(_speedMultiplierGameplay, multiplier)) return;

            var previousMultiplier = _speedMultiplierGameplay;
            _speedMultiplierGameplay = Mathf.Max(0f, multiplier);

            OnGameplaySpeedMultiplierChanged?.Invoke(_speedMultiplierGameplay);
            _messageBus?.Publish(new SpeedMultiplierChangedMessage(
                SpeedMultiplierType.Gameplay,
                previousMultiplier,
                _speedMultiplierGameplay));
        }

        public void SetPlayerSpeedMultiplier(float multiplier)
        {
            if (Mathf.Approximately(_speedMultiplierPlayer, multiplier)) return;

            var previousMultiplier = _speedMultiplierPlayer;
            _speedMultiplierPlayer = Mathf.Max(0f, multiplier);

            OnPlayerSpeedMultiplierChanged?.Invoke(_speedMultiplierPlayer);
            _messageBus?.Publish(new SpeedMultiplierChangedMessage(
                SpeedMultiplierType.Player,
                previousMultiplier,
                _speedMultiplierPlayer));
        }

        #endregion

        #region Booster Methods

        public void ActivateBooster()
        {
            if (_isBoosterActive) return;
            if (_boosterFill < 1f) return;

            var previousFill = _boosterFill;
            _isBoosterActive = true;
            _boosterTimer = _boosterDuration;
            _boosterFill = 0f;

            OnBoosterActivated?.Invoke();
            OnBoosterFillChanged?.Invoke(_boosterFill);

            _messageBus?.Publish(new BoosterActivatedMessage(_boosterDuration, _boosterSpeedMultiplier));
            _messageBus?.Publish(new BoosterFillChangedMessage(previousFill, _boosterFill));
        }

        public void AddBoosterFill(float amount)
        {
            if (_isBoosterActive) return;

            float oldFill = _boosterFill;
            _boosterFill = Mathf.Clamp01(_boosterFill + amount);

            if (!Mathf.Approximately(oldFill, _boosterFill))
            {
                OnBoosterFillChanged?.Invoke(_boosterFill);
                _messageBus?.Publish(new BoosterFillChangedMessage(oldFill, _boosterFill));
            }
        }

        public void ResetBooster()
        {
            bool wasActive = _isBoosterActive;
            var previousFill = _boosterFill;

            _isBoosterActive = false;
            _boosterFill = 0f;
            _boosterTimer = 0f;

            if (wasActive)
            {
                OnBoosterDeactivated?.Invoke();
                _messageBus?.Publish(new BoosterDeactivatedMessage());
            }

            OnBoosterFillChanged?.Invoke(_boosterFill);
            _messageBus?.Publish(new BoosterFillChangedMessage(previousFill, _boosterFill));
        }

        #endregion

        #region Combo Methods

        public void SetBaseComboPower(int power)
        {
            _baseComboPower = Mathf.Max(0, power);
            if (_currentComboPower < _baseComboPower)
            {
                var previousPower = _currentComboPower;
                _currentComboPower = _baseComboPower;

                OnComboPowerChanged?.Invoke(_currentComboPower);
                _messageBus?.Publish(new ComboChangedMessage(previousPower, _currentComboPower, ComboMultiplier));
            }
        }

        public void AddCombo(int amount)
        {
            if (amount <= 0) return;

            var previousPower = _currentComboPower;
            _currentComboPower += amount;

            OnComboPowerChanged?.Invoke(_currentComboPower);
            _messageBus?.Publish(new ComboChangedMessage(previousPower, _currentComboPower, ComboMultiplier));
        }

        public void ResetCombo()
        {
            var finalPower = _currentComboPower;
            _currentComboPower = _baseComboPower;

            OnComboReset?.Invoke();
            _messageBus?.Publish(new ComboResetMessage(finalPower));
        }

        #endregion

        #region General Methods

        public void Reset()
        {
            _speedMultiplierGameplay = 1f;
            _speedMultiplierPlayer = 1f;
            _firstMoveNotified = false;
            _currentComboPower = _baseComboPower;

            ResetBooster();

            OnGameplaySpeedMultiplierChanged?.Invoke(_speedMultiplierGameplay);
            OnPlayerSpeedMultiplierChanged?.Invoke(_speedMultiplierPlayer);
            OnComboPowerChanged?.Invoke(_currentComboPower);
        }

        public void NotifyFirstPlayerMove()
        {
            if (_firstMoveNotified) return;
            _firstMoveNotified = true;

            OnFirstPlayerMove?.Invoke();
            _messageBus?.Publish(new PlayerFirstMoveMessage());
        }

        public void Tick(float deltaTime)
        {
            if (!_isBoosterActive) return;

            _boosterTimer -= deltaTime;

            if (_boosterTimer <= 0f)
            {
                _isBoosterActive = false;
                _boosterTimer = 0f;

                OnBoosterDeactivated?.Invoke();
                _messageBus?.Publish(new BoosterDeactivatedMessage());
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Servis ayarlarını yapılandır (GameplayManager tarafından çağrılır)
        /// </summary>
        public void Configure(float startSpeed, float maxSpeed, float boosterDuration, float boosterSpeedMultiplier)
        {
            _startSpeed = startSpeed;
            _maxSpeed = maxSpeed;
            _boosterDuration = boosterDuration;
            _boosterSpeedMultiplier = boosterSpeedMultiplier;
        }

        /// <summary>
        /// Combo ayarlarını yapılandır
        /// </summary>
        public void ConfigureCombo(int baseComboPower)
        {
            _baseComboPower = baseComboPower;
            _currentComboPower = baseComboPower;
        }

        #endregion
    }
}
