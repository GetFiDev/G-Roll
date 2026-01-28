using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Components;
using GRoll.Presentation.Core;
using GRoll.Presentation.Popups;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Screens
{
    /// <summary>
    /// Gameplay HUD screen showing score, combo, booster, and speed indicators.
    /// Handles pause, death, and session end events.
    /// </summary>
    public class GameplayScreen : UIScreenBase
    {
        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private RectTransform comboContainer;

        [Header("HUD Components")]
        [SerializeField] private BoosterFillBar boosterFillBar;
        [SerializeField] private SpeedIndicator speedIndicator;
        [SerializeField] private CountdownOverlay countdownOverlay;

        [Header("Pause")]
        [SerializeField] private Button pauseButton;

        [Header("Animation")]
        [SerializeField] private float comboScalePunch = 1.3f;
        [SerializeField] private float scoreAnimationSpeed = 10f;

        [Inject] private ISessionService _sessionService;
        [Inject] private IGameStateService _gameStateService;

        private int _displayedScore;
        private int _targetScore;
        private int _currentCombo;
        private bool _isSessionActive;

        protected override async UniTask OnScreenEnterAsync(object parameters)
        {
            SetupButtonListeners();
            SubscribeToMessages();
            ResetHUD();

            if (countdownOverlay != null)
            {
                countdownOverlay.OnCountdownComplete += OnCountdownComplete;
                await countdownOverlay.StartCountdownAsync();
            }
            else
            {
                OnCountdownComplete();
            }
        }

        private void SetupButtonListeners()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveAllListeners();
                pauseButton.onClick.AddListener(OnPauseClicked);
            }
        }

        private void SubscribeToMessages()
        {
            SubscribeToMessage<PlayerCollectMessage>(OnPlayerCollect);
            SubscribeToMessage<PlayerDeathMessage>(OnPlayerDeath);
            SubscribeToMessage<SessionStateChangedMessage>(OnSessionStateChanged);
        }

        private void ResetHUD()
        {
            _displayedScore = 0;
            _targetScore = 0;
            _currentCombo = 0;
            _isSessionActive = false;

            UpdateScoreDisplay(0);
            UpdateComboDisplay(0);

            boosterFillBar?.Reset();
            speedIndicator?.SetSpeed(0, animate: false);
        }

        private void OnCountdownComplete()
        {
            _isSessionActive = true;
        }

        private void Update()
        {
            if (!_isSessionActive) return;

            if (_displayedScore != _targetScore)
            {
                _displayedScore = Mathf.RoundToInt(Mathf.Lerp(_displayedScore, _targetScore, Time.deltaTime * scoreAnimationSpeed));
                UpdateScoreDisplay(_displayedScore);
            }
        }

        public void AddScore(int points)
        {
            _targetScore += points;
        }

        public void SetScore(int score)
        {
            _targetScore = score;
        }

        public void SetCombo(int combo)
        {
            var previousCombo = _currentCombo;
            _currentCombo = combo;

            UpdateComboDisplay(combo);

            if (combo > previousCombo && combo > 1)
            {
                PlayComboPunch().Forget();
            }
        }

        public void SetSpeed(float speed)
        {
            speedIndicator?.SetSpeed(speed);
        }

        public void AddBoosterFill(float amount)
        {
            boosterFillBar?.AddFill(amount);
        }

        private void UpdateScoreDisplay(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = score.ToString("N0");
            }
        }

        private void UpdateComboDisplay(int combo)
        {
            if (comboText != null)
            {
                comboText.text = combo > 1 ? $"x{combo}" : "";
            }

            if (comboContainer != null)
            {
                comboContainer.gameObject.SetActive(combo > 1);
            }
        }

        private async UniTaskVoid PlayComboPunch()
        {
            if (comboContainer == null) return;

            var originalScale = Vector3.one;

            comboContainer.localScale = originalScale * comboScalePunch;

            var elapsed = 0f;
            var duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                comboContainer.localScale = Vector3.Lerp(originalScale * comboScalePunch, originalScale, easedT);
                await UniTask.Yield();
            }

            comboContainer.localScale = originalScale;
        }

        private void OnPlayerCollect(PlayerCollectMessage msg)
        {
            switch (msg.ItemType)
            {
                case "coin":
                    AddScore(msg.Amount);
                    break;
                case "booster":
                    AddBoosterFill(msg.Amount);
                    break;
            }
        }

        private void OnPlayerDeath(PlayerDeathMessage msg)
        {
            _isSessionActive = false;
            ShowSessionEndAsync(false).Forget();
        }

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            if (msg.NewState == SessionState.Completed || msg.NewState == SessionState.Failed)
            {
                _isSessionActive = false;
            }
        }

        private void OnPauseClicked()
        {
            ShowPausePopupAsync().Forget();
        }

        private async UniTaskVoid ShowPausePopupAsync()
        {
            Time.timeScale = 0f;

            var popup = await NavigationService.ShowPopupAsync<PausePopup>();
            var result = await popup.WaitForResultAsync<PausePopup.PauseResult>();

            Time.timeScale = 1f;

            if (result?.Quit == true && _gameStateService != null)
            {
                await _gameStateService.ReturnToMetaAsync();
            }
        }

        private async UniTaskVoid ShowSessionEndAsync(bool success)
        {
            var sessionResult = new SessionEndPopup.SessionEndParams
            {
                Score = _targetScore,
                CoinsEarned = 0,
                IsSuccess = success,
                IsNewHighScore = false
            };

            var popup = await NavigationService.ShowPopupAsync<SessionEndPopup>(sessionResult);
            var result = await popup.WaitForResultAsync<SessionEndPopup.SessionEndResult>();

            if (result?.Revived == true)
            {
                _isSessionActive = true;
            }
            else if (_gameStateService != null)
            {
                await _gameStateService.ReturnToMetaAsync();
            }
        }

        public override bool OnBackPressed()
        {
            OnPauseClicked();
            return true;
        }

        protected override void OnScreenEnterComplete()
        {
            if (pauseButton != null)
            {
                pauseButton.interactable = true;
            }
        }

        protected override UniTask OnScreenExitAsync()
        {
            if (countdownOverlay != null)
            {
                countdownOverlay.OnCountdownComplete -= OnCountdownComplete;
                countdownOverlay.Cancel();
            }

            Time.timeScale = 1f;

            return base.OnScreenExitAsync();
        }

        public int CurrentScore => _targetScore;
        public int CurrentCombo => _currentCombo;
        public bool IsSessionActive => _isSessionActive;
    }
}
