using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// No energy popup shown when player runs out of energy.
    /// Displays countdown timer and watch ad option.
    /// </summary>
    public class NoEnergyPopup : UIPopupBase
    {
        [Header("Energy Display")]
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image timerFillImage;

        [Header("Buttons")]
        [SerializeField] private Button watchAdButton;
        [SerializeField] private TextMeshProUGUI watchAdButtonText;
        [SerializeField] private Button closeButton;

        [Header("Processing")]
        [SerializeField] private GameObject processingPanel;

        [Inject] private IEnergyService _energyService;
        [Inject] private IAdService _adService;

        private bool _isTimerRunning;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            SetupButtonListeners();
            RefreshDisplay();
            StartTimerUpdate().Forget();

            SubscribeToMessage<EnergyChangedMessage>(OnEnergyChanged);

            return UniTask.CompletedTask;
        }

        private void SetupButtonListeners()
        {
            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveAllListeners();
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private void RefreshDisplay()
        {
            if (_energyService == null) return;

            var current = _energyService.CurrentEnergy;
            var max = _energyService.MaxEnergy;

            if (energyText != null)
            {
                energyText.text = $"{current} / {max}";
            }

            UpdateTimer();
            UpdateWatchAdButton();
        }

        private void UpdateTimer()
        {
            if (_energyService == null) return;

            var current = _energyService.CurrentEnergy;
            var max = _energyService.MaxEnergy;

            if (current >= max)
            {
                if (timerText != null)
                {
                    timerText.text = "FULL";
                }

                if (timerFillImage != null)
                {
                    timerFillImage.fillAmount = 1f;
                }

                return;
            }

            var nextRegenTime = _energyService.NextRegenTime;
            var remaining = nextRegenTime - DateTime.UtcNow;

            if (remaining.TotalSeconds > 0)
            {
                if (timerText != null)
                {
                    timerText.text = FormatTimeSpan(remaining);
                }

                if (timerFillImage != null)
                {
                    // Assuming 5 minute regen interval
                    var regenInterval = 300f;
                    timerFillImage.fillAmount = 1f - (float)(remaining.TotalSeconds / regenInterval);
                }
            }
            else
            {
                if (timerText != null)
                {
                    timerText.text = "00:00";
                }
            }
        }

        private void UpdateWatchAdButton()
        {
            if (watchAdButton != null)
            {
                var adReady = _adService?.IsRewardedAdReady ?? false;
                watchAdButton.interactable = adReady;

                if (watchAdButtonText != null)
                {
                    watchAdButtonText.text = adReady ? "Watch Ad for +1" : "No Ads Available";
                }
            }
        }

        private async UniTaskVoid StartTimerUpdate()
        {
            _isTimerRunning = true;

            while (_isTimerRunning && this != null)
            {
                UpdateTimer();

                if (_energyService != null && _energyService.CurrentEnergy >= _energyService.MaxEnergy)
                {
                    Close();
                    break;
                }

                await UniTask.Delay(1000);
            }
        }

        private void OnWatchAdClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            WatchAdForEnergyAsync().Forget();
        }

        private async UniTaskVoid WatchAdForEnergyAsync()
        {
            if (_adService == null || _energyService == null) return;

            SetProcessing(true);

            try
            {
                var adResult = await _adService.ShowRewardedAdAsync("energy_refill");

                if (adResult.Success)
                {
                    var result = await _energyService.RefillEnergyOptimisticAsync();

                    if (result.IsSuccess)
                    {
                        FeedbackService?.ShowSuccessToast("+1 Energy!");
                        FeedbackService?.PlaySuccessHaptic();
                        RefreshDisplay();
                    }
                    else
                    {
                        FeedbackService?.ShowErrorToast(result.Message ?? "Failed to add energy");
                    }
                }
                else
                {
                    FeedbackService?.ShowErrorToast("Ad not available");
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void OnCloseClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            Close();
        }

        private void OnEnergyChanged(EnergyChangedMessage msg)
        {
            RefreshDisplay();

            if (msg.CurrentEnergy >= msg.MaxEnergy)
            {
                Close();
            }
        }

        private void SetProcessing(bool processing)
        {
            if (processingPanel != null)
            {
                processingPanel.SetActive(processing);
            }

            if (watchAdButton != null)
            {
                watchAdButton.interactable = !processing;
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        protected override UniTask OnPopupHideAsync()
        {
            _isTimerRunning = false;
            return base.OnPopupHideAsync();
        }
    }
}
