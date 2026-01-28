using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Energy display component showing current/max energy with regeneration countdown.
    /// Subscribes to EnergyChangedMessage for real-time updates.
    /// </summary>
    public class EnergyDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject timerContainer;

        [Header("Heart Icons (Optional)")]
        [SerializeField] private Image[] heartIcons;
        [SerializeField] private Sprite heartFilledSprite;
        [SerializeField] private Sprite heartEmptySprite;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color lowEnergyColor = new Color(1f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color fullEnergyColor = new Color(0.5f, 1f, 0.5f, 1f);

        [Header("Settings")]
        [SerializeField] private bool showTimerWhenFull = false;
        [SerializeField] private int lowEnergyThreshold = 1;

        [Inject] private IEnergyService _energyService;
        [Inject] private IMessageBus _messageBus;
        [Inject] private INavigationService _navigationService;

        private IDisposable _subscription;
        private int _displayedEnergy;
        private int _displayedMaxEnergy;
        private bool _isInitialized;
        private bool _isTimerRunning;

        public event Action OnClicked;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            if (_energyService == null || _messageBus == null)
            {
                UpdateDisplay(0, 5, null);
                UniTask.Void(async () =>
                {
                    await UniTask.Yield();
                    Initialize();
                });
                return;
            }

            _displayedEnergy = _energyService.CurrentEnergy;
            _displayedMaxEnergy = _energyService.MaxEnergy;
            UpdateDisplay(_displayedEnergy, _displayedMaxEnergy, _energyService.NextRegenTime);

            _subscription = _messageBus.Subscribe<EnergyChangedMessage>(OnEnergyChanged);
            _isInitialized = true;

            StartTimerUpdate().Forget();
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _isTimerRunning = false;
        }

        private void OnEnergyChanged(EnergyChangedMessage msg)
        {
            AnimateChange(msg.PreviousEnergy, msg.CurrentEnergy, msg.MaxEnergy, msg.NextRegenTime).Forget();
        }

        private async UniTaskVoid AnimateChange(int from, int to, int max, DateTime? nextRegenTime)
        {
            var elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / animationDuration);
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                _displayedEnergy = Mathf.RoundToInt(Mathf.Lerp(from, to, easedT));
                UpdateDisplay(_displayedEnergy, max, nextRegenTime);

                await UniTask.Yield();
            }

            _displayedEnergy = to;
            _displayedMaxEnergy = max;
            UpdateDisplay(_displayedEnergy, _displayedMaxEnergy, nextRegenTime);
        }

        private void UpdateDisplay(int current, int max, DateTime? nextRegenTime)
        {
            if (energyText != null)
            {
                energyText.text = $"{current}/{max}";

                if (current <= lowEnergyThreshold)
                    energyText.color = lowEnergyColor;
                else if (current >= max)
                    energyText.color = fullEnergyColor;
                else
                    energyText.color = normalColor;
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = max > 0 ? (float)current / max : 0f;
            }

            UpdateHeartIcons(current, max);
            UpdateTimer(current, max, nextRegenTime);
        }

        private void UpdateHeartIcons(int current, int max)
        {
            if (heartIcons == null || heartIcons.Length == 0) return;

            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] != null)
                {
                    if (i < max)
                    {
                        heartIcons[i].gameObject.SetActive(true);
                        heartIcons[i].sprite = i < current ? heartFilledSprite : heartEmptySprite;
                    }
                    else
                    {
                        heartIcons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void UpdateTimer(int current, int max, DateTime? nextRegenTime)
        {
            bool shouldShowTimer = current < max || showTimerWhenFull;

            if (timerContainer != null)
            {
                timerContainer.SetActive(shouldShowTimer);
            }

            if (timerText != null && nextRegenTime.HasValue && current < max)
            {
                var remaining = nextRegenTime.Value - DateTime.UtcNow;
                if (remaining.TotalSeconds > 0)
                {
                    timerText.text = FormatTimeSpan(remaining);
                }
                else
                {
                    timerText.text = "00:00";
                }
            }
            else if (timerText != null)
            {
                timerText.text = current >= max ? "FULL" : "--:--";
            }
        }

        private async UniTaskVoid StartTimerUpdate()
        {
            _isTimerRunning = true;

            while (_isTimerRunning && this != null)
            {
                if (_energyService != null && _displayedEnergy < _displayedMaxEnergy)
                {
                    UpdateTimer(_displayedEnergy, _displayedMaxEnergy, _energyService.NextRegenTime);
                }

                await UniTask.Delay(1000);
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

        public void OnClick()
        {
            OnClicked?.Invoke();
        }

        public void Refresh()
        {
            if (_energyService != null)
            {
                _displayedEnergy = _energyService.CurrentEnergy;
                _displayedMaxEnergy = _energyService.MaxEnergy;
                UpdateDisplay(_displayedEnergy, _displayedMaxEnergy, _energyService.NextRegenTime);
            }
        }

        public int CurrentEnergy => _displayedEnergy;
        public int MaxEnergy => _displayedMaxEnergy;
        public bool IsFull => _displayedEnergy >= _displayedMaxEnergy;
    }
}
