using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Single stat display component showing icon, name, and value.
    /// Used for player stats like speed, combo power, etc.
    /// </summary>
    public class StatDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image backgroundImage;

        [Header("Animation")]
        [SerializeField] private float flashDuration = 0.3f;
        [SerializeField] private Color normalValueColor = Color.white;
        [SerializeField] private Color increaseColor = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color decreaseColor = new Color(1f, 0.5f, 0.5f, 1f);
        [SerializeField] private float punchScale = 1.2f;

        private float _currentValue;
        private bool _isAnimating;
        private Vector3 _originalScale;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void SetStat(string name, float value, Sprite icon = null)
        {
            if (nameText != null)
            {
                nameText.text = name;
            }

            if (icon != null && iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
            }

            UpdateValue(value, animate: false);
        }

        public void UpdateValue(float newValue, bool animate = true)
        {
            var oldValue = _currentValue;
            _currentValue = newValue;

            if (animate && oldValue != newValue)
            {
                AnimateValueChange(oldValue, newValue).Forget();
            }
            else
            {
                DisplayValue(newValue);
            }
        }

        private async UniTaskVoid AnimateValueChange(float from, float to)
        {
            if (_isAnimating)
            {
                _currentValue = to;
                DisplayValue(to);
                return;
            }

            _isAnimating = true;

            try
            {
                var flashColor = to > from ? increaseColor : decreaseColor;

                if (valueText != null)
                {
                    valueText.color = flashColor;
                }

                var elapsed = 0f;
                while (elapsed < flashDuration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / flashDuration);

                    var displayedValue = Mathf.Lerp(from, to, t);
                    DisplayValue(displayedValue);

                    var scaleT = t < 0.5f ? t * 2f : (1f - t) * 2f;
                    var scale = Mathf.Lerp(1f, punchScale, scaleT);
                    transform.localScale = _originalScale * scale;

                    await UniTask.Yield();
                }

                DisplayValue(to);
                transform.localScale = _originalScale;

                await FadeColor(flashColor, normalValueColor, 0.2f);
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private async UniTask FadeColor(Color from, Color to, float duration)
        {
            if (valueText == null) return;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                valueText.color = Color.Lerp(from, to, elapsed / duration);
                await UniTask.Yield();
            }

            valueText.color = to;
        }

        private void DisplayValue(float value)
        {
            if (valueText == null) return;

            if (Mathf.Approximately(value, Mathf.Round(value)))
            {
                valueText.text = Mathf.RoundToInt(value).ToString();
            }
            else
            {
                valueText.text = value.ToString("F1");
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }
        }

        public void SetName(string name)
        {
            if (nameText != null)
            {
                nameText.text = name;
            }
        }

        public void SetBackground(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }

        public float CurrentValue => _currentValue;
    }
}
