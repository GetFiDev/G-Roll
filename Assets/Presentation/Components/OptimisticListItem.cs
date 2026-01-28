using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// List item that shows pending/confirmed/rollback states.
    /// Used for inventory items, achievements, tasks etc.
    /// Visual states indicate optimistic operation progress.
    /// </summary>
    public class OptimisticListItem : MonoBehaviour
    {
        [Header("Visual States")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject pendingIndicator;
        [SerializeField] private GameObject confirmedIndicator;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pendingColor = new Color(1f, 1f, 0.8f, 1f);
        [SerializeField] private Color confirmedColor = new Color(0.8f, 1f, 0.8f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.8f, 0.8f, 1f);

        [Header("Animation")]
        [SerializeField] private float shakeMagnitude = 5f;
        [SerializeField] private float pulseMinAlpha = 0.8f;
        [SerializeField] private float pulseSpeed = 2f;

        private ItemState _currentState = ItemState.Normal;
        private Vector3 _originalPosition;
        private bool _isPulsing;

        public enum ItemState
        {
            Normal,
            Pending,
            Confirmed,
            Error
        }

        public ItemState CurrentState => _currentState;

        private void Awake()
        {
            _originalPosition = transform.localPosition;
        }

        /// <summary>
        /// Set the visual state of the item.
        /// </summary>
        public void SetState(ItemState state)
        {
            _currentState = state;

            switch (state)
            {
                case ItemState.Normal:
                    SetNormalState();
                    break;
                case ItemState.Pending:
                    SetPendingState();
                    break;
                case ItemState.Confirmed:
                    SetConfirmedState().Forget();
                    break;
                case ItemState.Error:
                    SetErrorState().Forget();
                    break;
            }
        }

        private void SetNormalState()
        {
            StopPulse();

            if (backgroundImage != null)
            {
                backgroundImage.color = normalColor;
            }

            if (pendingIndicator != null)
            {
                pendingIndicator.SetActive(false);
            }

            if (confirmedIndicator != null)
            {
                confirmedIndicator.SetActive(false);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            transform.localPosition = _originalPosition;
        }

        private void SetPendingState()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = pendingColor;
            }

            if (pendingIndicator != null)
            {
                pendingIndicator.SetActive(true);
            }

            if (confirmedIndicator != null)
            {
                confirmedIndicator.SetActive(false);
            }

            // Start pulse animation
            StartPulse().Forget();
        }

        private async UniTaskVoid StartPulse()
        {
            _isPulsing = true;

            while (_isPulsing && canvasGroup != null)
            {
                // Pulse alpha between min and 1
                var t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
                canvasGroup.alpha = Mathf.Lerp(pulseMinAlpha, 1f, t);
                await UniTask.Yield();
            }
        }

        private void StopPulse()
        {
            _isPulsing = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private async UniTaskVoid SetConfirmedState()
        {
            StopPulse();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (pendingIndicator != null)
            {
                pendingIndicator.SetActive(false);
            }

            if (confirmedIndicator != null)
            {
                confirmedIndicator.SetActive(true);
            }

            // Brief green flash
            if (backgroundImage != null)
            {
                await FlashColorAsync(confirmedColor, normalColor, 0.3f);
            }

            // Hide confirmed indicator after a delay
            await UniTask.Delay(1000);

            if (confirmedIndicator != null && _currentState == ItemState.Confirmed)
            {
                confirmedIndicator.SetActive(false);
            }

            // Return to normal if still in confirmed state
            if (_currentState == ItemState.Confirmed)
            {
                _currentState = ItemState.Normal;
                if (backgroundImage != null)
                {
                    backgroundImage.color = normalColor;
                }
            }
        }

        private async UniTaskVoid SetErrorState()
        {
            StopPulse();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (pendingIndicator != null)
            {
                pendingIndicator.SetActive(false);
            }

            if (confirmedIndicator != null)
            {
                confirmedIndicator.SetActive(false);
            }

            // Error shake and color
            if (backgroundImage != null)
            {
                backgroundImage.color = errorColor;
            }

            // Shake animation
            await PlayShakeAnimationAsync();

            // Fade back to normal
            await UniTask.Delay(500);

            if (_currentState == ItemState.Error)
            {
                _currentState = ItemState.Normal;
                if (backgroundImage != null)
                {
                    backgroundImage.color = normalColor;
                }
            }
        }

        private async UniTask FlashColorAsync(Color from, Color to, float duration)
        {
            if (backgroundImage == null) return;

            backgroundImage.color = from;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                backgroundImage.color = Color.Lerp(from, to, t);
                await UniTask.Yield();
            }

            backgroundImage.color = to;
        }

        private async UniTask PlayShakeAnimationAsync()
        {
            _originalPosition = transform.localPosition;

            for (int i = 0; i < 3; i++)
            {
                transform.localPosition = _originalPosition + new Vector3(shakeMagnitude, 0, 0);
                await UniTask.Delay(40);
                transform.localPosition = _originalPosition - new Vector3(shakeMagnitude, 0, 0);
                await UniTask.Delay(40);
            }

            transform.localPosition = _originalPosition;
        }

        private void OnDisable()
        {
            StopPulse();
        }
    }
}
