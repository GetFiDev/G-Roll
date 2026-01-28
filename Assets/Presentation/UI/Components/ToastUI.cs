using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using GRoll.Core.Interfaces.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.UI.Components
{
    /// <summary>
    /// Toast UI component for displaying temporary notifications.
    /// Attach to a prefab with CanvasGroup, Image (background), and TMP_Text.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image iconImage;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 2f;
        [SerializeField] private float fadeDuration = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color infoColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color successColor = new Color(0.1f, 0.6f, 0.3f, 0.9f);
        [SerializeField] private Color warningColor = new Color(0.9f, 0.6f, 0.1f, 0.9f);
        [SerializeField] private Color errorColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);

        [Header("Icons (Optional)")]
        [SerializeField] private Sprite infoIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite warningIcon;
        [SerializeField] private Sprite errorIcon;

        private RectTransform _rectTransform;
        private Vector2 _originalPosition;
        private bool _isShowing;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            _rectTransform = GetComponent<RectTransform>();
            _originalPosition = _rectTransform.anchoredPosition;

            // Start hidden
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the toast with the specified message and type.
        /// Returns when the toast is fully dismissed.
        /// </summary>
        public async UniTask ShowAsync(string message, ToastType type)
        {
            if (_isShowing) return;
            _isShowing = true;

            // Configure appearance
            messageText.text = message;
            backgroundImage.color = GetColorForType(type);

            if (iconImage != null)
            {
                var icon = GetIconForType(type);
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            // Activate and animate in
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;

            // Slide in from top
            var startPos = _originalPosition + new Vector2(0, 100f);
            _rectTransform.anchoredPosition = startPos;

            // Animate
            var sequence = DOTween.Sequence();
            sequence.Append(DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 1f, fadeDuration));
            sequence.Join(DOTween.To(() => _rectTransform.anchoredPosition, x => _rectTransform.anchoredPosition = x, _originalPosition, fadeDuration).SetEase(Ease.OutBack));

            await UniTask.WaitUntil(() => !sequence.IsActive() || sequence.IsComplete());

            // Wait for display duration
            await UniTask.Delay(TimeSpan.FromSeconds(displayDuration));

            // Animate out
            var hideSequence = DOTween.Sequence();
            hideSequence.Append(DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 0f, fadeDuration));
            hideSequence.Join(DOTween.To(() => _rectTransform.anchoredPosition, x => _rectTransform.anchoredPosition = x, startPos, fadeDuration).SetEase(Ease.InBack));

            await UniTask.WaitUntil(() => !hideSequence.IsActive() || hideSequence.IsComplete());

            // Hide
            gameObject.SetActive(false);
            _isShowing = false;
        }

        private Color GetColorForType(ToastType type)
        {
            return type switch
            {
                ToastType.Success => successColor,
                ToastType.Warning => warningColor,
                ToastType.Error => errorColor,
                _ => infoColor
            };
        }

        private Sprite GetIconForType(ToastType type)
        {
            return type switch
            {
                ToastType.Success => successIcon,
                ToastType.Warning => warningIcon,
                ToastType.Error => errorIcon,
                _ => infoIcon
            };
        }

        private void OnValidate()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
