using System;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using AssetKits.ParticleImage;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using VContainer;

namespace GRoll.Presentation.Gameplay
{
    /// <summary>
    /// Handles gameplay UI visuals: score text, coin display, combo indicator, booster slider.
    /// Uses MessageBus pattern for loose coupling with gameplay systems.
    /// </summary>
    public class GameplayVisualApplier : MonoBehaviour
    {
        [Header("Text UI")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private bool scoreAsInteger = true;
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private bool coinAsInteger = true;

        [Header("Combo UI")]
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private float comboDecreaseDuration = 0.5f;
        [SerializeField] private float comboIncreaseDuration = 0.12f;
        [SerializeField] private float comboBounceScale = 1.08f;

        [Header("Booster UI")]
        [SerializeField] private Slider boosterSlider;

        [Header("Coin UI FX")]
        [SerializeField] private ParticleImage coinPickupParticle;
        [SerializeField] private Vector2 coinFXScreenOffset = Vector2.zero;
        [SerializeField] private Camera uiCameraOverride;
        [SerializeField] private Camera worldCameraForCoinFX;
        [SerializeField] private bool autoPlayUIParticle = true;
        [SerializeField] private bool coinFXUseInstancePerBurst = true;
        [SerializeField] private float coinFXDespawnSeconds = 1.5f;

        [SerializeField] private Transform playerTransformForCoinFX;
        private int lastFXFrame = -1;

        [Header("Coin Text Bounce")]
        [SerializeField] private UnityEngine.Events.UnityEvent onCoinPickupFX;

        private Vector3 coinTextBaseScale = Vector3.one;

        private float displayedCombo = 1f;
        private Color comboBaseColor = Color.white;
        private Tween comboTween;
        private Tween comboColorTween;

        private float _coinTotal = 0f;

        private IMessageBus _messageBus;
        private IGameplaySpeedService _speedService;
        private CompositeDisposable _subscriptions = new();
        private bool _isBound = false;

        [Inject]
        public void Construct(IMessageBus messageBus, IGameplaySpeedService speedService)
        {
            _messageBus = messageBus;
            _speedService = speedService;
        }

        private void Start()
        {
            if (_messageBus == null)
            {
                var container = FindFirstObjectByType<VContainer.Unity.LifetimeScope>();
                if (container != null)
                {
                    try
                    {
                        _messageBus = container.Container.Resolve<IMessageBus>();
                        _speedService = container.Container.Resolve<IGameplaySpeedService>();
                    }
                    catch
                    {
                        Debug.LogWarning("[GameplayVisualApplier] Could not resolve dependencies from container");
                    }
                }
            }

            if (_messageBus != null && !_isBound)
            {
                Bind();
            }
        }

        public void Bind()
        {
            if (_messageBus == null)
            {
                Debug.LogWarning("[GameplayVisualApplier] Cannot bind - IMessageBus not available");
                return;
            }

            Unbind();
            _isBound = true;

            _subscriptions.Add(_messageBus.Subscribe<GameplaySessionStartedMessage>(msg => HandleRunStarted(false)));
            _subscriptions.Add(_messageBus.Subscribe<GameplaySessionEndedMessage>(msg => HandleRunStopped()));
            _subscriptions.Add(_messageBus.Subscribe<CurrencyCollectedMessage>(msg => {
                _coinTotal += msg.Amount;
                HandleCoinsChanged(_coinTotal, msg.Amount);
            }));
            _subscriptions.Add(_messageBus.Subscribe<BoosterFillChangedMessage>(msg => HandleBoosterChanged(msg.NewFill, msg.MinFill, msg.MaxFill)));
            _subscriptions.Add(_messageBus.Subscribe<CoinPickupFXRequestMessage>(msg => HandleCoinFXRequest(msg.WorldPosition, msg.Count)));
            _subscriptions.Add(_messageBus.Subscribe<ComboChangedMessage>(msg => HandleComboChanged(msg.NewPower)));
            _subscriptions.Add(_messageBus.Subscribe<ComboResetMessage>(msg => HandleComboReset()));

            if (_speedService != null)
            {
                _speedService.OnBoosterFillChanged += HandleBoosterFillDirect;
                _speedService.OnComboPowerChanged += HandleComboChanged;
                _speedService.OnComboReset += HandleComboReset;
            }
        }

        public void Unbind()
        {
            if (!_isBound) return;

            ClearCoinFXAnchor();
            _subscriptions.Dispose();
            _subscriptions = new CompositeDisposable();

            if (_speedService != null)
            {
                _speedService.OnBoosterFillChanged -= HandleBoosterFillDirect;
                _speedService.OnComboPowerChanged -= HandleComboChanged;
                _speedService.OnComboReset -= HandleComboReset;
            }

            comboTween?.Kill();
            comboColorTween?.Kill();

            _isBound = false;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleBoosterFillDirect(float fill)
        {
            HandleBoosterChanged(fill, 0f, 1f);
        }

        private void HandleRunStarted(bool isReviveResume)
        {
            if (isReviveResume) return;

            _coinTotal = 0f;
            if (scoreText) scoreText.text = "0";
            if (coinText)
            {
                coinTextBaseScale = coinText.transform.localScale;
                coinText.text = "0.000";
            }
            if (boosterSlider) boosterSlider.value = 0f;
            if (comboText)
            {
                comboBaseColor = comboText.color;
                int startCombo = _speedService != null ? _speedService.CurrentComboPower : 25;
                displayedCombo = startCombo;
                comboText.text = $"{startCombo}";
                comboText.transform.localScale = Vector3.one;
            }
        }

        public void ForceResetCounters()
        {
            if (scoreText) scoreText.text = "0";
            if (coinText)
            {
                coinTextBaseScale = coinText.transform.localScale;
                coinText.text = "0.000";
            }
            if (boosterSlider) boosterSlider.value = 0f;
            if (comboText)
            {
                comboBaseColor = comboText.color;
                int startCombo = _speedService != null ? _speedService.CurrentComboPower : 25;
                displayedCombo = startCombo;
                comboText.text = $"{startCombo}";
                comboText.transform.localScale = Vector3.one;
            }
        }

        private void HandleRunStopped() { }

        private void HandleScoreChanged(float score)
        {
            if (!scoreText) return;
            scoreText.text = scoreAsInteger ? Mathf.FloorToInt(score).ToString() : score.ToString("F2");
        }

        private void HandleCoinsChanged(float total, float delta)
        {
            if (coinText)
            {
                coinText.text = total.ToString("F3");

                var t = coinText.transform;
                t.DOKill();
                if (coinTextBaseScale == Vector3.one) coinTextBaseScale = t.localScale;
                t.localScale = coinTextBaseScale;
                t.DOScale(coinTextBaseScale * 1.15f, 0.08f).SetEase(Ease.OutQuad)
                    .OnComplete(() => t.DOScale(coinTextBaseScale, 0.08f).SetEase(Ease.InQuad));
            }

            onCoinPickupFX?.Invoke();

            if (playerTransformForCoinFX != null && lastFXFrame != Time.frameCount)
            {
                EmitCoinFXAtWorld(playerTransformForCoinFX.position, 1);
                lastFXFrame = Time.frameCount;
            }
        }

        private void HandleBoosterChanged(float fill, float min, float max)
        {
            if (!boosterSlider) return;
            float denom = Mathf.Max(0.0001f, max - min);
            boosterSlider.value = (fill - min) / denom;
        }

        private void HandleCoinFXRequest(Vector3 worldPos, int count)
        {
            EmitCoinFXAtWorld(worldPos, Mathf.Max(1, count));
            lastFXFrame = Time.frameCount;
        }

        private void EmitCoinFXAtWorld(Vector3 worldPos, int count)
        {
            if (coinPickupParticle == null) { onCoinPickupFX?.Invoke(); return; }
            var canvas = coinPickupParticle.canvas;
            if (canvas == null) { onCoinPickupFX?.Invoke(); return; }

            Camera worldCam = worldCameraForCoinFX != null ? worldCameraForCoinFX
                : (uiCameraOverride != null ? uiCameraOverride : Camera.main);
            if (worldCam == null) { onCoinPickupFX?.Invoke(); return; }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) { onCoinPickupFX?.Invoke(); return; }

            Vector3 screen = RectTransformUtility.WorldToScreenPoint(worldCam, worldPos);
            if (screen.z < 0f) { onCoinPickupFX?.Invoke(); return; }

            Camera uiCam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
                uiCam = uiCameraOverride != null ? uiCameraOverride : canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out var local))
            {
                onCoinPickupFX?.Invoke();
                return;
            }

            int burstCount = 1;

            if (coinFXUseInstancePerBurst)
            {
                var inst = Instantiate(coinPickupParticle, canvas.transform);
                var rt = inst.rectTransform;

                if (canvas.renderMode == RenderMode.WorldSpace)
                    rt.position = canvasRect.TransformPoint(local) + (Vector3)coinFXScreenOffset;
                else
                    rt.anchoredPosition = local + coinFXScreenOffset;

                inst.EmitBurstNow(burstCount, autoPlayUIParticle);
                Destroy(inst.gameObject, Mathf.Max(0.05f, coinFXDespawnSeconds));
            }
            else
            {
                var rt = coinPickupParticle.rectTransform;

                if (canvas.renderMode == RenderMode.WorldSpace)
                    rt.position = canvasRect.TransformPoint(local) + (Vector3)coinFXScreenOffset;
                else
                    rt.anchoredPosition = local + coinFXScreenOffset;

                coinPickupParticle.EmitBurstNow(burstCount, autoPlayUIParticle);
            }

            onCoinPickupFX?.Invoke();
        }

        public void SetCoinFXAnchor(Transform anchor)
        {
            playerTransformForCoinFX = anchor;
        }

        public void ClearCoinFXAnchor()
        {
            playerTransformForCoinFX = null;
        }

        private void HandleComboChanged(int newCombo)
        {
            if (!comboText) return;
            float dur = newCombo < displayedCombo ? comboDecreaseDuration : comboIncreaseDuration;

            comboTween?.Kill();
            float start = displayedCombo;
            comboTween = DOTween.To(() => start, v => {
                start = v;
                displayedCombo = v;
                comboText.text = $"{Mathf.RoundToInt(v)}";
            }, (float)newCombo, Mathf.Max(0.01f, dur));

            var t = comboText.transform;
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOScale(Vector3.one * comboBounceScale, dur * 0.5f).SetEase(Ease.OutQuad)
                .OnComplete(() => t.DOScale(Vector3.one, dur * 0.5f).SetEase(Ease.InQuad));
        }

        private void HandleComboReset()
        {
            if (!comboText) return;

            comboColorTween?.Kill();
            comboText.color = comboBaseColor;
            // Use DOTween.To for TMP color animation (DOColor extension requires DOTween Pro TMPro module)
            Color currentColor = comboBaseColor;
            comboColorTween = DOTween.To(
                () => currentColor,
                c => { currentColor = c; comboText.color = c; },
                Color.red,
                comboDecreaseDuration * 0.4f
            ).OnComplete(() =>
            {
                DOTween.To(
                    () => currentColor,
                    c => { currentColor = c; comboText.color = c; },
                    comboBaseColor,
                    comboDecreaseDuration * 0.6f
                );
            });
        }
    }
}
