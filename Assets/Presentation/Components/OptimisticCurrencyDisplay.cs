using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Currency display that shows optimistic updates and rollback animations.
    /// Subscribes to CurrencyChangedMessage and animates value changes.
    /// Shows special rollback animation when optimistic update is reverted.
    /// </summary>
    public class OptimisticCurrencyDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private RectTransform iconTransform;

        [Header("Currency Type")]
        [SerializeField] private CurrencyType currencyType = CurrencyType.SoftCurrency;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private Color increaseColor = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color decreaseColor = new Color(1f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color rollbackColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Header("Shake")]
        [SerializeField] private float shakeMagnitude = 5f;

        [Inject] private ICurrencyService _currencyService;
        [Inject] private IMessageBus _messageBus;

        private int _displayedAmount;
        private Color _originalColor;
        private IDisposable _subscription;
        private Vector3 _originalPosition;
        private bool _isAnimating;
        private bool _isInitialized;

        private void Awake()
        {
            if (amountText != null)
            {
                _originalColor = amountText.color;
            }
            _originalPosition = transform.localPosition;
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            // Handle case where DI hasn't injected services yet
            if (_currencyService == null || _messageBus == null)
            {
                // Show default value and retry on next frame
                UpdateDisplay(0);
                UniTask.Void(async () =>
                {
                    await UniTask.Yield();
                    Initialize();
                });
                return;
            }

            _displayedAmount = _currencyService.GetBalance(currencyType);
            UpdateDisplay(_displayedAmount);

            // Subscribe to changes
            _subscription = _messageBus.Subscribe<CurrencyChangedMessage>(OnCurrencyChanged);
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        private void OnCurrencyChanged(CurrencyChangedMessage msg)
        {
            if (msg.Type != currencyType) return;

            AnimateChange(msg.PreviousAmount, msg.NewAmount, msg.IsOptimistic).Forget();
        }

        private async UniTaskVoid AnimateChange(int from, int to, bool isOptimistic)
        {
            // Wait if already animating
            while (_isAnimating)
            {
                await UniTask.Yield();
            }

            _isAnimating = true;

            try
            {
                var delta = to - from;
                var color = delta > 0 ? increaseColor : decreaseColor;

                // If this is a rollback (optimistic false and different from displayed)
                // This means server corrected our optimistic update
                if (!isOptimistic && _displayedAmount != from)
                {
                    color = rollbackColor;
                    await PlayShakeAnimationAsync();
                }

                // Animate number counting
                await AnimateCountAsync(to, color);
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private async UniTask AnimateCountAsync(int targetAmount, Color highlightColor)
        {
            var elapsed = 0f;
            var startAmount = _displayedAmount;

            if (amountText != null)
            {
                amountText.color = highlightColor;
            }

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / animationDuration);
                // EaseOutCubic
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                _displayedAmount = Mathf.RoundToInt(Mathf.Lerp(startAmount, targetAmount, easedT));
                UpdateDisplay(_displayedAmount);

                await UniTask.Yield();
            }

            _displayedAmount = targetAmount;
            UpdateDisplay(_displayedAmount);

            // Fade back to original color
            await FadeColorAsync(highlightColor, _originalColor, 0.2f);
        }

        private async UniTask FadeColorAsync(Color from, Color to, float duration)
        {
            if (amountText == null) return;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                amountText.color = Color.Lerp(from, to, t);
                await UniTask.Yield();
            }

            amountText.color = to;
        }

        private async UniTask PlayShakeAnimationAsync()
        {
            _originalPosition = transform.localPosition;

            for (int i = 0; i < 4; i++)
            {
                var offset = new Vector3(
                    UnityEngine.Random.Range(-shakeMagnitude, shakeMagnitude),
                    0, 0);
                transform.localPosition = _originalPosition + offset;
                await UniTask.Delay(30);
            }

            transform.localPosition = _originalPosition;
        }

        private void UpdateDisplay(int amount)
        {
            if (amountText != null)
            {
                amountText.text = FormatCurrency(amount);
            }
        }

        private string FormatCurrency(int amount)
        {
            if (amount >= 1000000)
                return $"{amount / 1000000f:F1}M";
            if (amount >= 1000)
                return $"{amount / 1000f:F1}K";
            return amount.ToString("N0");
        }

        /// <summary>
        /// Manually set the displayed amount (without animation).
        /// </summary>
        public void SetAmount(int amount)
        {
            _displayedAmount = amount;
            UpdateDisplay(amount);
        }

        /// <summary>
        /// Force refresh from currency service.
        /// </summary>
        public void Refresh()
        {
            if (_currencyService != null)
            {
                var newAmount = _currencyService.GetBalance(currencyType);
                if (newAmount != _displayedAmount)
                {
                    AnimateChange(_displayedAmount, newAmount, false).Forget();
                }
            }
        }
    }
}
