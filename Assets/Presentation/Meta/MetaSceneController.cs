using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using GRoll.Core.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Meta
{
    /// <summary>
    /// Meta scene controller.
    /// Handles main menu, shop, inventory, settings navigation.
    /// Replaces old UIMainMenu and GameManager (Meta phase) functionality.
    /// </summary>
    public class MetaSceneController : MonoBehaviour
    {
        [Header("Main Menu")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaderboardButton;

        [Header("Top Panel")]
        [SerializeField] private TMPro.TMP_Text coinsText;
        [SerializeField] private TMPro.TMP_Text gemsText;
        [SerializeField] private TMPro.TMP_Text energyText;
        [SerializeField] private TMPro.TMP_Text usernameText;

        [Header("Panels")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject insufficientEnergyPanel;

        private IGameStateService _gameStateService;
        private ICurrencyService _currencyService;
        private IEnergyService _energyService;
        private IUserProfileService _profileService;
        private INavigationService _navigationService;
        private ISceneFlowManager _sceneFlowManager;
        private IMessageBus _messageBus;

        [Inject]
        public void Construct(
            IGameStateService gameStateService,
            ICurrencyService currencyService,
            IEnergyService energyService,
            IUserProfileService profileService,
            INavigationService navigationService,
            ISceneFlowManager sceneFlowManager,
            IMessageBus messageBus)
        {
            _gameStateService = gameStateService;
            _currencyService = currencyService;
            _energyService = energyService;
            _profileService = profileService;
            _navigationService = navigationService;
            _sceneFlowManager = sceneFlowManager;
            _messageBus = messageBus;
        }

        private void Start()
        {
            // Setup button listeners
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (shopButton != null)
                shopButton.onClick.AddListener(OnShopClicked);

            if (inventoryButton != null)
                inventoryButton.onClick.AddListener(OnInventoryClicked);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            if (leaderboardButton != null)
                leaderboardButton.onClick.AddListener(OnLeaderboardClicked);

            // Subscribe to currency/energy changes
            _messageBus.Subscribe<CurrencyChangedMessage>(OnCurrencyChanged);
            _messageBus.Subscribe<EnergyChangedMessage>(OnEnergyChanged);
            _messageBus.Subscribe<UserProfileChangedMessage>(OnProfileChanged);
            _messageBus.Subscribe<GamePhaseChangedMessage>(OnPhaseChanged);

            // Set game state to Meta
            _gameStateService.SetPhaseAsync(GamePhase.Meta).Forget();

            // Initialize UI
            RefreshUI().Forget();
        }

        private async UniTaskVoid RefreshUI()
        {
            // Update currency display (synchronous - uses cached values)
            var coins = _currencyService.GetBalance(CurrencyType.SoftCurrency);
            var gems = _currencyService.GetBalance(CurrencyType.HardCurrency);
            UpdateCurrencyDisplay(coins, gems);

            // Update energy display (synchronous - uses cached values)
            UpdateEnergyDisplay(_energyService.CurrentEnergy, _energyService.MaxEnergy);

            // Update username
            var profile = _profileService.CurrentProfile;
            if (profile != null && usernameText != null)
            {
                usernameText.text = profile.Username ?? "Player";
            }

            await UniTask.CompletedTask;
        }

        #region Button Handlers

        private async void OnPlayClicked()
        {
            Debug.Log("[MetaSceneController] Play clicked");

            // Start gameplay with default mode (Endless)
            var result = await _gameStateService.StartGameplayAsync(GameMode.Endless);

            if (result.Success)
            {
                Debug.Log($"[MetaSceneController] Session started: {result.SessionId}");
                // Transition to Gameplay scene
                await _sceneFlowManager.TransitionToAsync(SceneType.Gameplay);
            }
            else
            {
                Debug.LogWarning($"[MetaSceneController] Failed to start game: {result.FailReason}");

                // Show appropriate UI based on failure reason
                if (result.FailReason == GameStartFailReason.InsufficientEnergy)
                {
                    ShowInsufficientEnergyPanel();
                }
                else
                {
                    // Show generic error
                    // TODO: Use DialogService to show error
                }
            }
        }

        private void OnShopClicked()
        {
            Debug.Log("[MetaSceneController] Shop clicked");
            HideAllPanels();
            shopPanel?.SetActive(true);
        }

        private void OnInventoryClicked()
        {
            Debug.Log("[MetaSceneController] Inventory clicked");
            HideAllPanels();
            inventoryPanel?.SetActive(true);
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MetaSceneController] Settings clicked");
            HideAllPanels();
            settingsPanel?.SetActive(true);
        }

        private void OnLeaderboardClicked()
        {
            Debug.Log("[MetaSceneController] Leaderboard clicked");
            HideAllPanels();
            leaderboardPanel?.SetActive(true);
        }

        #endregion

        #region UI Updates

        private void HideAllPanels()
        {
            shopPanel?.SetActive(false);
            inventoryPanel?.SetActive(false);
            settingsPanel?.SetActive(false);
            leaderboardPanel?.SetActive(false);
            insufficientEnergyPanel?.SetActive(false);
        }

        private void ShowInsufficientEnergyPanel()
        {
            insufficientEnergyPanel?.SetActive(true);
        }

        private void UpdateCurrencyDisplay(int coins, int gems)
        {
            if (coinsText != null)
                coinsText.text = FormatNumber(coins);

            if (gemsText != null)
                gemsText.text = FormatNumber(gems);
        }

        private void UpdateEnergyDisplay(int current, int max)
        {
            if (energyText != null)
                energyText.text = $"{current}/{max}";
        }

        private string FormatNumber(int number)
        {
            if (number >= 1000000)
                return $"{number / 1000000f:F1}M";
            if (number >= 1000)
                return $"{number / 1000f:F1}K";
            return number.ToString();
        }

        #endregion

        #region Event Handlers

        private void OnCurrencyChanged(CurrencyChangedMessage message)
        {
            // Update the appropriate currency based on type
            if (message.Type == CurrencyType.SoftCurrency)
            {
                if (coinsText != null)
                    coinsText.text = FormatNumber(message.NewAmount);
            }
            else if (message.Type == CurrencyType.HardCurrency)
            {
                if (gemsText != null)
                    gemsText.text = FormatNumber(message.NewAmount);
            }
        }

        private void OnEnergyChanged(EnergyChangedMessage message)
        {
            UpdateEnergyDisplay(message.CurrentEnergy, message.MaxEnergy);
        }

        private void OnProfileChanged(UserProfileChangedMessage message)
        {
            if (usernameText != null && message.Profile != null)
            {
                usernameText.text = message.Profile.Username ?? "Player";
            }
        }

        private void OnPhaseChanged(GamePhaseChangedMessage message)
        {
            // If returning to Meta from Gameplay, refresh UI
            if (message.NewPhase == GamePhase.Meta)
            {
                HideAllPanels();
                RefreshUI().Forget();
            }
        }

        #endregion

        /// <summary>
        /// Called when returning from Gameplay scene
        /// </summary>
        public void OnReturnFromGameplay()
        {
            Debug.Log("[MetaSceneController] Returned from gameplay");
            RefreshUI().Forget();
        }
    }
}
