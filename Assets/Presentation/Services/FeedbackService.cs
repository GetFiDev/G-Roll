using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.UI.Components;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Services
{
    /// <summary>
    /// FeedbackService implementation.
    /// Handles toast notifications, haptic feedback, and dialogs.
    /// Platform-aware haptic implementation.
    /// </summary>
    public class FeedbackService : IFeedbackService
    {
        private readonly IGRollLogger _logger;
        private readonly IDialogService _dialogService;

        // Toast queue for sequential display
        private readonly Queue<ToastData> _toastQueue = new();
        private bool _isShowingToast;

        // Toast UI reference (set via SetToastUI)
        private ToastUI _toastUI;

        [Inject]
        public FeedbackService(IGRollLogger logger, IDialogService dialogService)
        {
            _logger = logger;
            _dialogService = dialogService;
        }

        #region Toast Messages

        public void ShowToast(string message, ToastType type = ToastType.Info)
        {
            _toastQueue.Enqueue(new ToastData(message, type));
            ProcessToastQueue().Forget();
        }

        public void ShowInfoToast(string message)
        {
            ShowToast(message, ToastType.Info);
        }

        public void ShowSuccessToast(string message)
        {
            ShowToast(message, ToastType.Success);
        }

        public void ShowErrorToast(string message)
        {
            ShowToast(message, ToastType.Error);
        }

        public void ShowWarningToast(string message)
        {
            ShowToast(message, ToastType.Warning);
        }

        private async UniTaskVoid ProcessToastQueue()
        {
            if (_isShowingToast || _toastQueue.Count == 0) return;

            _isShowingToast = true;

            while (_toastQueue.Count > 0)
            {
                var toast = _toastQueue.Dequeue();
                await ShowToastInternalAsync(toast);
            }

            _isShowingToast = false;
        }

        private async UniTask ShowToastInternalAsync(ToastData toast)
        {
            // Log the toast
            var icon = toast.Type switch
            {
                ToastType.Success => "[OK]",
                ToastType.Warning => "[!]",
                ToastType.Error => "[X]",
                _ => "[i]"
            };

            _logger.Log($"[Toast] {icon} {toast.Message}");

            // Use ToastUI if available
            if (_toastUI != null)
            {
                await _toastUI.ShowAsync(toast.Message, toast.Type);
            }
            else
            {
                // Fallback: just wait
                await UniTask.Delay(TimeSpan.FromSeconds(2f));
            }
        }

        /// <summary>
        /// Sets the ToastUI component for displaying toasts.
        /// Should be called from a MonoBehaviour that has a reference to the ToastUI prefab.
        /// </summary>
        public void SetToastUI(ToastUI toastUI)
        {
            _toastUI = toastUI;
        }

        private struct ToastData
        {
            public string Message;
            public ToastType Type;

            public ToastData(string message, ToastType type)
            {
                Message = message;
                Type = type;
            }
        }

        #endregion

        #region Haptic Feedback

        public void PlayHaptic(HapticType type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            PlayHapticIOS(type);
#elif UNITY_ANDROID && !UNITY_EDITOR
            PlayHapticAndroid(type);
#else
            // No haptic in editor, just log
            _logger.Log($"[Haptic] {type}");
#endif
        }

        public void PlaySuccessHaptic()
        {
            PlayHaptic(HapticType.Success);
        }

        public void PlayErrorHaptic()
        {
            PlayHaptic(HapticType.Error);
        }

        public void PlayWarningHaptic()
        {
            PlayHaptic(HapticType.Warning);
        }

        public void PlaySelectionHaptic()
        {
            PlayHaptic(HapticType.Selection);
        }

#if UNITY_IOS && !UNITY_EDITOR
        // iOS native haptic bridge
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerImpactHaptic(int style);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerNotificationHaptic(int type);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerSelectionHaptic();

        private void PlayHapticIOS(HapticType type)
        {
            try
            {
                switch (type)
                {
                    case HapticType.Light:
                        _TriggerImpactHaptic(0); // UIImpactFeedbackStyle.Light
                        break;
                    case HapticType.Medium:
                        _TriggerImpactHaptic(1); // UIImpactFeedbackStyle.Medium
                        break;
                    case HapticType.Heavy:
                        _TriggerImpactHaptic(2); // UIImpactFeedbackStyle.Heavy
                        break;
                    case HapticType.Selection:
                        _TriggerSelectionHaptic();
                        break;
                    case HapticType.Success:
                        _TriggerNotificationHaptic(0); // UINotificationFeedbackType.Success
                        break;
                    case HapticType.Warning:
                        _TriggerNotificationHaptic(1); // UINotificationFeedbackType.Warning
                        break;
                    case HapticType.Error:
                        _TriggerNotificationHaptic(2); // UINotificationFeedbackType.Error
                        break;
                }
            }
            catch (System.Exception ex)
            {
                // Native plugin not available, fallback to log
                _logger.LogWarning($"[Haptic] iOS native haptic failed: {ex.Message}");
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PlayHapticAndroid(HapticType type)
        {
            // Use Android Vibrator
            var duration = type switch
            {
                HapticType.Light => 10,
                HapticType.Medium => 30,
                HapticType.Heavy => 50,
                HapticType.Selection => 5,
                HapticType.Success => 20,
                HapticType.Warning => 40,
                HapticType.Error => 60,
                _ => 20
            };

            Handheld.Vibrate();
            // For more control, use Android vibrator API via JNI
            // AndroidJavaClass vibrator = new AndroidJavaClass("android.os.Vibrator");
        }
#endif

        #endregion

        #region Dialogs

        public async UniTask<bool> ShowConfirmationDialogAsync(
            string title,
            string message,
            string confirmText = "OK",
            string cancelText = "Cancel")
        {
            if (_dialogService != null)
            {
                return await _dialogService.ShowConfirmationAsync(title, message, confirmText, cancelText);
            }

            // Fallback: log and return true
            _logger.LogWarning($"[Dialog] No DialogService - auto-confirming: {title}");
            return true;
        }

        public async UniTask ShowRetryDialogAsync(string message, Func<UniTask> onRetry)
        {
            if (_dialogService != null)
            {
                var shouldRetry = await _dialogService.ShowRetryAsync(message);
                if (shouldRetry && onRetry != null)
                {
                    await onRetry();
                }
                return;
            }

            // Fallback: log and auto-retry
            _logger.LogWarning($"[Dialog] No DialogService - auto-retrying: {message}");
            if (onRetry != null)
            {
                await onRetry();
            }
        }

        public async UniTask ShowAlertAsync(string title, string message, string buttonText = "OK")
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowAlertAsync(title, message, buttonText);
                return;
            }

            // Fallback: log
            _logger.Log($"[Alert] {title}: {message}");
        }

        #endregion
    }
}
