using System;
using Cysharp.Threading.Tasks;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Autopilot popup for passive earning system.
    /// Handles start, progress, and claim states.
    /// </summary>
    public class AutopilotPopup : UIPopupBase
    {
        public enum AutopilotState
        {
            NotStarted,
            InProgress,
            Ready
        }

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Progress")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI earningsText;

        [Header("Action Button")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private Image actionButtonImage;
        [SerializeField] private Sprite startSprite;
        [SerializeField] private Sprite claimSprite;
        [SerializeField] private Sprite inProgressSprite;

        [Header("Elite Section")]
        [SerializeField] private GameObject elitePromotionPanel;
        [SerializeField] private Button eliteButton;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Processing")]
        [SerializeField] private GameObject processingPanel;

        [Header("Settings")]
        [SerializeField] private float normalEarningRate = 10f;
        [SerializeField] private float eliteEarningRate = 25f;
        [SerializeField] private float totalDurationHours = 8f;

        // TODO: Inject IAutopilotService when available
        // [Inject] private IAutopilotService _autopilotService;

        private AutopilotState _currentState = AutopilotState.NotStarted;
        private bool _isElite;
        private float _currentProgress;
        private float _pendingEarnings;
        private DateTime _startTime;
        private DateTime _endTime;
        private bool _isTimerRunning;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            SetupButtonListeners();
            RefreshFromService();
            UpdateUI();

            if (_currentState == AutopilotState.InProgress)
            {
                StartProgressTimer().Forget();
            }

            return UniTask.CompletedTask;
        }

        private void SetupButtonListeners()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            if (eliteButton != null)
            {
                eliteButton.onClick.RemoveAllListeners();
                eliteButton.onClick.AddListener(OnEliteButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private void RefreshFromService()
        {
            // TODO: Get actual state from AutopilotService
            // For now, using placeholder values
            _isElite = false;
            _currentState = AutopilotState.NotStarted;
            _currentProgress = 0f;
            _pendingEarnings = 0f;
        }

        private void UpdateUI()
        {
            UpdateHeader();
            UpdateProgress();
            UpdateActionButton();
            UpdateElitePromotion();
        }

        private void UpdateHeader()
        {
            if (titleText != null)
            {
                titleText.text = _isElite ? "ELITE AUTOPILOT" : "AUTOPILOT";
            }

            if (descriptionText != null)
            {
                var rate = _isElite ? eliteEarningRate : normalEarningRate;
                descriptionText.text = $"Earn {rate:F0} GET/hour while away";
            }
        }

        private void UpdateProgress()
        {
            if (progressSlider != null)
            {
                progressSlider.value = _currentProgress;
            }

            if (progressText != null)
            {
                switch (_currentState)
                {
                    case AutopilotState.NotStarted:
                        progressText.text = "Not Started";
                        break;
                    case AutopilotState.InProgress:
                        var remaining = _endTime - DateTime.UtcNow;
                        if (remaining.TotalSeconds > 0)
                        {
                            progressText.text = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                        }
                        else
                        {
                            progressText.text = "Ready!";
                        }
                        break;
                    case AutopilotState.Ready:
                        progressText.text = "Ready to Claim!";
                        break;
                }
            }

            if (earningsText != null)
            {
                earningsText.text = $"+{_pendingEarnings:F0}";
                earningsText.gameObject.SetActive(_pendingEarnings > 0);
            }
        }

        private void UpdateActionButton()
        {
            if (actionButton == null) return;

            switch (_currentState)
            {
                case AutopilotState.NotStarted:
                    actionButton.interactable = true;
                    if (actionButtonText != null) actionButtonText.text = "START";
                    if (actionButtonImage != null && startSprite != null) actionButtonImage.sprite = startSprite;
                    break;

                case AutopilotState.InProgress:
                    actionButton.interactable = false;
                    if (actionButtonText != null) actionButtonText.text = "IN PROGRESS";
                    if (actionButtonImage != null && inProgressSprite != null) actionButtonImage.sprite = inProgressSprite;
                    break;

                case AutopilotState.Ready:
                    actionButton.interactable = true;
                    if (actionButtonText != null) actionButtonText.text = "CLAIM";
                    if (actionButtonImage != null && claimSprite != null) actionButtonImage.sprite = claimSprite;
                    break;
            }
        }

        private void UpdateElitePromotion()
        {
            if (elitePromotionPanel != null)
            {
                elitePromotionPanel.SetActive(!_isElite);
            }
        }

        private void OnActionButtonClicked()
        {
            FeedbackService?.PlaySelectionHaptic();

            switch (_currentState)
            {
                case AutopilotState.NotStarted:
                    StartAutopilotAsync().Forget();
                    break;
                case AutopilotState.Ready:
                    ClaimEarningsAsync().Forget();
                    break;
            }
        }

        private async UniTaskVoid StartAutopilotAsync()
        {
            SetProcessing(true);

            try
            {
                // TODO: Call AutopilotService.StartAsync()
                await UniTask.Delay(500); // Simulate API call

                _currentState = AutopilotState.InProgress;
                _startTime = DateTime.UtcNow;
                _endTime = _startTime.AddHours(totalDurationHours);
                _currentProgress = 0f;

                UpdateUI();
                StartProgressTimer().Forget();

                FeedbackService?.ShowSuccessToast("Autopilot started!");
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private async UniTaskVoid ClaimEarningsAsync()
        {
            SetProcessing(true);

            try
            {
                // TODO: Call AutopilotService.ClaimAsync()
                await UniTask.Delay(500); // Simulate API call

                var claimed = _pendingEarnings;
                _currentState = AutopilotState.NotStarted;
                _pendingEarnings = 0f;
                _currentProgress = 0f;

                UpdateUI();

                FeedbackService?.ShowSuccessToast($"+{claimed:F0} claimed!");
                FeedbackService?.PlaySuccessHaptic();
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private async UniTaskVoid StartProgressTimer()
        {
            _isTimerRunning = true;

            while (_isTimerRunning && this != null && _currentState == AutopilotState.InProgress)
            {
                var elapsed = DateTime.UtcNow - _startTime;
                var total = _endTime - _startTime;

                _currentProgress = Mathf.Clamp01((float)(elapsed.TotalSeconds / total.TotalSeconds));

                var rate = _isElite ? eliteEarningRate : normalEarningRate;
                _pendingEarnings = (float)(elapsed.TotalHours * rate);

                if (DateTime.UtcNow >= _endTime)
                {
                    _currentState = AutopilotState.Ready;
                    _currentProgress = 1f;
                }

                UpdateProgress();
                UpdateActionButton();

                await UniTask.Delay(1000);
            }
        }

        private void OnEliteButtonClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            // TODO: Show ElitePassPopup
            // NavigationService.ShowPopupAsync<ElitePassPopup>().Forget();
        }

        private void OnCloseClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            Close();
        }

        private void SetProcessing(bool processing)
        {
            if (processingPanel != null)
            {
                processingPanel.SetActive(processing);
            }

            if (actionButton != null)
            {
                actionButton.interactable = !processing && _currentState != AutopilotState.InProgress;
            }
        }

        protected override UniTask OnPopupHideAsync()
        {
            _isTimerRunning = false;
            return base.OnPopupHideAsync();
        }
    }
}
