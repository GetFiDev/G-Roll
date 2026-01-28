using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Components;
using GRoll.Presentation.Core;
using GRoll.Presentation.Popups;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Screens
{
    /// <summary>
    /// Home screen - main menu and entry point for gameplay.
    /// Displays energy, currency, player stats, and autopilot preview.
    /// </summary>
    public class HomeScreen : UIScreenBase
    {
        [Header("Play Section")]
        [SerializeField] private Button playButton;
        [SerializeField] private TextMeshProUGUI playButtonText;

        [Header("Currency Displays")]
        [SerializeField] private OptimisticCurrencyDisplay softCurrencyDisplay;
        [SerializeField] private OptimisticCurrencyDisplay hardCurrencyDisplay;

        [Header("Energy Display")]
        [SerializeField] private EnergyDisplay energyDisplay;

        [Header("Player Stats")]
        [SerializeField] private StatDisplay[] statDisplays;

        [Header("Autopilot Preview")]
        [SerializeField] private GameObject autopilotPreviewCard;
        [SerializeField] private TextMeshProUGUI autopilotEarningsText;
        [SerializeField] private Button autopilotButton;

        [Header("Bottom Navigation")]
        [SerializeField] private BottomNavigation bottomNavigation;

        [Header("User Profile")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Button settingsButton;

        [Inject] private IEnergyService _energyService;
        [Inject] private ICurrencyService _currencyService;
        [Inject] private IGameStateService _gameStateService;
        [Inject] private IUserProfileService _userProfileService;

        protected override async UniTask OnScreenEnterAsync(object parameters)
        {
            SetupButtonListeners();
            SubscribeToMessages();

            await RefreshAllDataAsync();

            if (bottomNavigation != null)
            {
                bottomNavigation.SelectTab(BottomNavigation.NavTab.Home, navigate: false);
            }
        }

        private void SetupButtonListeners()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (autopilotButton != null)
            {
                autopilotButton.onClick.RemoveAllListeners();
                autopilotButton.onClick.AddListener(OnAutopilotClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (energyDisplay != null)
            {
                energyDisplay.OnClicked += OnEnergyDisplayClicked;
            }
        }

        private void SubscribeToMessages()
        {
            SubscribeToMessage<EnergyChangedMessage>(OnEnergyChanged);
            SubscribeToMessage<CurrencyChangedMessage>(OnCurrencyChanged);
            SubscribeToMessage<UserProfileChangedMessage>(OnProfileChanged);
        }

        private async UniTask RefreshAllDataAsync()
        {
            RefreshUserProfile();
            RefreshCurrencyDisplays();
            RefreshEnergyDisplay();
            RefreshPlayerStats();
            RefreshAutopilotPreview();

            await UniTask.CompletedTask;
        }

        private void RefreshUserProfile()
        {
            if (_userProfileService?.CurrentProfile == null) return;

            var profile = _userProfileService.CurrentProfile;

            if (usernameText != null)
            {
                usernameText.text = profile.Username ?? "Guest";
            }
        }

        private void RefreshCurrencyDisplays()
        {
            softCurrencyDisplay?.Refresh();
            hardCurrencyDisplay?.Refresh();
        }

        private void RefreshEnergyDisplay()
        {
            energyDisplay?.Refresh();
        }

        private void RefreshPlayerStats()
        {
            // Stats will be populated from player profile/stats service
            // This is a placeholder for actual stat data
        }

        private void RefreshAutopilotPreview()
        {
            // Autopilot preview will be populated from autopilot service
            // Show/hide based on whether user has pending earnings
            if (autopilotPreviewCard != null)
            {
                // TODO: Check AutopilotService for pending earnings
                autopilotPreviewCard.SetActive(false);
            }
        }

        private void OnPlayClicked()
        {
            StartGameAsync().Forget();
        }

        private async UniTaskVoid StartGameAsync()
        {
            if (_energyService == null || _gameStateService == null) return;

            // Check energy
            if (!_energyService.HasEnoughEnergy(1))
            {
                await NavigationService.ShowPopupAsync<NoEnergyPopup>();
                return;
            }

            // Start gameplay
            var result = await _gameStateService.StartGameplayAsync(GameMode.Endless);

            if (!result.Success)
            {
                if (result.FailReason == GameStartFailReason.InsufficientEnergy)
                {
                    await NavigationService.ShowPopupAsync<NoEnergyPopup>();
                }
                else
                {
                    FeedbackService?.ShowErrorToast(result.ErrorMessage ?? "Failed to start game");
                }
            }
        }

        private void OnAutopilotClicked()
        {
            NavigationService.ShowPopupAsync<AutopilotPopup>().Forget();
        }

        private void OnSettingsClicked()
        {
            NavigationService.PushScreenAsync<SettingsScreen>().Forget();
        }

        private void OnEnergyDisplayClicked()
        {
            if (_energyService != null && !_energyService.HasEnoughEnergy(_energyService.MaxEnergy))
            {
                NavigationService.ShowPopupAsync<NoEnergyPopup>().Forget();
            }
        }

        private void OnEnergyChanged(EnergyChangedMessage msg)
        {
            // Energy display auto-updates via its own subscription
            // Update play button state if needed
            UpdatePlayButtonState();
        }

        private void OnCurrencyChanged(CurrencyChangedMessage msg)
        {
            // Currency displays auto-update via their own subscriptions
        }

        private void OnProfileChanged(UserProfileChangedMessage msg)
        {
            RefreshUserProfile();
        }

        private void UpdatePlayButtonState()
        {
            if (playButton != null && _energyService != null)
            {
                var hasEnergy = _energyService.HasEnoughEnergy(1);
                playButton.interactable = hasEnergy;

                if (playButtonText != null)
                {
                    playButtonText.text = hasEnergy ? "PLAY" : "NO ENERGY";
                }
            }
        }

        protected override void OnScreenEnterComplete()
        {
            UpdatePlayButtonState();
        }

        protected override void OnDestroy()
        {
            if (energyDisplay != null)
            {
                energyDisplay.OnClicked -= OnEnergyDisplayClicked;
            }

            base.OnDestroy();
        }
    }
}
