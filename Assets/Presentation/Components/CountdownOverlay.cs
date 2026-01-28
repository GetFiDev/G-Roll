using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Countdown overlay component for game start sequence (3-2-1-GO!).
    /// Shows animated numbers with scale and fade effects.
    /// </summary>
    public class CountdownOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform contentTransform;

        [Header("Settings")]
        [SerializeField] private int startCount = 3;
        [SerializeField] private float numberDisplayDuration = 0.8f;
        [SerializeField] private string goText = "GO!";

        [Header("Animation")]
        [SerializeField] private float scaleIn = 1.5f;
        [SerializeField] private float scaleOut = 0.8f;
        [SerializeField] private float fadeInDuration = 0.1f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Colors")]
        [SerializeField] private Color numberColor = Color.white;
        [SerializeField] private Color goColor = new Color(0.5f, 1f, 0.5f, 1f);

        [Header("Audio")]
        [SerializeField] private AudioClip countSound;
        [SerializeField] private AudioClip goSound;
        [SerializeField] private AudioSource audioSource;

        public event Action OnCountdownComplete;

        private bool _isCountingDown;

        public async UniTask StartCountdownAsync()
        {
            if (_isCountingDown) return;

            _isCountingDown = true;
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            try
            {
                for (int i = startCount; i >= 1; i--)
                {
                    await ShowNumberAsync(i.ToString(), numberColor);
                }

                await ShowNumberAsync(goText, goColor, true);
            }
            finally
            {
                _isCountingDown = false;
                await FadeOutAsync();
                gameObject.SetActive(false);
                OnCountdownComplete?.Invoke();
            }
        }

        private async UniTask ShowNumberAsync(string text, Color color, bool isGo = false)
        {
            if (countdownText == null) return;

            countdownText.text = text;
            countdownText.color = color;

            if (isGo && goSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(goSound);
            }
            else if (!isGo && countSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(countSound);
            }

            await AnimateNumberAsync(isGo);

            if (!isGo)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(numberDisplayDuration - fadeInDuration - fadeOutDuration));
            }
        }

        private async UniTask AnimateNumberAsync(bool isGo)
        {
            if (contentTransform == null) return;

            countdownText.alpha = 0f;
            contentTransform.localScale = Vector3.one * scaleIn;

            var elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeInDuration);
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                countdownText.alpha = easedT;
                contentTransform.localScale = Vector3.Lerp(Vector3.one * scaleIn, Vector3.one, easedT);

                await UniTask.Yield();
            }

            countdownText.alpha = 1f;
            contentTransform.localScale = Vector3.one;

            if (isGo)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
            }

            elapsed = 0f;
            var startScale = contentTransform.localScale;
            var targetScale = isGo ? Vector3.one * 1.5f : Vector3.one * scaleOut;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeOutDuration);
                var easedT = t * t;

                countdownText.alpha = 1f - easedT;
                contentTransform.localScale = Vector3.Lerp(startScale, targetScale, easedT);

                await UniTask.Yield();
            }

            countdownText.alpha = 0f;
        }

        private async UniTask FadeOutAsync()
        {
            if (canvasGroup == null) return;

            var elapsed = 0f;
            var duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                await UniTask.Yield();
            }

            canvasGroup.alpha = 0f;
        }

        public void Cancel()
        {
            _isCountingDown = false;
            gameObject.SetActive(false);
        }

        public bool IsCountingDown => _isCountingDown;
    }
}
