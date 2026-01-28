using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Navigation;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Core
{
    /// <summary>
    /// Base class for popup dialogs.
    /// Popups can stack on top of each other and screens.
    /// Supports async lifecycle, animations, and background click handling.
    /// </summary>
    public abstract class UIPopupBase : MonoBehaviour, IUIPopup
    {
        [Header("Popup Settings")]
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected RectTransform contentTransform;
        [SerializeField] protected float transitionDuration = 0.25f;
        [SerializeField] protected bool closeOnBackgroundClick = true;
        [SerializeField] protected bool closeOnBackButton = true;
        [SerializeField] private string popupId;

        [Header("Auto-Registration")]
        [Tooltip("If true, automatically registers with NavigationService on Awake")]
        [SerializeField] private bool autoRegister = true;

        [Inject] protected INavigationService NavigationService;
        [Inject] protected IMessageBus MessageBus;
        [Inject] protected IFeedbackService FeedbackService;
        [Inject] protected IGRollLogger Logger;

        protected CompositeDisposable Subscriptions = new();

        private bool _isVisible;
        private bool _isInitialized;
        private UniTaskCompletionSource<object> _completionSource;

        public event Action OnClosed;

        #region IUIPopup Implementation

        public string PopupId => string.IsNullOrEmpty(popupId) ? GetType().Name : popupId;

        public bool IsVisible => _isVisible;

        public async UniTask ShowAsync(object parameters)
        {
            gameObject.SetActive(true);
            _isVisible = true;
            Result = null;
            _completionSource = new UniTaskCompletionSource<object>();

            // Initialize on first show
            if (!_isInitialized)
            {
                OnInitialize();
                _isInitialized = true;
            }

            await OnPopupShowAsync(parameters);
            await PlayShowTransitionAsync();

            OnPopupShowComplete();
        }

        public async UniTask HideAsync()
        {
            await OnPopupHideAsync();
            await PlayHideTransitionAsync();

            _isVisible = false;
            gameObject.SetActive(false);

            OnPopupHideComplete();
            DisposeSubscriptions();

            OnClosed?.Invoke();
            _completionSource?.TrySetResult(Result);
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // Auto-register with NavigationService if enabled
            if (autoRegister && NavigationService is NavigationService navService)
            {
                navService.RegisterPopup(this);
            }

            // Auto-get components if not assigned
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        #endregion

        #region Popup Result

        /// <summary>
        /// Result to return when popup closes.
        /// Set this before calling Close().
        /// </summary>
        protected object Result { get; set; }

        /// <summary>
        /// Wait for popup to close and get the result.
        /// </summary>
        public UniTask<object> WaitForResultAsync()
        {
            return _completionSource?.Task ?? UniTask.FromResult<object>(null);
        }

        /// <summary>
        /// Wait for popup to close and get typed result.
        /// </summary>
        public async UniTask<T> WaitForResultAsync<T>()
        {
            var result = await WaitForResultAsync();
            if (result is T typedResult)
                return typedResult;
            return default;
        }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called once when the popup is first shown.
        /// Use for one-time initialization.
        /// </summary>
        protected virtual void OnInitialize() { }

        protected virtual UniTask OnPopupShowAsync(object parameters) => UniTask.CompletedTask;
        protected virtual void OnPopupShowComplete() { }
        protected virtual UniTask OnPopupHideAsync() => UniTask.CompletedTask;
        protected virtual void OnPopupHideComplete() { }

        /// <summary>
        /// Called when back button pressed.
        /// Return true to prevent default close behavior.
        /// </summary>
        public virtual bool OnBackPressed()
        {
            if (closeOnBackButton)
            {
                Close();
                return true;
            }
            return false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called by background click handler.
        /// </summary>
        public void OnBackgroundClicked()
        {
            if (closeOnBackgroundClick)
            {
                Close();
            }
        }

        /// <summary>
        /// Close the popup without a result.
        /// </summary>
        public void Close()
        {
            if (!_isVisible) return;
            NavigationService?.HidePopupAsync(this).Forget();
        }

        /// <summary>
        /// Close the popup with a result.
        /// </summary>
        public void CloseWithResult(object result)
        {
            Result = result;
            Close();
        }

        /// <summary>
        /// Close the popup with a typed result.
        /// </summary>
        public void CloseWithResult<T>(T result)
        {
            Result = result;
            Close();
        }

        #endregion

        #region Transition Animations

        protected virtual async UniTask PlayShowTransitionAsync()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = true;
            }

            if (contentTransform != null)
            {
                contentTransform.localScale = Vector3.one * 0.8f;
            }

            var elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / transitionDuration);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = t;
                }

                if (contentTransform != null)
                {
                    // EaseOutBack
                    var backT = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);
                    var scale = Mathf.Lerp(0.8f, 1f, backT);
                    contentTransform.localScale = Vector3.one * scale;
                }

                await UniTask.Yield();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
            }

            if (contentTransform != null)
            {
                contentTransform.localScale = Vector3.one;
            }
        }

        protected virtual async UniTask PlayHideTransitionAsync()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
            }

            var hideTransition = transitionDuration * 0.75f;
            var elapsed = 0f;
            var startScale = contentTransform != null ? contentTransform.localScale : Vector3.one;

            while (elapsed < hideTransition)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / hideTransition);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f - t;
                }

                if (contentTransform != null)
                {
                    // EaseInBack
                    var backT = 2.7f * t * t * t - 1.7f * t * t;
                    var scale = Mathf.Lerp(startScale.x, 0.8f, backT);
                    contentTransform.localScale = Vector3.one * scale;
                }

                await UniTask.Yield();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (contentTransform != null)
            {
                contentTransform.localScale = Vector3.one * 0.8f;
            }
        }

        #endregion

        #region Subscription Helpers

        /// <summary>
        /// Subscribe to a message and automatically dispose when popup closes.
        /// </summary>
        protected void SubscribeToMessage<T>(Action<T> handler) where T : IMessage
        {
            if (MessageBus != null)
            {
                Subscriptions.Add(MessageBus.Subscribe(handler));
            }
        }

        /// <summary>
        /// Subscribe to a message asynchronously.
        /// </summary>
        protected void SubscribeToMessageAsync<T>(Func<T, UniTask> handler) where T : IMessage
        {
            if (MessageBus != null)
            {
                Subscriptions.Add(MessageBus.SubscribeAsync(handler));
            }
        }

        private void DisposeSubscriptions()
        {
            Subscriptions.Dispose();
            Subscriptions = new CompositeDisposable();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Log a message with popup context.
        /// </summary>
        protected void Log(string message)
        {
            Logger?.Log($"[{PopupId}] {message}");
        }

        /// <summary>
        /// Log a warning with popup context.
        /// </summary>
        protected void LogWarning(string message)
        {
            Logger?.LogWarning($"[{PopupId}] {message}");
        }

        /// <summary>
        /// Log an error with popup context.
        /// </summary>
        protected void LogError(string message)
        {
            Logger?.LogError($"[{PopupId}] {message}");
        }

        #endregion

        protected virtual void OnDestroy()
        {
            DisposeSubscriptions();
        }
    }
}
