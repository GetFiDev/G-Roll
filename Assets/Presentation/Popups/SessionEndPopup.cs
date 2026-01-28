using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Session end popup showing score, coins, and revive option.
    /// Displayed after gameplay ends (success or failure).
    /// </summary>
    public class SessionEndPopup : UIPopupBase
    {
        public class SessionEndParams
        {
            public int Score { get; set; }
            public int CoinsEarned { get; set; }
            public bool IsSuccess { get; set; }
            public bool IsNewHighScore { get; set; }
        }

        public class SessionEndResult
        {
            public bool Revived { get; set; }
            public bool Continue { get; set; }
        }

        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("New High Score")]
        [SerializeField] private GameObject newHighScoreContainer;

        [Header("Revive Section")]
        [SerializeField] private GameObject reviveContainer;
        [SerializeField] private Button reviveButton;
        [SerializeField] private TextMeshProUGUI reviveTimerText;
        [SerializeField] private Image reviveTimerFill;
        [SerializeField] private float reviveCountdownSeconds = 5f;

        [Header("Continue Button")]
        [SerializeField] private Button continueButton;
        [SerializeField] private TextMeshProUGUI continueButtonText;

        [Header("Animation")]
        [SerializeField] private float scoreCountUpDuration = 1f;
        [SerializeField] private float scoreCountUpDelay = 0.3f;

        [Inject] private IAdService _adService;

        private SessionEndParams _params;
        private bool _isReviveCountdownActive;
        private float _reviveTimeRemaining;
        private bool _hasRevived;

        protected override async UniTask OnPopupShowAsync(object parameters)
        {
            _params = parameters as SessionEndParams ?? new SessionEndParams();
            _hasRevived = false;

            SetupUI();
            SetupButtonListeners();

            await PlayScoreCountUpAsync();

            if (!_params.IsSuccess && _adService?.IsRewardedAdReady == true)
            {
                StartReviveCountdown().Forget();
            }
            else
            {
                ShowContinueOnly();
            }
        }

        private void SetupUI()
        {
            if (titleText != null)
            {
                titleText.text = _params.IsSuccess ? "Level Complete!" : "Game Over";
            }

            if (newHighScoreContainer != null)
            {
                newHighScoreContainer.SetActive(_params.IsNewHighScore);
            }

            if (reviveContainer != null)
            {
                reviveContainer.SetActive(false);
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }
        }

        private void SetupButtonListeners()
        {
            if (reviveButton != null)
            {
                reviveButton.onClick.RemoveAllListeners();
                reviveButton.onClick.AddListener(OnReviveClicked);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        private async UniTask PlayScoreCountUpAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(scoreCountUpDelay));

            var elapsed = 0f;

            while (elapsed < scoreCountUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / scoreCountUpDuration);
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                var displayScore = Mathf.RoundToInt(Mathf.Lerp(0, _params.Score, easedT));
                var displayCoins = Mathf.RoundToInt(Mathf.Lerp(0, _params.CoinsEarned, easedT));

                if (scoreText != null)
                {
                    scoreText.text = displayScore.ToString("N0");
                }

                if (coinsText != null)
                {
                    coinsText.text = $"+{displayCoins}";
                }

                await UniTask.Yield();
            }

            if (scoreText != null)
            {
                scoreText.text = _params.Score.ToString("N0");
            }

            if (coinsText != null)
            {
                coinsText.text = $"+{_params.CoinsEarned}";
            }
        }

        private async UniTaskVoid StartReviveCountdown()
        {
            if (reviveContainer != null)
            {
                reviveContainer.SetActive(true);
            }

            _isReviveCountdownActive = true;
            _reviveTimeRemaining = reviveCountdownSeconds;

            while (_reviveTimeRemaining > 0 && _isReviveCountdownActive)
            {
                _reviveTimeRemaining -= Time.unscaledDeltaTime;

                if (reviveTimerText != null)
                {
                    reviveTimerText.text = Mathf.CeilToInt(_reviveTimeRemaining).ToString();
                }

                if (reviveTimerFill != null)
                {
                    reviveTimerFill.fillAmount = _reviveTimeRemaining / reviveCountdownSeconds;
                }

                await UniTask.Yield();
            }

            if (_isReviveCountdownActive && !_hasRevived)
            {
                ShowContinueOnly();
            }
        }

        private void ShowContinueOnly()
        {
            _isReviveCountdownActive = false;

            if (reviveContainer != null)
            {
                reviveContainer.SetActive(false);
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
            }

            if (continueButtonText != null)
            {
                continueButtonText.text = "Continue";
            }
        }

        private void OnReviveClicked()
        {
            _isReviveCountdownActive = false;
            FeedbackService?.PlaySelectionHaptic();
            ShowReviveAdAsync().Forget();
        }

        private async UniTaskVoid ShowReviveAdAsync()
        {
            if (_adService == null)
            {
                ShowContinueOnly();
                return;
            }

            var result = await _adService.ShowRewardedAdAsync("revive");

            if (result.Success)
            {
                _hasRevived = true;
                FeedbackService?.ShowSuccessToast("Revived!");
                FeedbackService?.PlaySuccessHaptic();
                CloseWithResult(new SessionEndResult { Revived = true, Continue = false });
            }
            else
            {
                FeedbackService?.ShowErrorToast("Ad not available");
                ShowContinueOnly();
            }
        }

        private void OnContinueClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(new SessionEndResult { Revived = false, Continue = true });
        }

        public override bool OnBackPressed()
        {
            // Disable back button during session end
            return true;
        }

        protected override UniTask OnPopupHideAsync()
        {
            _isReviveCountdownActive = false;
            return base.OnPopupHideAsync();
        }
    }
}
