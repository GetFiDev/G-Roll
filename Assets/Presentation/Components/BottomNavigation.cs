using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Bottom navigation bar component for main screen navigation.
    /// Handles tab selection, visual states, and notification badges.
    /// </summary>
    public class BottomNavigation : MonoBehaviour
    {
        [Serializable]
        public class NavButton
        {
            public Button button;
            public Image iconImage;
            public TextMeshProUGUI labelText;
            public GameObject badgeObject;
            public TextMeshProUGUI badgeCountText;
            public NavTab tab;

            [Header("Sprites")]
            public Sprite activeIcon;
            public Sprite inactiveIcon;
        }

        public enum NavTab
        {
            None = -1,
            Home,
            Shop,
            Leaderboard,
            Progress,
            Settings
        }

        [Header("Navigation Buttons")]
        [SerializeField] private NavButton[] navButtons;

        [Header("Colors")]
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color activeBackgroundColor = new Color(1f, 1f, 1f, 0.1f);

        [Header("Animation")]
        [SerializeField] private float transitionDuration = 0.15f;

        [Inject] private INavigationService _navigationService;
        [Inject] private IMessageBus _messageBus;

        private NavTab _currentTab = NavTab.Home;
        private IDisposable _screenChangedSubscription;
        private bool _isTransitioning;

        private void Awake()
        {
            SetupButtonListeners();
        }

        private void Start()
        {
            if (_messageBus != null)
            {
                _screenChangedSubscription = _messageBus.Subscribe<ScreenChangedMessage>(OnScreenChanged);
            }

            UpdateVisualState(_currentTab);
        }

        private void OnDestroy()
        {
            _screenChangedSubscription?.Dispose();
        }

        private void SetupButtonListeners()
        {
            foreach (var navButton in navButtons)
            {
                if (navButton.button != null)
                {
                    var tab = navButton.tab;
                    navButton.button.onClick.AddListener(() => OnTabClicked(tab));
                }
            }
        }

        private void OnTabClicked(NavTab tab)
        {
            if (_isTransitioning || tab == _currentTab) return;

            NavigateToTab(tab).Forget();
        }

        private async UniTaskVoid NavigateToTab(NavTab tab)
        {
            if (_navigationService == null) return;

            _isTransitioning = true;

            try
            {
                switch (tab)
                {
                    case NavTab.Home:
                        await _navigationService.ReplaceScreenAsync<IUIScreen>();
                        break;
                    case NavTab.Shop:
                        await _navigationService.ReplaceScreenAsync<IUIScreen>();
                        break;
                    case NavTab.Leaderboard:
                        await _navigationService.ReplaceScreenAsync<IUIScreen>();
                        break;
                    case NavTab.Progress:
                        await _navigationService.ReplaceScreenAsync<IUIScreen>();
                        break;
                    case NavTab.Settings:
                        await _navigationService.ReplaceScreenAsync<IUIScreen>();
                        break;
                }

                _currentTab = tab;
                UpdateVisualState(tab);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void OnScreenChanged(ScreenChangedMessage msg)
        {
            var tab = GetTabFromScreenId(msg.ScreenId);
            if (tab.HasValue && tab.Value != _currentTab)
            {
                _currentTab = tab.Value;
                UpdateVisualState(_currentTab);
            }
        }

        private NavTab? GetTabFromScreenId(string screenId)
        {
            return screenId switch
            {
                "HomeScreen" => NavTab.Home,
                "ShopScreen" => NavTab.Shop,
                "LeaderboardScreen" => NavTab.Leaderboard,
                "ProgressScreen" => NavTab.Progress,
                "SettingsScreen" => NavTab.Settings,
                _ => null
            };
        }

        private void UpdateVisualState(NavTab activeTab)
        {
            foreach (var navButton in navButtons)
            {
                var isActive = navButton.tab == activeTab;
                UpdateButtonState(navButton, isActive);
            }
        }

        private void UpdateButtonState(NavButton navButton, bool isActive)
        {
            if (navButton.iconImage != null)
            {
                navButton.iconImage.sprite = isActive ? navButton.activeIcon : navButton.inactiveIcon;
                navButton.iconImage.color = isActive ? activeColor : inactiveColor;
            }

            if (navButton.labelText != null)
            {
                navButton.labelText.color = isActive ? activeColor : inactiveColor;
            }
        }

        public void SetBadge(NavTab tab, int count)
        {
            var navButton = GetNavButton(tab);
            if (navButton == null) return;

            if (navButton.badgeObject != null)
            {
                navButton.badgeObject.SetActive(count > 0);
            }

            if (navButton.badgeCountText != null)
            {
                navButton.badgeCountText.text = count > 99 ? "99+" : count.ToString();
            }
        }

        public void ClearBadge(NavTab tab)
        {
            SetBadge(tab, 0);
        }

        public void ClearAllBadges()
        {
            foreach (var tab in Enum.GetValues(typeof(NavTab)))
            {
                ClearBadge((NavTab)tab);
            }
        }

        public void SetTabEnabled(NavTab tab, bool enabled)
        {
            var navButton = GetNavButton(tab);
            if (navButton?.button != null)
            {
                navButton.button.interactable = enabled;
            }
        }

        public void SelectTab(NavTab tab, bool navigate = true)
        {
            if (navigate)
            {
                OnTabClicked(tab);
            }
            else
            {
                _currentTab = tab;
                UpdateVisualState(tab);
            }
        }

        private NavButton GetNavButton(NavTab tab)
        {
            foreach (var navButton in navButtons)
            {
                if (navButton.tab == tab)
                    return navButton;
            }
            return null;
        }

        public NavTab CurrentTab => _currentTab;
    }
}
