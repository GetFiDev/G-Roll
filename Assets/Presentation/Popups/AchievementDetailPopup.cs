using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Achievement detail popup showing full achievement info with all levels.
    /// Supports "Claim All" functionality for multiple claimable levels.
    /// </summary>
    public class AchievementDetailPopup : UIPopupBase
    {
        [Header("Achievement Info")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Level Rows")]
        [SerializeField] private Transform levelRowContainer;
        [SerializeField] private AchievementLevelRow levelRowPrefab;

        [Header("Claim All")]
        [SerializeField] private Button claimAllButton;
        [SerializeField] private TextMeshProUGUI claimAllButtonText;
        [SerializeField] private GameObject claimAllContainer;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Processing")]
        [SerializeField] private GameObject processingOverlay;

        [Inject] private IAchievementService _achievementService;
        [Inject] private ICurrencyService _currencyService;

        private Achievement _achievement;
        private List<AchievementLevelRow> _instantiatedRows = new();
        private bool _anyClaimed;

        protected override async UniTask OnPopupShowAsync(object parameters)
        {
            _achievement = parameters as Achievement;

            if (_achievement == null)
            {
                Close();
                return;
            }

            SetupUI();
            SetupButtonListeners();
            await LoadIconAsync();
        }

        private void SetupUI()
        {
            if (titleText != null)
            {
                titleText.text = _achievement.Name;
            }

            if (descriptionText != null)
            {
                descriptionText.text = _achievement.Description;
            }

            CreateLevelRows();
            UpdateClaimAllButton();
        }

        private void SetupButtonListeners()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (claimAllButton != null)
            {
                claimAllButton.onClick.RemoveAllListeners();
                claimAllButton.onClick.AddListener(OnClaimAllClicked);
            }
        }

        private void CreateLevelRows()
        {
            ClearLevelRows();

            if (levelRowPrefab == null || levelRowContainer == null) return;

            // For simple single-level achievements
            var row = Instantiate(levelRowPrefab, levelRowContainer);
            row.SetData(
                _achievement.TargetProgress,
                _achievement.Reward,
                _achievement.IsUnlocked,
                _achievement.IsClaimed,
                OnLevelClaimAsync
            );

            _instantiatedRows.Add(row);
        }

        private async UniTask OnLevelClaimAsync(AchievementLevelRow row)
        {
            if (_achievementService == null) return;

            row.SetProcessing(true);

            try
            {
                var result = await _achievementService.ClaimAchievementOptimisticAsync(_achievement.AchievementId);

                if (result.IsSuccess)
                {
                    _achievement.IsClaimed = true;
                    _anyClaimed = true;

                    row.SetClaimed(true);
                    UpdateClaimAllButton();

                    FeedbackService?.ShowSuccessToast("Reward claimed!");
                    FeedbackService?.PlaySuccessHaptic();
                }
                else
                {
                    FeedbackService?.ShowErrorToast(result.Message ?? "Failed to claim");
                    FeedbackService?.PlayErrorHaptic();
                }
            }
            finally
            {
                row.SetProcessing(false);
            }
        }

        private void UpdateClaimAllButton()
        {
            var claimableCount = 0;
            var totalReward = 0;

            if (_achievement.IsUnlocked && !_achievement.IsClaimed)
            {
                claimableCount = 1;
                totalReward = _achievement.Reward?.Amount ?? 0;
            }

            if (claimAllContainer != null)
            {
                claimAllContainer.SetActive(claimableCount > 0);
            }

            if (claimAllButton != null)
            {
                claimAllButton.interactable = claimableCount > 0;
            }

            if (claimAllButtonText != null)
            {
                claimAllButtonText.text = claimableCount > 0
                    ? $"Claim All (+{totalReward})"
                    : "All Claimed";
            }
        }

        private void OnClaimAllClicked()
        {
            ClaimAllAsync().Forget();
        }

        private async UniTaskVoid ClaimAllAsync()
        {
            if (_achievementService == null || _achievement.IsClaimed) return;

            SetProcessing(true);

            try
            {
                var result = await _achievementService.ClaimAchievementOptimisticAsync(_achievement.AchievementId);

                if (result.IsSuccess)
                {
                    _achievement.IsClaimed = true;
                    _anyClaimed = true;

                    foreach (var row in _instantiatedRows)
                    {
                        row.SetClaimed(true);
                    }

                    UpdateClaimAllButton();

                    FeedbackService?.ShowSuccessToast("All rewards claimed!");
                    FeedbackService?.PlaySuccessHaptic();
                }
                else
                {
                    FeedbackService?.ShowErrorToast(result.Message ?? "Failed to claim");
                    FeedbackService?.PlayErrorHaptic();
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private async UniTask LoadIconAsync()
        {
            // TODO: Load icon from achievement definition URL
            await UniTask.Yield();
        }

        private void OnCloseClicked()
        {
            CloseWithResult(_anyClaimed);
        }

        public override bool OnBackPressed()
        {
            CloseWithResult(_anyClaimed);
            return true;
        }

        private void SetProcessing(bool processing)
        {
            if (processingOverlay != null)
            {
                processingOverlay.SetActive(processing);
            }

            if (claimAllButton != null)
            {
                claimAllButton.interactable = !processing;
            }
        }

        private void ClearLevelRows()
        {
            foreach (var row in _instantiatedRows)
            {
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }
            _instantiatedRows.Clear();
        }

        protected override UniTask OnPopupHideAsync()
        {
            ClearLevelRows();
            return base.OnPopupHideAsync();
        }
    }

    /// <summary>
    /// Individual achievement level row within detail popup.
    /// </summary>
    public class AchievementLevelRow : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimButtonText;
        [SerializeField] private GameObject processingIndicator;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Sprites")]
        [SerializeField] private Sprite claimableSprite;
        [SerializeField] private Sprite claimedSprite;

        private bool _isClaimed;
        private bool _isReachable;
        private System.Func<AchievementLevelRow, UniTask> _onClaimAsync;

        public void SetData(
            int targetValue,
            AchievementReward reward,
            bool isReachable,
            bool isClaimed,
            System.Func<AchievementLevelRow, UniTask> onClaimAsync)
        {
            _isReachable = isReachable;
            _isClaimed = isClaimed;
            _onClaimAsync = onClaimAsync;

            if (targetText != null)
            {
                targetText.text = targetValue.ToString("N0");
            }

            if (rewardText != null && reward != null)
            {
                rewardText.text = $"+{reward.Amount}";
            }

            UpdateVisuals();

            if (claimButton != null)
            {
                claimButton.onClick.RemoveAllListeners();
                claimButton.onClick.AddListener(OnClaimClicked);
            }
        }

        private void UpdateVisuals()
        {
            if (claimButton != null)
            {
                claimButton.gameObject.SetActive(!_isClaimed && _isReachable);
                claimButton.interactable = !_isClaimed && _isReachable;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = _isClaimed ? 0.5f : 1f;
            }

            if (claimButtonText != null)
            {
                claimButtonText.text = _isClaimed ? "CLAIMED" : "CLAIM";
            }
        }

        private void OnClaimClicked()
        {
            if (_isClaimed || !_isReachable) return;

            _onClaimAsync?.Invoke(this).Forget();
        }

        public void SetProcessing(bool processing)
        {
            if (processingIndicator != null)
            {
                processingIndicator.SetActive(processing);
            }

            if (claimButton != null)
            {
                claimButton.interactable = !processing && !_isClaimed && _isReachable;
            }
        }

        public void SetClaimed(bool claimed)
        {
            _isClaimed = claimed;
            UpdateVisuals();
        }
    }
}
