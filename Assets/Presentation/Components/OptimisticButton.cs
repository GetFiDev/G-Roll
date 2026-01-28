using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.UI;
using GRoll.Core.Optimistic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Button that handles optimistic operations with loading states.
    /// Provides visual feedback for pending, success, and error states.
    /// Follows the "No Loading Indicator" principle - states are brief visual feedback only.
    /// </summary>
    public class OptimisticButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private Image buttonImage;

        [Header("States")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pendingColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color successColor = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.5f, 0.5f, 1f);

        [Header("Animation")]
        [SerializeField] private float feedbackDuration = 0.3f;
        [SerializeField] private float shakeMagnitude = 5f;
        [SerializeField] private int shakeCount = 3;

        [Inject] private IFeedbackService _feedbackService;

        private string _originalText;
        private bool _isOperationInProgress;
        private Vector3 _originalPosition;

        public event Action OnClick;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }

            _originalText = buttonText != null ? buttonText.text : "";
            _originalPosition = transform.localPosition;

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
        }

        private void HandleClick()
        {
            if (_isOperationInProgress) return;
            _feedbackService?.PlaySelectionHaptic();
            OnClick?.Invoke();
        }

        /// <summary>
        /// Executes an optimistic operation with visual feedback.
        /// Button is disabled during operation to prevent double-tap.
        /// </summary>
        public async UniTask<OperationResult> ExecuteOptimisticAsync(
            Func<UniTask<OperationResult>> operation,
            string pendingText = null)
        {
            if (_isOperationInProgress)
                return OperationResult.ValidationError("Operation in progress");

            _isOperationInProgress = true;

            // 1. PENDING STATE (brief, just prevent double-tap)
            SetPendingState(pendingText);

            try
            {
                // 2. EXECUTE
                var result = await operation();

                // 3. RESULT STATE
                if (result.IsSuccess)
                {
                    await ShowSuccessStateAsync();
                }
                else
                {
                    await ShowErrorStateAsync(result.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                await ShowErrorStateAsync(ex.Message);
                return OperationResult.NetworkError(ex);
            }
            finally
            {
                _isOperationInProgress = false;
                ResetState();
            }
        }

        /// <summary>
        /// Executes an optimistic operation with typed result.
        /// </summary>
        public async UniTask<OperationResult<T>> ExecuteOptimisticAsync<T>(
            Func<UniTask<OperationResult<T>>> operation,
            string pendingText = null)
        {
            if (_isOperationInProgress)
                return OperationResult<T>.ValidationError("Operation in progress");

            _isOperationInProgress = true;

            // 1. PENDING STATE
            SetPendingState(pendingText);

            try
            {
                // 2. EXECUTE
                var result = await operation();

                // 3. RESULT STATE
                if (result.IsSuccess)
                {
                    await ShowSuccessStateAsync();
                }
                else
                {
                    await ShowErrorStateAsync(result.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                await ShowErrorStateAsync(ex.Message);
                return OperationResult<T>.NetworkError(ex);
            }
            finally
            {
                _isOperationInProgress = false;
                ResetState();
            }
        }

        private void SetPendingState(string pendingText)
        {
            if (button != null)
            {
                button.interactable = false;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            if (buttonText != null && !string.IsNullOrEmpty(pendingText))
            {
                buttonText.text = pendingText;
            }

            if (buttonImage != null)
            {
                buttonImage.color = pendingColor;
            }
        }

        private async UniTask ShowSuccessStateAsync()
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            if (buttonImage != null)
            {
                buttonImage.color = successColor;
            }

            _feedbackService?.PlaySuccessHaptic();

            await UniTask.Delay(TimeSpan.FromSeconds(feedbackDuration));
        }

        private async UniTask ShowErrorStateAsync(string error)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            if (buttonImage != null)
            {
                buttonImage.color = errorColor;
            }

            // Shake animation
            await PlayShakeAnimationAsync();

            _feedbackService?.PlayErrorHaptic();

            await UniTask.Delay(TimeSpan.FromSeconds(feedbackDuration));
        }

        private async UniTask PlayShakeAnimationAsync()
        {
            _originalPosition = transform.localPosition;

            for (int i = 0; i < shakeCount; i++)
            {
                transform.localPosition = _originalPosition + new Vector3(shakeMagnitude, 0, 0);
                await UniTask.Delay(50);
                transform.localPosition = _originalPosition - new Vector3(shakeMagnitude, 0, 0);
                await UniTask.Delay(50);
            }

            transform.localPosition = _originalPosition;
        }

        private void ResetState()
        {
            if (button != null)
            {
                button.interactable = true;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            if (buttonText != null)
            {
                buttonText.text = _originalText;
            }

            if (buttonImage != null)
            {
                buttonImage.color = normalColor;
            }

            transform.localPosition = _originalPosition;
        }

        /// <summary>
        /// Set button interactable state.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        /// <summary>
        /// Update button text.
        /// </summary>
        public void SetText(string text)
        {
            if (buttonText != null)
            {
                buttonText.text = text;
                _originalText = text;
            }
        }
    }
}
