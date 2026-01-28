using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Components;
using GRoll.Presentation.Core;
using GRoll.Presentation.Popups;
using TMPro;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Screens
{
    /// <summary>
    /// Progress screen combining Tasks and Achievements in a single view.
    /// Shows daily/weekly tasks at top and achievements grid below.
    /// </summary>
    public class ProgressScreen : UIScreenBase
    {
        [Header("Tasks Section")]
        [SerializeField] private Transform taskListContainer;
        [SerializeField] private TaskCard taskCardPrefab;
        [SerializeField] private TextMeshProUGUI tasksHeaderText;
        [SerializeField] private GameObject noTasksMessage;

        [Header("Achievements Section")]
        [SerializeField] private Transform achievementGridContainer;
        [SerializeField] private AchievementCard achievementCardPrefab;
        [SerializeField] private TextMeshProUGUI achievementsHeaderText;
        [SerializeField] private GameObject noAchievementsMessage;

        [Header("Streak Display")]
        [SerializeField] private TextMeshProUGUI streakCountText;
        [SerializeField] private GameObject streakContainer;

        [Header("Loading")]
        [SerializeField] private GameObject loadingPanel;

        [Header("Bottom Navigation")]
        [SerializeField] private BottomNavigation bottomNavigation;

        [Inject] private ITaskService _taskService;
        [Inject] private IAchievementService _achievementService;
        [Inject] private ICurrencyService _currencyService;

        private List<TaskCard> _instantiatedTaskCards = new();
        private List<AchievementCard> _instantiatedAchievementCards = new();

        protected override async UniTask OnScreenEnterAsync(object parameters)
        {
            SubscribeToMessages();

            if (bottomNavigation != null)
            {
                bottomNavigation.SelectTab(BottomNavigation.NavTab.Progress, navigate: false);
            }

            await RefreshAllAsync();
        }

        private void SubscribeToMessages()
        {
            SubscribeToMessage<TaskProgressMessage>(OnTaskProgressChanged);
            SubscribeToMessage<AchievementChangedMessage>(OnAchievementChanged);
            SubscribeToMessage<CurrencyChangedMessage>(OnCurrencyChanged);
        }

        private async UniTask RefreshAllAsync()
        {
            ShowLoading(true);

            try
            {
                await UniTask.WhenAll(
                    RefreshTasksAsync(),
                    RefreshAchievementsAsync()
                );

                UpdateStreakDisplay();
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async UniTask RefreshTasksAsync()
        {
            ClearTaskCards();

            if (_taskService == null)
            {
                ShowNoTasks(true);
                return;
            }

            var activeTasks = _taskService.GetActiveTasks();

            if (activeTasks == null || activeTasks.Count == 0)
            {
                ShowNoTasks(true);
                return;
            }

            ShowNoTasks(false);

            foreach (var task in activeTasks)
            {
                CreateTaskCard(task);
            }

            if (tasksHeaderText != null)
            {
                var claimableCount = 0;
                foreach (var task in activeTasks)
                {
                    if (task.IsCompleted && !task.IsClaimed)
                        claimableCount++;
                }

                tasksHeaderText.text = claimableCount > 0
                    ? $"Daily Tasks ({claimableCount} claimable)"
                    : "Daily Tasks";
            }

            await UniTask.Yield();
        }

        private void CreateTaskCard(GameTask task)
        {
            if (taskCardPrefab == null || taskListContainer == null) return;

            var card = Instantiate(taskCardPrefab, taskListContainer);
            card.SetData(task);
            card.OnClaimed += OnTaskClaimed;
            card.OnGoClicked += OnTaskGoClicked;

            _instantiatedTaskCards.Add(card);
        }

        private async UniTask RefreshAchievementsAsync()
        {
            ClearAchievementCards();

            if (_achievementService == null)
            {
                ShowNoAchievements(true);
                return;
            }

            var achievements = _achievementService.GetAllAchievements();

            if (achievements == null || achievements.Count == 0)
            {
                ShowNoAchievements(true);
                return;
            }

            ShowNoAchievements(false);

            var claimableCount = 0;

            foreach (var achievement in achievements)
            {
                CreateAchievementCard(achievement);

                if (achievement.IsUnlocked && !achievement.IsClaimed)
                    claimableCount++;
            }

            if (achievementsHeaderText != null)
            {
                achievementsHeaderText.text = claimableCount > 0
                    ? $"Achievements ({claimableCount} claimable)"
                    : "Achievements";
            }

            await UniTask.Yield();
        }

        private void CreateAchievementCard(Achievement achievement)
        {
            if (achievementCardPrefab == null || achievementGridContainer == null) return;

            var card = Instantiate(achievementCardPrefab, achievementGridContainer);
            var state = AchievementCard.DetermineState(achievement);

            card.SetData(achievement, state);
            card.OnClicked += OnAchievementCardClicked;

            _instantiatedAchievementCards.Add(card);
        }

        private void UpdateStreakDisplay()
        {
            // TODO: Get streak count from service
            var streakCount = 0;

            if (streakContainer != null)
            {
                streakContainer.SetActive(streakCount > 0);
            }

            if (streakCountText != null)
            {
                streakCountText.text = $"{streakCount} day streak!";
            }
        }

        private void OnTaskClaimed(TaskCard card)
        {
            RefreshTasksAsync().Forget();
        }

        private void OnTaskGoClicked(TaskCard card, string taskId)
        {
            // Navigate to relevant screen based on task type
            // For example, open URL, navigate to shop, etc.
            FeedbackService?.ShowInfoToast("Task action triggered");
        }

        private void OnAchievementCardClicked(AchievementCard card, Achievement achievement)
        {
            ShowAchievementDetailAsync(achievement).Forget();
        }

        private async UniTaskVoid ShowAchievementDetailAsync(Achievement achievement)
        {
            var popup = await NavigationService.ShowPopupAsync<AchievementDetailPopup>(achievement);

            var result = await popup.WaitForResultAsync<bool>();

            if (result)
            {
                await RefreshAchievementsAsync();
            }
        }

        private void OnTaskProgressChanged(TaskProgressMessage msg)
        {
            foreach (var card in _instantiatedTaskCards)
            {
                if (card.TaskId == msg.TaskId)
                {
                    card.UpdateProgress(msg.CurrentProgress, msg.TargetProgress);
                    break;
                }
            }
        }

        private void OnAchievementChanged(AchievementChangedMessage msg)
        {
            foreach (var card in _instantiatedAchievementCards)
            {
                if (card.AchievementId == msg.AchievementId)
                {
                    var achievement = _achievementService?.GetAchievement(msg.AchievementId);
                    if (achievement != null)
                    {
                        var state = AchievementCard.DetermineState(achievement);
                        card.UpdateProgress(msg.CurrentProgress, msg.TargetProgress);
                        card.UpdateVisualState(state);
                    }
                    break;
                }
            }
        }

        private void OnCurrencyChanged(CurrencyChangedMessage msg)
        {
            // Currency display updates automatically
        }

        private void ClearTaskCards()
        {
            foreach (var card in _instantiatedTaskCards)
            {
                if (card != null)
                {
                    card.OnClaimed -= OnTaskClaimed;
                    card.OnGoClicked -= OnTaskGoClicked;
                    Destroy(card.gameObject);
                }
            }
            _instantiatedTaskCards.Clear();
        }

        private void ClearAchievementCards()
        {
            foreach (var card in _instantiatedAchievementCards)
            {
                if (card != null)
                {
                    card.OnClicked -= OnAchievementCardClicked;
                    Destroy(card.gameObject);
                }
            }
            _instantiatedAchievementCards.Clear();
        }

        private void ShowNoTasks(bool show)
        {
            if (noTasksMessage != null)
            {
                noTasksMessage.SetActive(show);
            }
        }

        private void ShowNoAchievements(bool show)
        {
            if (noAchievementsMessage != null)
            {
                noAchievementsMessage.SetActive(show);
            }
        }

        private void ShowLoading(bool show)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(show);
            }
        }

        protected override UniTask OnScreenExitAsync()
        {
            ClearTaskCards();
            ClearAchievementCards();

            return base.OnScreenExitAsync();
        }
    }
}
