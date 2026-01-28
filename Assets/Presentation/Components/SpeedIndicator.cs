using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Speed indicator component for gameplay HUD.
    /// Shows current speed with color coding based on range.
    /// </summary>
    public class SpeedIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image glowImage;
        [SerializeField] private RectTransform needleTransform;

        [Header("Settings")]
        [SerializeField] private float minSpeed = 0f;
        [SerializeField] private float maxSpeed = 100f;
        [SerializeField] private float animationSpeed = 10f;
        [SerializeField] private string speedFormat = "F0";
        [SerializeField] private string speedSuffix = "";

        [Header("Colors")]
        [SerializeField] private Gradient speedGradient;
        [SerializeField] private Color lowSpeedColor = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color midSpeedColor = new Color(1f, 1f, 0.5f, 1f);
        [SerializeField] private Color highSpeedColor = new Color(1f, 0.5f, 0.5f, 1f);

        [Header("Needle (Optional)")]
        [SerializeField] private float minNeedleAngle = -90f;
        [SerializeField] private float maxNeedleAngle = 90f;

        [Header("Warning")]
        [SerializeField] private float warningThreshold = 0.8f;
        [SerializeField] private float warningPulseSpeed = 3f;
        [SerializeField] private GameObject warningIndicator;

        private float _currentDisplaySpeed;
        private float _targetSpeed;
        private bool _isWarning;

        private void Update()
        {
            if (Mathf.Abs(_currentDisplaySpeed - _targetSpeed) > 0.1f)
            {
                _currentDisplaySpeed = Mathf.Lerp(_currentDisplaySpeed, _targetSpeed, Time.deltaTime * animationSpeed);
                UpdateVisuals();
            }

            if (_isWarning && glowImage != null)
            {
                var alpha = (Mathf.Sin(Time.time * warningPulseSpeed) + 1f) * 0.5f;
                glowImage.color = new Color(highSpeedColor.r, highSpeedColor.g, highSpeedColor.b, alpha * 0.5f);
            }
        }

        public void SetSpeed(float speed, bool animate = true)
        {
            _targetSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);

            if (!animate)
            {
                _currentDisplaySpeed = _targetSpeed;
                UpdateVisuals();
            }

            CheckWarningState();
        }

        public void SetSpeedRange(float min, float max)
        {
            minSpeed = min;
            maxSpeed = max;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            var normalizedSpeed = GetNormalizedSpeed();

            if (speedText != null)
            {
                speedText.text = _currentDisplaySpeed.ToString(speedFormat) + speedSuffix;
                speedText.color = GetSpeedColor(normalizedSpeed);
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = normalizedSpeed;
                fillImage.color = GetSpeedColor(normalizedSpeed);
            }

            if (needleTransform != null)
            {
                var angle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, normalizedSpeed);
                needleTransform.localRotation = Quaternion.Euler(0, 0, -angle);
            }
        }

        private float GetNormalizedSpeed()
        {
            var range = maxSpeed - minSpeed;
            return range > 0 ? (_currentDisplaySpeed - minSpeed) / range : 0f;
        }

        private Color GetSpeedColor(float normalized)
        {
            if (speedGradient != null)
            {
                return speedGradient.Evaluate(normalized);
            }

            if (normalized < 0.5f)
            {
                return Color.Lerp(lowSpeedColor, midSpeedColor, normalized * 2f);
            }

            return Color.Lerp(midSpeedColor, highSpeedColor, (normalized - 0.5f) * 2f);
        }

        private void CheckWarningState()
        {
            var wasWarning = _isWarning;
            _isWarning = GetNormalizedSpeed() >= warningThreshold;

            if (warningIndicator != null)
            {
                warningIndicator.SetActive(_isWarning);
            }

            if (glowImage != null)
            {
                glowImage.gameObject.SetActive(_isWarning);
            }

            if (_isWarning && !wasWarning)
            {
                PlayWarningPulse().Forget();
            }
        }

        private async UniTaskVoid PlayWarningPulse()
        {
            if (speedText == null) return;

            var originalScale = speedText.transform.localScale;

            for (int i = 0; i < 2; i++)
            {
                speedText.transform.localScale = originalScale * 1.2f;
                await UniTask.Delay(50);
                speedText.transform.localScale = originalScale;
                await UniTask.Delay(50);
            }
        }

        public float CurrentSpeed => _currentDisplaySpeed;
        public float TargetSpeed => _targetSpeed;
        public float NormalizedSpeed => GetNormalizedSpeed();
        public bool IsWarning => _isWarning;
    }
}
