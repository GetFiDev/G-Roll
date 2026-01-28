using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GRoll.Core.SceneManagement.LoadingScene
{
    /// <summary>
    /// Loading Scene'in ana controller'i
    /// Tum sahne gecislerinde kullanilan unified loading ekrani
    /// </summary>
    public class LoadingSceneController : MonoBehaviour
    {
        public static LoadingSceneController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image progressBar;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private GameObject spinner;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float minimumDisplayTime = 0.5f;

        private float _showTime;
        private bool _isVisible;

        // Loading tiplerine gore varsayilan mesajlar
        private static readonly Dictionary<LoadingType, string[]> StatusMessages = new()
        {
            [LoadingType.SceneTransition] = new[] { "Yukleniyor...", "Hazirlaniyor..." },
            [LoadingType.NetworkOperation] = new[] { "Baglaniyor...", "Sunucu ile iletisim..." },
            [LoadingType.DataSync] = new[] { "Veriler senkronize ediliyor...", "Profil yukleniyor..." }
        };

        // Ipuclari (loading sirasinda gosterilecek)
        private static readonly string[] Tips = new[]
        {
            "Ipucu: Kombinasyonlari kullanarak daha yuksek puan al!",
            "Ipucu: Her gun giris yaparak oduller kazan!",
            "Ipucu: Enerji bittiginde reklam izleyerek doldurabilirsin!"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Baslangicta gizli
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Loading ekranini gosterir
        /// </summary>
        public async UniTask ShowAsync(LoadingType type = LoadingType.SceneTransition, string customMessage = null)
        {
            _showTime = Time.time;
            _isVisible = true;

            // Mesaji ayarla
            SetStatusMessage(customMessage ?? GetDefaultMessage(type));

            // Rastgele bir ipucu goster
            SetTip(Tips[Random.Range(0, Tips.Length)]);

            // Progress bar'i sifirla
            SetProgress(0f);

            // Spinner'i aktif et
            if (spinner != null)
                spinner.SetActive(true);

            // Fade in
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                await FadeCanvasGroup(canvasGroup, 0f, 1f, fadeInDuration);
            }
        }

        /// <summary>
        /// Loading ekranini gizler
        /// </summary>
        public async UniTask HideAsync()
        {
            // Minimum gosterim suresi
            float elapsed = Time.time - _showTime;
            if (elapsed < minimumDisplayTime)
            {
                await UniTask.Delay((int)((minimumDisplayTime - elapsed) * 1000));
            }

            // Fade out
            if (canvasGroup != null)
            {
                await FadeCanvasGroup(canvasGroup, 1f, 0f, fadeOutDuration);
                canvasGroup.blocksRaycasts = false;
            }

            _isVisible = false;
        }

        /// <summary>
        /// Progress degerini gunceller (0-1 arasi)
        /// </summary>
        public void SetProgress(float progress)
        {
            if (progressBar != null)
            {
                progressBar.fillAmount = Mathf.Clamp01(progress);
            }
        }

        /// <summary>
        /// Status mesajini gunceller
        /// </summary>
        public void SetStatusMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// Ipucu metnini ayarlar
        /// </summary>
        public void SetTip(string tip)
        {
            if (tipText != null)
            {
                tipText.text = tip;
            }
        }

        /// <summary>
        /// Loading ekrani gorunur mu?
        /// </summary>
        public bool IsVisible => _isVisible;

        private string GetDefaultMessage(LoadingType type)
        {
            if (StatusMessages.TryGetValue(type, out var messages) && messages.Length > 0)
            {
                return messages[Random.Range(0, messages.Length)];
            }
            return "Yukleniyor...";
        }

        private async UniTask FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (duration <= 0)
            {
                cg.alpha = to;
                return;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease out quad for fade in, ease in quad for fade out
                float eased = from < to
                    ? 1f - (1f - t) * (1f - t) // EaseOutQuad
                    : t * t; // EaseInQuad

                cg.alpha = Mathf.Lerp(from, to, eased);
                await UniTask.Yield();
            }

            cg.alpha = to;
        }
    }
}
