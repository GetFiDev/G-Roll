using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Achievement card component displaying achievement summary with progress.
    /// Shows claimable badge when reward is available.
    /// </summary>
    public class AchievementCard : MonoBehaviour
    {
        public enum VisualState
        {
            Locked,
            InProgress,
            Claimable,
            Completed
        }

        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button cardButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Badges")]
        [SerializeField] private GameObject claimableBadge;
        [SerializeField] private GameObject completedBadge;
        [SerializeField] private GameObject lockedOverlay;

        [Header("State Sprites")]
        [SerializeField] private Sprite lockedBackground;
        [SerializeField] private Sprite inProgressBackground;
        [SerializeField] private Sprite claimableBackground;
        [SerializeField] private Sprite completedBackground;

        [Header("Animation")]
        [SerializeField] private float iconFadeDuration = 0.25f;

        [Inject] private IAchievementService _achievementService;
        [Inject] private INavigationService _navigationService;

        private Achievement _achievementData;
        private VisualState _currentState;
        private Coroutine _fadeCoroutine;

        public event Action<AchievementCard, Achievement> OnClicked;

        private void Awake()
        {
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(OnCardClicked);
            }
        }

        public void SetData(Achievement achievement, VisualState state)
        {
            _achievementData = achievement;
            _currentState = state;

            if (nameText != null)
            {
                nameText.text = achievement.Name;
            }

            UpdateProgress(achievement.CurrentProgress, achievement.TargetProgress);
            UpdateVisualState(state);
        }

        public void UpdateProgress(int current, int target)
        {
            if (_achievementData != null)
            {
                _achievementData.CurrentProgress = current;
            }

            if (progressText != null)
            {
                if (Mathf.Approximately(current, Mathf.Round(current)) &&
                    Mathf.Approximately(target, Mathf.Round(target)))
                {
                    progressText.text = $"{(int)current}/{(int)target}";
                }
                else
                {
                    progressText.text = $"{current:F1}/{target:F1}";
                }
            }

            if (progressSlider != null)
            {
                progressSlider.value = target > 0 ? (float)current / target : 0f;
            }
        }

        public void UpdateVisualState(VisualState state)
        {
            _currentState = state;

            if (backgroundImage != null)
            {
                backgroundImage.sprite = state switch
                {
                    VisualState.Locked => lockedBackground,
                    VisualState.InProgress => inProgressBackground,
                    VisualState.Claimable => claimableBackground,
                    VisualState.Completed => completedBackground,
                    _ => inProgressBackground
                };
            }

            if (claimableBadge != null)
            {
                claimableBadge.SetActive(state == VisualState.Claimable);
            }

            if (completedBadge != null)
            {
                completedBadge.SetActive(state == VisualState.Completed);
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(state == VisualState.Locked);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = state == VisualState.Locked ? 0.5f : 1f;
            }

            if (cardButton != null)
            {
                cardButton.interactable = state != VisualState.Locked;
            }
        }

        public async UniTask LoadIconAsync(string iconUrl)
        {
            if (iconImage == null || string.IsNullOrEmpty(iconUrl)) return;

            iconImage.color = new Color(1f, 1f, 1f, 0f);

            try
            {
                using var request = UnityWebRequestTexture.GetTexture(iconUrl);
                await request.SendWebRequest();

                if (this == null) return;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);
                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    iconImage.sprite = sprite;
                    await FadeIconAlpha(0f, 1f, iconFadeDuration);
                }
            }
            catch
            {
                iconImage.color = Color.white;
            }
        }

        private async UniTask FadeIconAlpha(float from, float to, float duration)
        {
            if (iconImage == null) return;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (this == null) return;

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var alpha = Mathf.Lerp(from, to, t);
                iconImage.color = new Color(1f, 1f, 1f, alpha);
                await UniTask.Yield();
            }

            if (iconImage != null)
            {
                iconImage.color = new Color(1f, 1f, 1f, to);
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null && sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = Color.white;
            }
        }

        private void OnCardClicked()
        {
            OnClicked?.Invoke(this, _achievementData);
        }

        public static VisualState DetermineState(Achievement achievement)
        {
            if (achievement == null) return VisualState.Locked;

            if (achievement.IsClaimed)
                return VisualState.Completed;

            if (achievement.IsUnlocked)
                return VisualState.Claimable;

            if (achievement.CurrentProgress > 0)
                return VisualState.InProgress;

            return VisualState.Locked;
        }

        public string AchievementId => _achievementData?.AchievementId;
        public VisualState CurrentState => _currentState;
        public Achievement Data => _achievementData;
    }
}
