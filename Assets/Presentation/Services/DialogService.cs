using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.UI;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Services
{
    /// <summary>
    /// Dialog service implementation.
    /// Provides various types of modal dialogs (confirm, alert, input, etc.)
    /// Also supports generic popup display via NavigationService.
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;
        private INavigationService _navigationService;

        // Dialog prefab references - will be set via registration
        private IConfirmDialog _confirmDialogPrefab;
        private IAlertDialog _alertDialogPrefab;
        private IInputDialog _inputDialogPrefab;
        private ILoadingDialog _loadingDialogPrefab;
        private IProgressDialog _progressDialogPrefab;

        // Generic popup registry
        private readonly Dictionary<Type, Func<object, UniTask<object>>> _popupFactories = new();
        private readonly Dictionary<string, Type> _popupIdToType = new();

        private Transform _dialogContainer;

        [Inject]
        public DialogService(IMessageBus messageBus, IGRollLogger logger)
        {
            _messageBus = messageBus;
            _logger = logger;
        }

        /// <summary>
        /// Set the navigation service for popup support.
        /// Called after injection is complete.
        /// </summary>
        public void SetNavigationService(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        #region Registration

        /// <summary>
        /// Set the container where dialogs will be spawned
        /// </summary>
        public void SetDialogContainer(Transform container)
        {
            _dialogContainer = container;
        }

        /// <summary>
        /// Register dialog prefabs
        /// </summary>
        public void RegisterConfirmDialog(IConfirmDialog dialog) => _confirmDialogPrefab = dialog;
        public void RegisterAlertDialog(IAlertDialog dialog) => _alertDialogPrefab = dialog;
        public void RegisterInputDialog(IInputDialog dialog) => _inputDialogPrefab = dialog;
        public void RegisterLoadingDialog(ILoadingDialog dialog) => _loadingDialogPrefab = dialog;
        public void RegisterProgressDialog(IProgressDialog dialog) => _progressDialogPrefab = dialog;

        #endregion

        #region IDialogService Implementation

        public async UniTask<bool> ShowConfirmAsync(string title, string message)
        {
            return await ShowConfirmationAsync(title, message, "Yes", "No");
        }

        public async UniTask<bool> ShowConfirmationAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
        {
            _logger.Log($"[Dialog] Showing confirm: {title}");

            if (_confirmDialogPrefab == null)
            {
                _logger.LogWarning("[Dialog] Confirm dialog not registered, returning true");
                return true;
            }

            var tcs = new UniTaskCompletionSource<bool>();

            _confirmDialogPrefab.Show(title, message, confirmText, cancelText,
                onConfirm: () => tcs.TrySetResult(true),
                onCancel: () => tcs.TrySetResult(false)
            );

            return await tcs.Task;
        }

        public async UniTask<bool> ShowRetryAsync(string message)
        {
            return await ShowConfirmationAsync("Error", message, "Retry", "Cancel");
        }

        public async UniTask<string> ShowInputAsync(string title, string placeholder, string defaultValue = "")
        {
            _logger.Log($"[Dialog] Showing input: {title}");

            if (_inputDialogPrefab == null)
            {
                _logger.LogWarning("[Dialog] Input dialog not registered, returning null");
                return null;
            }

            var tcs = new UniTaskCompletionSource<string>();

            _inputDialogPrefab.Show(title, placeholder, defaultValue,
                onSubmit: (value) => tcs.TrySetResult(value),
                onCancel: () => tcs.TrySetResult(null)
            );

            return await tcs.Task;
        }

        public async UniTask ShowAlertAsync(string title, string message)
        {
            await ShowAlertAsync(title, message, "OK");
        }

        public async UniTask ShowAlertAsync(string title, string message, string buttonText)
        {
            _logger.Log($"[Dialog] Showing alert: {title}");

            if (_alertDialogPrefab == null)
            {
                _logger.LogWarning("[Dialog] Alert dialog not registered");
                return;
            }

            var tcs = new UniTaskCompletionSource<bool>();

            _alertDialogPrefab.Show(title, message, buttonText,
                onClose: () => tcs.TrySetResult(true)
            );

            await tcs.Task;
        }

        public async UniTask<int> ShowOptionsAsync(string title, params string[] options)
        {
            _logger.Log($"[Dialog] Showing options: {title}");

            // For now, use confirm dialog for simple yes/no
            // More complex option dialogs can be implemented later
            if (options.Length == 2)
            {
                var result = await ShowConfirmationAsync(title, "", options[0], options[1]);
                return result ? 0 : 1;
            }

            _logger.LogWarning("[Dialog] Options dialog with more than 2 options not yet implemented");
            return -1;
        }

        public ILoadingDialogHandle ShowLoading(string message = "Loading...")
        {
            _logger.Log($"[Dialog] Showing loading: {message}");

            if (_loadingDialogPrefab == null)
            {
                _logger.LogWarning("[Dialog] Loading dialog not registered");
                return new DummyLoadingHandle();
            }

            _loadingDialogPrefab.Show(message);
            return new LoadingDialogHandle(_loadingDialogPrefab);
        }

        public IProgressDialogHandle ShowProgress(string title, bool cancellable = false)
        {
            _logger.Log($"[Dialog] Showing progress: {title}");

            if (_progressDialogPrefab == null)
            {
                _logger.LogWarning("[Dialog] Progress dialog not registered");
                return new DummyProgressHandle();
            }

            _progressDialogPrefab.Show(title, cancellable);
            return new ProgressDialogHandle(_progressDialogPrefab);
        }

        public async UniTask<T> ShowPopupAsync<T>(object parameters = null) where T : class
        {
            _logger.Log($"[Dialog] ShowPopupAsync<{typeof(T).Name}> called");

            // Check if we have a custom factory for this type
            if (_popupFactories.TryGetValue(typeof(T), out var factory))
            {
                var result = await factory(parameters);
                return result as T;
            }

            // Try using NavigationService if available
            if (_navigationService != null)
            {
                // Check if T implements IUIPopup
                if (typeof(IUIPopup).IsAssignableFrom(typeof(T)))
                {
                    // Use reflection to call ShowPopupAsync<T> with the correct generic type
                    var method = typeof(INavigationService).GetMethod(nameof(INavigationService.ShowPopupAsync));
                    if (method != null)
                    {
                        var genericMethod = method.MakeGenericMethod(typeof(T));
                        var task = (UniTask<T>)genericMethod.Invoke(_navigationService, new[] { parameters });
                        var popup = await task;
                        if (popup != null)
                        {
                            // Wait for popup result if it's a UIPopupBase
                            if (popup is Core.UIPopupBase popupBase)
                            {
                                var result = await popupBase.WaitForResultAsync<T>();
                                return result;
                            }
                            return popup;
                        }
                    }
                }
            }

            _logger.LogWarning($"[Dialog] Popup not found or not registered: {typeof(T).Name}");
            return default;
        }

        public async UniTask<T> ShowPopupAsync<T>(string popupId, object parameters) where T : class
        {
            _logger.Log($"[Dialog] ShowPopupAsync<{typeof(T).Name}> called with popupId: {popupId}");

            // Try to find popup by ID
            if (_popupIdToType.TryGetValue(popupId, out var popupType))
            {
                // Use reflection to call the generic version
                var method = GetType().GetMethod(nameof(ShowPopupAsync), new[] { typeof(object) });
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(popupType);
                    var task = genericMethod.Invoke(this, new[] { parameters });
                    if (task is UniTask<T> typedTask)
                    {
                        return await typedTask;
                    }
                }
            }

            _logger.LogWarning($"[Dialog] Popup not found with ID: {popupId}");
            return default;
        }

        /// <summary>
        /// Register a popup factory for custom popup handling.
        /// </summary>
        public void RegisterPopupFactory<T>(Func<object, UniTask<object>> factory, string popupId = null) where T : class
        {
            _popupFactories[typeof(T)] = factory;
            if (!string.IsNullOrEmpty(popupId))
            {
                _popupIdToType[popupId] = typeof(T);
            }
            _logger.Log($"[Dialog] Registered popup factory: {typeof(T).Name}");
        }

        public async UniTask<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText)
        {
            return await ShowConfirmationAsync(title, message, confirmText, cancelText);
        }

        #endregion

        #region Handle Implementations

        private class LoadingDialogHandle : ILoadingDialogHandle
        {
            private readonly ILoadingDialog _dialog;

            public LoadingDialogHandle(ILoadingDialog dialog)
            {
                _dialog = dialog;
            }

            public void Close() => _dialog.Hide();
            public void UpdateMessage(string message) => _dialog.UpdateMessage(message);
        }

        private class ProgressDialogHandle : IProgressDialogHandle
        {
            private readonly IProgressDialog _dialog;

            public ProgressDialogHandle(IProgressDialog dialog)
            {
                _dialog = dialog;
            }

            public bool IsCancelled => _dialog.IsCancelled;
            public void Close() => _dialog.Hide();
            public void UpdateProgress(float progress, string message = null) => _dialog.UpdateProgress(progress, message);
        }

        private class DummyLoadingHandle : ILoadingDialogHandle
        {
            public void Close() { }
            public void UpdateMessage(string message) { }
        }

        private class DummyProgressHandle : IProgressDialogHandle
        {
            public bool IsCancelled => false;
            public void Close() { }
            public void UpdateProgress(float progress, string message = null) { }
        }

        #endregion
    }

    #region Dialog Interfaces (for prefab implementations)

    /// <summary>
    /// Confirm dialog interface
    /// </summary>
    public interface IConfirmDialog
    {
        void Show(string title, string message, string confirmText, string cancelText, Action onConfirm, Action onCancel);
        void Hide();
    }

    /// <summary>
    /// Alert dialog interface
    /// </summary>
    public interface IAlertDialog
    {
        void Show(string title, string message, string buttonText, Action onClose);
        void Hide();
    }

    /// <summary>
    /// Input dialog interface
    /// </summary>
    public interface IInputDialog
    {
        void Show(string title, string placeholder, string defaultValue, Action<string> onSubmit, Action onCancel);
        void Hide();
    }

    /// <summary>
    /// Loading dialog interface
    /// </summary>
    public interface ILoadingDialog
    {
        void Show(string message);
        void Hide();
        void UpdateMessage(string message);
    }

    /// <summary>
    /// Progress dialog interface
    /// </summary>
    public interface IProgressDialog
    {
        void Show(string title, bool cancellable);
        void Hide();
        void UpdateProgress(float progress, string message = null);
        bool IsCancelled { get; }
    }

    #endregion
}
