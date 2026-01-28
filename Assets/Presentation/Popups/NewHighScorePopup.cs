using System;
using Cysharp.Threading.Tasks;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// New high score celebration popup.
    /// Shows animated score with confetti and auto-closes.
    /// </summary>
    public class NewHighScorePopup : UIPopupBase
    {
        public class HighScoreParams
        {
            public int NewScore { get; set; }
            public int PreviousScore { get; set; }
        }

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI newScoreText;
        [SerializeField] private TextMeshProUGUI previousScoreText;

        [Header("Effects")]
        [SerializeField] private ParticleSystem confettiParticles;
        [SerializeField] private GameObject glowEffect;

        [Header("Animation")]
        [SerializeField] private float scoreCountUpDuration = 1f;
        [SerializeField] private float autoDismissDelay = 3f;
        [SerializeField] private float pulseScale = 1.1f;
        [SerializeField] private float pulseSpeed = 2f;

        [Header("Audio")]
        [SerializeField] private AudioClip celebrationSound;
        [SerializeField] private AudioSource audioSource;

        private HighScoreParams _params;
        private bool _isPulsing;

        protected override async UniTask OnPopupShowAsync(object parameters)
        {
            _params = parameters as HighScoreParams ?? new HighScoreParams();

            SetupUI();
            PlayCelebration();

            await PlayScoreAnimationAsync();

            StartAutoDismiss().Forget();
        }

        private void SetupUI()
        {
            if (titleText != null)
            {
                titleText.text = "NEW HIGH SCORE!";
            }

            if (previousScoreText != null)
            {
                previousScoreText.text = $"Previous: {_params.PreviousScore:N0}";
            }

            if (newScoreText != null)
            {
                newScoreText.text = "0";
            }

            if (glowEffect != null)
            {
                glowEffect.SetActive(true);
            }
        }

        private void PlayCelebration()
        {
            if (confettiParticles != null)
            {
                confettiParticles.Play();
            }

            if (celebrationSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(celebrationSound);
            }

            FeedbackService?.PlaySuccessHaptic();
        }

        private async UniTask PlayScoreAnimationAsync()
        {
            if (newScoreText == null) return;

            var elapsed = 0f;
            var startScale = newScoreText.transform.localScale;

            while (elapsed < scoreCountUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / scoreCountUpDuration);
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                var displayScore = Mathf.RoundToInt(Mathf.Lerp(0, _params.NewScore, easedT));
                newScoreText.text = displayScore.ToString("N0");

                var scaleMultiplier = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                newScoreText.transform.localScale = startScale * scaleMultiplier;

                await UniTask.Yield();
            }

            newScoreText.text = _params.NewScore.ToString("N0");
            newScoreText.transform.localScale = startScale;

            StartPulseAnimation().Forget();
        }

        private async UniTaskVoid StartPulseAnimation()
        {
            if (newScoreText == null) return;

            _isPulsing = true;
            var originalScale = newScoreText.transform.localScale;

            while (_isPulsing && this != null)
            {
                var scale = 1f + (pulseScale - 1f) * (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
                newScoreText.transform.localScale = originalScale * scale;
                await UniTask.Yield();
            }

            if (newScoreText != null)
            {
                newScoreText.transform.localScale = originalScale;
            }
        }

        private async UniTaskVoid StartAutoDismiss()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(autoDismissDelay), ignoreTimeScale: true);

            if (this != null && IsVisible)
            {
                Close();
            }
        }

        public void OnTapToDismiss()
        {
            FeedbackService?.PlaySelectionHaptic();
            Close();
        }

        public override bool OnBackPressed()
        {
            Close();
            return true;
        }

        protected override UniTask OnPopupHideAsync()
        {
            _isPulsing = false;

            if (confettiParticles != null)
            {
                confettiParticles.Stop();
            }

            return base.OnPopupHideAsync();
        }
    }
}
