using Cysharp.Threading.Tasks;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Full-screen loading overlay with spinner and optional message.
    /// Cannot be dismissed by user - must be closed programmatically.
    /// </summary>
    public class LoadingPopup : UIPopupBase
    {
        public class LoadingParams
        {
            public string Message { get; set; } = "Loading...";
            public bool ShowSpinner { get; set; } = true;
            public bool ShowProgress { get; set; } = false;
            public float InitialProgress { get; set; } = 0f;
        }

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private GameObject spinnerContainer;
        [SerializeField] private Image spinnerImage;

        [Header("Progress")]
        [SerializeField] private GameObject progressContainer;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("Animation")]
        [SerializeField] private float spinnerRotationSpeed = 360f;

        private LoadingParams _params;
        private bool _isSpinning;
        private float _currentProgress;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            _params = parameters as LoadingParams ?? new LoadingParams();

            SetupUI();
            StartSpinnerAnimation().Forget();

            return UniTask.CompletedTask;
        }

        private void SetupUI()
        {
            if (messageText != null)
            {
                messageText.text = _params.Message;
            }

            if (spinnerContainer != null)
            {
                spinnerContainer.SetActive(_params.ShowSpinner);
            }

            if (progressContainer != null)
            {
                progressContainer.SetActive(_params.ShowProgress);
            }

            if (_params.ShowProgress)
            {
                SetProgress(_params.InitialProgress);
            }
        }

        private async UniTaskVoid StartSpinnerAnimation()
        {
            if (spinnerImage == null) return;

            _isSpinning = true;

            while (_isSpinning && this != null && spinnerImage != null)
            {
                spinnerImage.transform.Rotate(0f, 0f, -spinnerRotationSpeed * Time.unscaledDeltaTime);
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// Updates the loading message.
        /// </summary>
        public void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

        /// <summary>
        /// Updates the progress bar (0-1 range).
        /// </summary>
        public void SetProgress(float progress)
        {
            _currentProgress = Mathf.Clamp01(progress);

            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = _currentProgress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
            }
        }

        /// <summary>
        /// Smoothly animates progress to target value.
        /// </summary>
        public async UniTask AnimateProgressAsync(float targetProgress, float duration = 0.5f)
        {
            var startProgress = _currentProgress;
            targetProgress = Mathf.Clamp01(targetProgress);
            var elapsed = 0f;

            while (elapsed < duration && this != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = 1f - Mathf.Pow(1f - t, 2f);

                SetProgress(Mathf.Lerp(startProgress, targetProgress, easedT));
                await UniTask.Yield();
            }

            SetProgress(targetProgress);
        }

        /// <summary>
        /// Shows/hides the progress bar.
        /// </summary>
        public void ShowProgress(bool show)
        {
            if (progressContainer != null)
            {
                progressContainer.SetActive(show);
            }
        }

        // Prevent user from dismissing loading popup
        public override bool OnBackPressed()
        {
            return false;
        }

        protected override UniTask OnPopupHideAsync()
        {
            _isSpinning = false;
            return base.OnPopupHideAsync();
        }
    }
}
