using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Booster fill bar component for gameplay HUD.
    /// Shows fill progress and triggers effects when full.
    /// </summary>
    public class BoosterFillBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image glowImage;
        [SerializeField] private RectTransform fillContainer;

        [Header("Settings")]
        [SerializeField] private float maxFillAmount = 100f;
        [SerializeField] private float fillAnimationSpeed = 5f;
        [SerializeField] private float glowPulseSpeed = 2f;

        [Header("Colors")]
        [SerializeField] private Gradient fillGradient;
        [SerializeField] private Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color fullGlowColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Header("Effects")]
        [SerializeField] private ParticleSystem fillParticles;
        [SerializeField] private ParticleSystem fullParticles;

        [Inject] private IMessageBus _messageBus;

        private float _currentFill;
        private float _targetFill;
        private bool _isFull;
        private IDisposable _subscription;

        private void Start()
        {
            if (_messageBus != null)
            {
                _subscription = _messageBus.Subscribe<PlayerCollectMessage>(OnPlayerCollect);
            }

            UpdateVisuals();
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        private void Update()
        {
            if (Mathf.Abs(_currentFill - _targetFill) > 0.01f)
            {
                _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * fillAnimationSpeed);
                UpdateVisuals();
            }

            if (_isFull && glowImage != null)
            {
                var alpha = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
                glowImage.color = new Color(fullGlowColor.r, fullGlowColor.g, fullGlowColor.b, alpha * 0.5f);
            }
        }

        private void OnPlayerCollect(PlayerCollectMessage msg)
        {
            if (msg.ItemType == "booster")
            {
                AddFill(msg.Amount);
            }
        }

        public void AddFill(float amount)
        {
            _targetFill = Mathf.Clamp(_targetFill + amount, 0f, maxFillAmount);

            if (fillParticles != null && amount > 0)
            {
                fillParticles.Play();
            }

            CheckFullState();
        }

        public void SetFill(float amount, bool animate = true)
        {
            _targetFill = Mathf.Clamp(amount, 0f, maxFillAmount);

            if (!animate)
            {
                _currentFill = _targetFill;
                UpdateVisuals();
            }

            CheckFullState();
        }

        public void Reset()
        {
            _targetFill = 0f;
            _currentFill = 0f;
            _isFull = false;
            UpdateVisuals();

            if (glowImage != null)
            {
                glowImage.color = Color.clear;
            }
        }

        public void Use()
        {
            if (!_isFull) return;

            Reset();
        }

        private void CheckFullState()
        {
            var wasFull = _isFull;
            _isFull = _targetFill >= maxFillAmount;

            if (_isFull && !wasFull)
            {
                OnBecomeFull();
            }
        }

        private void OnBecomeFull()
        {
            if (fullParticles != null)
            {
                fullParticles.Play();
            }

            if (glowImage != null)
            {
                glowImage.gameObject.SetActive(true);
            }

            PlayPunchAnimation().Forget();
        }

        private async UniTaskVoid PlayPunchAnimation()
        {
            if (fillContainer == null) return;

            var originalScale = fillContainer.localScale;

            var elapsed = 0f;
            var duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                var scale = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
                fillContainer.localScale = originalScale * scale;
                await UniTask.Yield();
            }

            fillContainer.localScale = originalScale;
        }

        private void UpdateVisuals()
        {
            var fillRatio = maxFillAmount > 0 ? _currentFill / maxFillAmount : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = fillRatio;

                if (fillGradient != null)
                {
                    fillImage.color = fillGradient.Evaluate(fillRatio);
                }
            }
        }

        public float CurrentFill => _currentFill;
        public float MaxFill => maxFillAmount;
        public float FillRatio => maxFillAmount > 0 ? _currentFill / maxFillAmount : 0f;
        public bool IsFull => _isFull;
    }
}
