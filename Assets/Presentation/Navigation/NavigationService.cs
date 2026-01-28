using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Core;
using VContainer;

namespace GRoll.Presentation.Navigation
{
    /// <summary>
    /// Navigation service implementation.
    /// Manages screen and popup stacks with transition support.
    /// Thread-safe for transition state management.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly Stack<IUIScreen> _screenStack = new();
        private readonly Stack<IUIPopup> _popupStack = new();

        private readonly Dictionary<Type, IUIScreen> _screenRegistry = new();
        private readonly Dictionary<Type, IUIPopup> _popupRegistry = new();
        private readonly Dictionary<string, Type> _screenIdToType = new();
        private readonly Dictionary<string, Type> _popupIdToType = new();

        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private readonly object _transitionLock = new();
        private bool _isTransitioning;

        public event Action<IUIScreen> OnScreenChanged;

        [Inject]
        public NavigationService(IMessageBus messageBus, IGRollLogger logger)
        {
            _messageBus = messageBus;
            _logger = logger;
        }

        #region INavigationService Implementation

        public IUIScreen CurrentScreen
        {
            get
            {
                lock (_transitionLock)
                {
                    return _screenStack.Count > 0 ? _screenStack.Peek() : null;
                }
            }
        }

        public bool CanGoBack
        {
            get
            {
                lock (_transitionLock)
                {
                    return _screenStack.Count > 1 || _popupStack.Count > 0;
                }
            }
        }

        public int StackDepth
        {
            get
            {
                lock (_transitionLock)
                {
                    return _screenStack.Count;
                }
            }
        }

        #endregion

        #region Registration

        /// <summary>
        /// Register a screen for navigation.
        /// </summary>
        public void RegisterScreen<T>(T screen) where T : IUIScreen
        {
            var type = typeof(T);
            _screenRegistry[type] = screen;
            _screenIdToType[screen.ScreenId] = type;

            if (screen is UIScreenBase screenBase)
            {
                screenBase.gameObject.SetActive(false);
            }
            _logger.Log($"[Navigation] Registered screen: {type.Name} (ID: {screen.ScreenId})");
        }

        /// <summary>
        /// Register a popup for navigation.
        /// </summary>
        public void RegisterPopup<T>(T popup) where T : IUIPopup
        {
            var type = typeof(T);
            _popupRegistry[type] = popup;
            _popupIdToType[popup.PopupId] = type;

            if (popup is UIPopupBase popupBase)
            {
                popupBase.gameObject.SetActive(false);
            }
            _logger.Log($"[Navigation] Registered popup: {type.Name} (ID: {popup.PopupId})");
        }

        /// <summary>
        /// Register a screen by type. Called by auto-registration system.
        /// </summary>
        public void RegisterScreen(IUIScreen screen)
        {
            var type = screen.GetType();
            _screenRegistry[type] = screen;
            _screenIdToType[screen.ScreenId] = type;

            if (screen is UIScreenBase screenBase)
            {
                screenBase.gameObject.SetActive(false);
            }
            _logger.Log($"[Navigation] Auto-registered screen: {type.Name} (ID: {screen.ScreenId})");
        }

        /// <summary>
        /// Register a popup by type. Called by auto-registration system.
        /// </summary>
        public void RegisterPopup(IUIPopup popup)
        {
            var type = popup.GetType();
            _popupRegistry[type] = popup;
            _popupIdToType[popup.PopupId] = type;

            if (popup is UIPopupBase popupBase)
            {
                popupBase.gameObject.SetActive(false);
            }
            _logger.Log($"[Navigation] Auto-registered popup: {type.Name} (ID: {popup.PopupId})");
        }

        /// <summary>
        /// Get a screen by type without navigating
        /// </summary>
        public T GetScreen<T>() where T : IUIScreen
        {
            if (_screenRegistry.TryGetValue(typeof(T), out var screen))
            {
                return (T)screen;
            }
            return default;
        }

        /// <summary>
        /// Get a popup by type without showing
        /// </summary>
        public T GetPopup<T>() where T : IUIPopup
        {
            if (_popupRegistry.TryGetValue(typeof(T), out var popup))
            {
                return (T)popup;
            }
            return default;
        }

        /// <summary>
        /// Check if screen is registered
        /// </summary>
        public bool HasScreen<T>() where T : IUIScreen => _screenRegistry.ContainsKey(typeof(T));

        /// <summary>
        /// Check if popup is registered
        /// </summary>
        public bool HasPopup<T>() where T : IUIPopup => _popupRegistry.ContainsKey(typeof(T));

        #endregion

        #region Screen Navigation

        public async UniTask PushScreenAsync<T>(object parameters = null) where T : IUIScreen
        {
            lock (_transitionLock)
            {
                if (_isTransitioning)
                {
                    _logger.LogWarning("[Navigation] Transition in progress, ignoring push");
                    return;
                }
                _isTransitioning = true;
            }

            try
            {
                if (!_screenRegistry.TryGetValue(typeof(T), out var screen))
                {
                    _logger.LogError($"[Navigation] Screen not registered: {typeof(T).Name}");
                    return;
                }

                // Hide current screen (but keep in stack)
                if (_screenStack.Count > 0)
                {
                    var currentScreen = _screenStack.Peek();
                    await currentScreen.HideAsync();
                }

                // Show new screen
                _screenStack.Push(screen);
                await screen.ShowAsync(parameters);

                OnScreenChanged?.Invoke(screen);
                _messageBus.Publish(new ScreenChangedMessage(screen.ScreenId, ScreenNavigationType.Push));

                _logger.Log($"[Navigation] Pushed screen: {typeof(T).Name}");
            }
            finally
            {
                lock (_transitionLock)
                {
                    _isTransitioning = false;
                }
            }
        }

        public async UniTask PopScreenAsync()
        {
            lock (_transitionLock)
            {
                if (_isTransitioning)
                {
                    _logger.LogWarning("[Navigation] Transition in progress, ignoring pop");
                    return;
                }

                if (_screenStack.Count <= 1)
                {
                    _logger.LogWarning("[Navigation] Cannot pop root screen");
                    return;
                }

                _isTransitioning = true;
            }

            try
            {
                // Hide and remove current screen
                var currentScreen = _screenStack.Pop();
                await currentScreen.HideAsync();

                // Show previous screen
                var previousScreen = _screenStack.Peek();
                await previousScreen.ShowAsync(null);

                OnScreenChanged?.Invoke(previousScreen);
                _messageBus.Publish(new ScreenChangedMessage(previousScreen.ScreenId, ScreenNavigationType.Pop));

                _logger.Log($"[Navigation] Popped to screen: {previousScreen.ScreenId}");
            }
            finally
            {
                lock (_transitionLock)
                {
                    _isTransitioning = false;
                }
            }
        }

        public async UniTask PopToRootAsync()
        {
            while (_screenStack.Count > 1)
            {
                await PopScreenAsync();
            }
        }

        public async UniTask GoBack()
        {
            // First check if there are popups to close
            if (_popupStack.Count > 0)
            {
                var topPopup = _popupStack.Peek();
                await HidePopupAsync(topPopup);
                return;
            }

            // Otherwise pop screen
            await PopScreenAsync();
        }

        public async UniTask NavigateTo(string screenId, object parameters = null, bool clearStack = false)
        {
            _logger.Log($"[Navigation] NavigateTo called with screenId: {screenId}, clearStack: {clearStack}");

            // Clear stack if requested
            if (clearStack)
            {
                await PopToRootAsync();
            }

            // Find screen by ID
            foreach (var kvp in _screenRegistry)
            {
                if (kvp.Value.ScreenId == screenId)
                {
                    // Use reflection to call PushScreenAsync with the correct type
                    var method = typeof(NavigationService).GetMethod(nameof(PushScreenAsync));
                    var genericMethod = method.MakeGenericMethod(kvp.Key);
                    await (UniTask)genericMethod.Invoke(this, new[] { parameters });
                    return;
                }
            }

            _logger.LogWarning($"[Navigation] Screen not found with ID: {screenId}");
        }

        public async UniTask ReplaceScreenAsync<T>(object parameters = null) where T : IUIScreen
        {
            lock (_transitionLock)
            {
                if (_isTransitioning)
                {
                    _logger.LogWarning("[Navigation] Transition in progress, ignoring replace");
                    return;
                }
                _isTransitioning = true;
            }

            try
            {
                if (!_screenRegistry.TryGetValue(typeof(T), out var screen))
                {
                    _logger.LogError($"[Navigation] Screen not registered: {typeof(T).Name}");
                    return;
                }

                // Hide and remove current screen
                if (_screenStack.Count > 0)
                {
                    var currentScreen = _screenStack.Pop();
                    await currentScreen.HideAsync();
                }

                // Show new screen
                _screenStack.Push(screen);
                await screen.ShowAsync(parameters);

                OnScreenChanged?.Invoke(screen);
                _messageBus.Publish(new ScreenChangedMessage(screen.ScreenId, ScreenNavigationType.Replace));

                _logger.Log($"[Navigation] Replaced screen: {typeof(T).Name}");
            }
            finally
            {
                lock (_transitionLock)
                {
                    _isTransitioning = false;
                }
            }
        }

        #endregion

        #region Popup Navigation

        public async UniTask<T> ShowPopupAsync<T>(object parameters = null) where T : IUIPopup
        {
            if (!_popupRegistry.TryGetValue(typeof(T), out var popup))
            {
                _logger.LogError($"[Navigation] Popup not registered: {typeof(T).Name}");
                return default;
            }

            _popupStack.Push(popup);

            _logger.Log($"[Navigation] Showing popup: {typeof(T).Name}");

            await popup.ShowAsync(parameters);

            return (T)popup;
        }

        public async UniTask HidePopupAsync<T>() where T : IUIPopup
        {
            if (!_popupRegistry.TryGetValue(typeof(T), out var popup))
                return;

            await HidePopupAsync(popup);
        }

        /// <summary>
        /// Hide a specific popup instance.
        /// Called by popup's own Close() method.
        /// </summary>
        public async UniTask HidePopupAsync(IUIPopup popup)
        {
            if (!popup.IsVisible) return;

            await popup.HideAsync();

            // Remove from stack
            var tempStack = new Stack<IUIPopup>();
            while (_popupStack.Count > 0)
            {
                var p = _popupStack.Pop();
                if (p != popup)
                {
                    tempStack.Push(p);
                }
            }
            while (tempStack.Count > 0)
            {
                _popupStack.Push(tempStack.Pop());
            }

            _logger.Log($"[Navigation] Hidden popup: {popup.PopupId}");
        }

        public async UniTask HideAllPopupsAsync()
        {
            while (_popupStack.Count > 0)
            {
                var popup = _popupStack.Pop();
                await popup.HideAsync();
            }
        }

        #endregion

        #region Back Button Handling

        /// <summary>
        /// Handle back button press.
        /// Returns true if handled, false otherwise.
        /// </summary>
        public bool HandleBackButton()
        {
            // First, try popups
            if (_popupStack.Count > 0)
            {
                var topPopup = _popupStack.Peek();
                if (topPopup is UIPopupBase popupBase && popupBase.OnBackPressed())
                {
                    return true;
                }
            }

            // Then, try current screen
            if (_screenStack.Count > 0)
            {
                var currentScreen = _screenStack.Peek();
                if (currentScreen is UIScreenBase screenBase && screenBase.OnBackPressed())
                {
                    return true;
                }
            }

            // Default: pop screen if possible
            if (_screenStack.Count > 1)
            {
                PopScreenAsync().Forget();
                return true;
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Screen navigation type
    /// </summary>
    public enum ScreenNavigationType
    {
        Push,
        Pop,
        Replace
    }

    /// <summary>
    /// Message for screen changes
    /// </summary>
    public readonly struct ScreenChangedMessage : IMessage
    {
        public string ScreenId { get; }
        public ScreenNavigationType NavigationType { get; }

        public ScreenChangedMessage(string screenId, ScreenNavigationType navigationType)
        {
            ScreenId = screenId;
            NavigationType = navigationType;
        }
    }
}
