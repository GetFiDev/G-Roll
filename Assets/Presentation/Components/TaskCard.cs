using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Task card component displaying a single task with progress and claim functionality.
    /// </summary>
    public class TaskCard : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private Image backgroundImage;

        [Header("Currency Icons")]
        [SerializeField] private Sprite softCurrencyIcon;
        [SerializeField] private Sprite hardCurrencyIcon;

        [Header("State Colors")]
        [SerializeField] private Color inProgressColor = new Color(0.2f, 0.2f, 0.3f, 1f);
        [SerializeField] private Color claimableColor = new Color(0.2f, 0.4f, 0.3f, 1f);
        [SerializeField] private Color claimedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Processing")]
        [SerializeField] private GameObject processingOverlay;
        [SerializeField] private CanvasGroup canvasGroup;

        [Inject] private ITaskService _taskService;
        [Inject] private IFeedbackService _feedbackService;

        private GameTask _taskData;
        private bool _isProcessing;

        public event Action<TaskCard> OnClaimed;
        public event Action<TaskCard, string> OnGoClicked;

        private void Awake()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            if (processingOverlay != null)
            {
                processingOverlay.SetActive(false);
            }
        }

        public void SetData(GameTask task)
        {
            _taskData = task;

            if (nameText != null)
            {
                nameText.text = task.Name;
            }

            if (descriptionText != null)
            {
                descriptionText.text = task.Description;
            }

            UpdateProgress(task.CurrentProgress, task.TargetProgress);
            UpdateReward(task.Reward);
            UpdateState();
        }

        public void UpdateProgress(int current, int target)
        {
            if (_taskData != null)
            {
                _taskData.CurrentProgress = current;
            }

            if (progressText != null)
            {
                progressText.text = $"{current}/{target}";
            }

            if (progressSlider != null)
            {
                progressSlider.value = target > 0 ? (float)current / target : 0f;
            }

            UpdateState();
        }

        private void UpdateReward(TaskReward reward)
        {
            if (reward == null) return;

            if (rewardText != null)
            {
                rewardText.text = $"+{reward.Amount}";
            }

            if (rewardIcon != null)
            {
                rewardIcon.sprite = reward.CurrencyType == CurrencyType.SoftCurrency
                    ? softCurrencyIcon
                    : hardCurrencyIcon;
            }
        }

        private void UpdateState()
        {
            if (_taskData == null) return;

            var isCompleted = _taskData.IsCompleted;
            var isClaimed = _taskData.IsClaimed;

            if (backgroundImage != null)
            {
                if (isClaimed)
                    backgroundImage.color = claimedColor;
                else if (isCompleted)
                    backgroundImage.color = claimableColor;
                else
                    backgroundImage.color = inProgressColor;
            }

            if (actionButton != null)
            {
                actionButton.interactable = !isClaimed && !_isProcessing;
            }

            if (actionButtonText != null)
            {
                if (isClaimed)
                    actionButtonText.text = "CLAIMED";
                else if (isCompleted)
                    actionButtonText.text = "CLAIM";
                else
                    actionButtonText.text = "GO";
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = isClaimed ? 0.5f : 1f;
            }
        }

        private void OnActionButtonClicked()
        {
            if (_isProcessing || _taskData == null) return;

            if (_taskData.IsCompleted && !_taskData.IsClaimed)
            {
                ClaimRewardAsync().Forget();
            }
            else if (!_taskData.IsCompleted)
            {
                OnGoClicked?.Invoke(this, _taskData.TaskId);
            }
        }

        private async UniTaskVoid ClaimRewardAsync()
        {
            if (_taskService == null) return;

            SetProcessing(true);

            try
            {
                var result = await _taskService.ClaimTaskRewardOptimisticAsync(_taskData.TaskId);

                if (result.IsSuccess)
                {
                    _taskData.IsClaimed = true;
                    UpdateState();
                    _feedbackService?.ShowSuccessToast("Reward claimed!");
                    _feedbackService?.PlaySuccessHaptic();
                    OnClaimed?.Invoke(this);
                }
                else
                {
                    _feedbackService?.ShowErrorToast(result.Message ?? "Failed to claim reward");
                    _feedbackService?.PlayErrorHaptic();
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void SetProcessing(bool processing)
        {
            _isProcessing = processing;

            if (processingOverlay != null)
            {
                processingOverlay.SetActive(processing);
            }

            if (actionButton != null)
            {
                actionButton.interactable = !processing && !_taskData?.IsClaimed == true;
            }
        }

        public void SetIcon(Sprite icon)
        {
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
            }
        }

        public string TaskId => _taskData?.TaskId;
        public bool IsCompleted => _taskData?.IsCompleted ?? false;
        public bool IsClaimed => _taskData?.IsClaimed ?? false;
    }
}
