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
    /// Base class for all full-screen UI screens.
    /// Screens are mutually exclusive - only one is visible at a time.
    /// Supports async lifecycle, transitions, and back button handling.
    /// </summary>
    public abstract class UIScreenBase : MonoBehaviour, IUIScreen
    {
        [Header("Screen Settings")]
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float transitionDuration = 0.3f;
        [SerializeField] private string screenId;

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

        #region IUIScreen Implementation

        public string ScreenId => string.IsNullOrEmpty(screenId) ? GetType().Name : screenId;

        public bool IsVisible => _isVisible;

        public async UniTask ShowAsync(object parameters)
        {
            gameObject.SetActive(true);
            _isVisible = true;

            // Initialize on first show
            if (!_isInitialized)
            {
                OnInitialize();
                _isInitialized = true;
            }

            await OnScreenEnterAsync(parameters);
            await PlayEnterTransitionAsync();

            OnScreenEnterComplete();
        }

        public async UniTask HideAsync()
        {
            await OnScreenExitAsync();
            await PlayExitTransitionAsync();

            _isVisible = false;
            gameObject.SetActive(false);

            OnScreenExitComplete();
            DisposeSubscriptions();
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // Auto-register with NavigationService if enabled
            if (autoRegister && NavigationService is NavigationService navService)
            {
                navService.RegisterScreen(this);
            }

            // Auto-get CanvasGroup if not assigned
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        #endregion

        #region Lifecycle Methods (Override in subclasses)

        /// <summary>
        /// Called once when the screen is first shown.
        /// Use for one-time initialization.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// Called when screen is about to be shown.
        /// Use for initialization, data loading, subscriptions.
        /// </summary>
        protected virtual UniTask OnScreenEnterAsync(object parameters) => UniTask.CompletedTask;

        /// <summary>
        /// Called after screen transition completes.
        /// Use for animations, focus handling.
        /// </summary>
        protected virtual void OnScreenEnterComplete() { }

        /// <summary>
        /// Called when screen is about to be hidden.
        /// Use for cleanup, saving state.
        /// </summary>
        protected virtual UniTask OnScreenExitAsync() => UniTask.CompletedTask;

        /// <summary>
        /// Called after screen is fully hidden.
        /// Use for unsubscriptions, releasing resources.
        /// </summary>
        protected virtual void OnScreenExitComplete() { }

        /// <summary>
        /// Called when back button is pressed.
        /// Return true if handled, false to let navigation service handle it.
        /// </summary>
        public virtual bool OnBackPressed() => false;

        #endregion

        #region Transition Animations

        protected virtual async UniTask PlayEnterTransitionAsync()
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Simple fade in using time-based lerp
            var elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / transitionDuration);
                // EaseOutQuad
                var easedT = 1f - (1f - t) * (1f - t);
                canvasGroup.alpha = easedT;
                await UniTask.Yield();
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        protected virtual async UniTask PlayExitTransitionAsync()
        {
            if (canvasGroup == null) return;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Simple fade out using time-based lerp
            var elapsed = 0f;
            var startAlpha = canvasGroup.alpha;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / transitionDuration);
                // EaseInQuad
                var easedT = t * t;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, easedT);
                await UniTask.Yield();
            }

            canvasGroup.alpha = 0f;
        }

        #endregion

        #region Subscription Helpers

        /// <summary>
        /// Subscribe to a message and automatically dispose when screen exits.
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

        /// <summary>
        /// Dispose all subscriptions and create a new disposable.
        /// </summary>
        private void DisposeSubscriptions()
        {
            Subscriptions.Dispose();
            Subscriptions = new CompositeDisposable();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Show a popup and wait for result.
        /// </summary>
        protected async UniTask<T> ShowPopupAsync<T>() where T : UIPopupBase
        {
            if (NavigationService == null) return null;
            return await NavigationService.ShowPopupAsync<T>();
        }

        /// <summary>
        /// Navigate to another screen.
        /// </summary>
        protected async UniTask NavigateToAsync<T>(object parameters = null) where T : IUIScreen
        {
            if (NavigationService == null) return;
            await NavigationService.PushScreenAsync<T>(parameters);
        }

        /// <summary>
        /// Go back to previous screen.
        /// </summary>
        protected async UniTask GoBackAsync()
        {
            if (NavigationService == null) return;
            await NavigationService.GoBack();
        }

        /// <summary>
        /// Log a message with screen context.
        /// </summary>
        protected void Log(string message)
        {
            Logger?.Log($"[{ScreenId}] {message}");
        }

        /// <summary>
        /// Log a warning with screen context.
        /// </summary>
        protected void LogWarning(string message)
        {
            Logger?.LogWarning($"[{ScreenId}] {message}");
        }

        /// <summary>
        /// Log an error with screen context.
        /// </summary>
        protected void LogError(string message)
        {
            Logger?.LogError($"[{ScreenId}] {message}");
        }

        #endregion

        protected virtual void OnDestroy()
        {
            DisposeSubscriptions();
        }
    }
}
