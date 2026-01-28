using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Components;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using LeaderboardEntryData = GRoll.Core.Interfaces.Services.LeaderboardEntry;
using LeaderboardEntryComponent = GRoll.Presentation.Components.LeaderboardEntry;

namespace GRoll.Presentation.Screens
{
    /// <summary>
    /// Leaderboard screen displaying rankings with tab support (All-Time/Season).
    /// Features sticky user entry and pull-to-refresh.
    /// </summary>
    public class LeaderboardScreen : UIScreenBase
    {
        public enum LeaderboardTab
        {
            AllTime,
            Season
        }

        [Serializable]
        public class TabButton
        {
            public Button button;
            public TextMeshProUGUI labelText;
            public LeaderboardTab tab;
            public Sprite activeSprite;
            public Sprite inactiveSprite;
            public Image backgroundImage;
        }

        [Header("Tabs")]
        [SerializeField] private TabButton[] tabButtons;

        [Header("User Header")]
        [SerializeField] private LeaderboardEntryComponent userHeaderEntry;

        [Header("Leaderboard List")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform listContainer;
        [SerializeField] private LeaderboardEntryComponent entryPrefab;

        [Header("Sticky Entry")]
        [SerializeField] private LeaderboardEntryComponent stickyEntry;
        [SerializeField] private RectTransform stickyTopAnchor;
        [SerializeField] private RectTransform stickyBottomAnchor;

        [Header("Season Info")]
        [SerializeField] private TextMeshProUGUI seasonNameText;
        [SerializeField] private TextMeshProUGUI seasonDescriptionText;
        [SerializeField] private GameObject seasonClosedPanel;

        [Header("Loading")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TextMeshProUGUI emptyStateText;

        [Header("Bottom Navigation")]
        [SerializeField] private BottomNavigation bottomNavigation;

        [Header("Settings")]
        [SerializeField] private int topEntriesCount = 100;
        [SerializeField] private float stickyCheckInterval = 0.1f;

        [Inject] private ILeaderboardService _leaderboardService;
        [Inject] private IAuthService _authService;

        private LeaderboardTab _currentTab = LeaderboardTab.AllTime;
        private List<LeaderboardEntryComponent> _instantiatedEntries = new();
        private LeaderboardEntryData _userEntry;
        private IReadOnlyList<LeaderboardEntryData> _currentEntries;
        private float _lastStickyCheck;
        private bool _isSeasonActive = true;
        private IDisposable _leaderboardSubscription;

        protected override async UniTask OnScreenEnterAsync(object parameters)
        {
            SetupTabListeners();

            if (bottomNavigation != null)
            {
                bottomNavigation.SelectTab(BottomNavigation.NavTab.Leaderboard, navigate: false);
            }

            if (_leaderboardService != null)
            {
                _leaderboardService.OnLeaderboardUpdated += OnLeaderboardUpdated;
            }

            SelectTab(LeaderboardTab.AllTime);
            await RefreshLeaderboardAsync();
        }

        private void SetupTabListeners()
        {
            foreach (var tabButton in tabButtons)
            {
                if (tabButton.button != null)
                {
                    var tab = tabButton.tab;
                    tabButton.button.onClick.RemoveAllListeners();
                    tabButton.button.onClick.AddListener(() => OnTabClicked(tab));
                }
            }
        }

        private void Update()
        {
            if (Time.time - _lastStickyCheck >= stickyCheckInterval)
            {
                _lastStickyCheck = Time.time;
                UpdateStickyVisibility();
            }
        }

        private void OnTabClicked(LeaderboardTab tab)
        {
            if (tab == _currentTab) return;

            SelectTab(tab);
            RefreshLeaderboardAsync().Forget();
        }

        private void SelectTab(LeaderboardTab tab)
        {
            _currentTab = tab;

            foreach (var tabButton in tabButtons)
            {
                var isActive = tabButton.tab == tab;
                UpdateTabVisual(tabButton, isActive);

                if (tabButton.tab == LeaderboardTab.Season)
                {
                    tabButton.button.interactable = _isSeasonActive;
                }
            }

            UpdateSeasonInfo();
        }

        private void UpdateTabVisual(TabButton tabButton, bool isActive)
        {
            if (tabButton.backgroundImage != null)
            {
                tabButton.backgroundImage.sprite = isActive ? tabButton.activeSprite : tabButton.inactiveSprite;
            }

            if (tabButton.labelText != null)
            {
                tabButton.labelText.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }

        private void UpdateSeasonInfo()
        {
            if (seasonClosedPanel != null)
            {
                seasonClosedPanel.SetActive(_currentTab == LeaderboardTab.Season && !_isSeasonActive);
            }

            if (seasonDescriptionText != null && _currentTab == LeaderboardTab.Season && !_isSeasonActive)
            {
                seasonDescriptionText.text = "Season Closed";
            }
        }

        private async UniTask RefreshLeaderboardAsync()
        {
            if (_leaderboardService == null) return;

            ShowLoading(true);
            ClearEntries();

            try
            {
                var userId = _authService?.CurrentUserId;

                _currentEntries = await _leaderboardService.GetTopEntriesAsync(topEntriesCount);

                if (!string.IsNullOrEmpty(userId))
                {
                    _userEntry = await _leaderboardService.GetUserEntryAsync(userId);
                    UpdateUserHeader();
                }

                PopulateEntries(_currentEntries);
                UpdateStickyEntry();

                if (emptyStateText != null)
                {
                    emptyStateText.gameObject.SetActive(_currentEntries == null || _currentEntries.Count == 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardScreen] Failed to load leaderboard: {ex.Message}");
                FeedbackService?.ShowErrorToast("Failed to load leaderboard");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void UpdateUserHeader()
        {
            if (userHeaderEntry == null || _userEntry == null) return;

            userHeaderEntry.SetData(
                _userEntry.Rank,
                _userEntry.DisplayName,
                _userEntry.Score,
                false,
                _userEntry.AvatarUrl
            );
            userHeaderEntry.SetCurrentUser(true);
            userHeaderEntry.SetLockFrame(true);
        }

        private void PopulateEntries(IReadOnlyList<LeaderboardEntryData> entries)
        {
            if (entries == null || entryPrefab == null || listContainer == null) return;

            foreach (var entryData in entries)
            {
                var entry = Instantiate(entryPrefab, listContainer);
                entry.SetData(
                    entryData.Rank,
                    entryData.DisplayName,
                    entryData.Score,
                    false,
                    entryData.AvatarUrl
                );
                entry.SetCurrentUser(entryData.IsCurrentUser);

                _instantiatedEntries.Add(entry);
            }
        }

        private void UpdateStickyEntry()
        {
            if (stickyEntry == null || _userEntry == null) return;

            stickyEntry.SetData(
                _userEntry.Rank,
                _userEntry.DisplayName,
                _userEntry.Score,
                false,
                _userEntry.AvatarUrl
            );
            stickyEntry.SetCurrentUser(true);

            stickyEntry.gameObject.SetActive(false);
        }

        private void UpdateStickyVisibility()
        {
            if (stickyEntry == null || _userEntry == null || scrollRect == null) return;

            var userRank = _userEntry.Rank;

            if (userRank <= 0 || userRank > topEntriesCount)
            {
                PositionStickyEntry(true);
                return;
            }

            var userEntryIndex = userRank - 1;
            if (userEntryIndex < 0 || userEntryIndex >= _instantiatedEntries.Count)
            {
                stickyEntry.gameObject.SetActive(false);
                return;
            }

            var userEntryTransform = _instantiatedEntries[userEntryIndex].transform as RectTransform;
            if (userEntryTransform == null)
            {
                stickyEntry.gameObject.SetActive(false);
                return;
            }

            var viewportRect = scrollRect.viewport;
            var viewportCorners = new Vector3[4];
            viewportRect.GetWorldCorners(viewportCorners);

            var entryCorners = new Vector3[4];
            userEntryTransform.GetWorldCorners(entryCorners);

            var entryTop = entryCorners[1].y;
            var entryBottom = entryCorners[0].y;
            var viewportTop = viewportCorners[1].y;
            var viewportBottom = viewportCorners[0].y;

            if (entryTop >= viewportBottom && entryBottom <= viewportTop)
            {
                stickyEntry.gameObject.SetActive(false);
            }
            else if (entryBottom > viewportTop)
            {
                PositionStickyEntry(false);
            }
            else
            {
                PositionStickyEntry(true);
            }
        }

        private void PositionStickyEntry(bool atBottom)
        {
            if (stickyEntry == null) return;

            stickyEntry.gameObject.SetActive(true);

            var stickyTransform = stickyEntry.transform as RectTransform;
            if (stickyTransform == null) return;

            if (atBottom && stickyBottomAnchor != null)
            {
                stickyTransform.position = stickyBottomAnchor.position;
            }
            else if (!atBottom && stickyTopAnchor != null)
            {
                stickyTransform.position = stickyTopAnchor.position;
            }
        }

        private void ClearEntries()
        {
            foreach (var entry in _instantiatedEntries)
            {
                if (entry != null)
                {
                    Destroy(entry.gameObject);
                }
            }
            _instantiatedEntries.Clear();
        }

        private void ShowLoading(bool show)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(show);
            }
        }

        private void OnLeaderboardUpdated()
        {
            RefreshLeaderboardAsync().Forget();
        }

        protected override UniTask OnScreenExitAsync()
        {
            if (_leaderboardService != null)
            {
                _leaderboardService.OnLeaderboardUpdated -= OnLeaderboardUpdated;
            }

            ClearEntries();

            return base.OnScreenExitAsync();
        }

        public LeaderboardTab CurrentTab => _currentTab;
    }
}
