using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.SceneManagement;
using GRoll.Gameplay.Session;
using GRoll.Gameplay.Scoring;
using GRoll.Gameplay.Spawning;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Gameplay.Controllers
{
    /// <summary>
    /// Gameplay scene controller.
    /// Orchestrates gameplay flow: start, play, end, results.
    /// Replaces old GameplayManager functionality.
    /// </summary>
    public class GameplaySceneController : MonoBehaviour
    {
        [Header("Gameplay UI")]
        [SerializeField] private TMPro.TMP_Text scoreText;
        [SerializeField] private TMPro.TMP_Text coinText;
        [SerializeField] private TMPro.TMP_Text comboText;
        [SerializeField] private GameObject pauseButton;

        [Header("Loading")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMPro.TMP_Text loadingText;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMPro.TMP_Text finalScoreText;
        [SerializeField] private TMPro.TMP_Text finalCoinsText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button reviveButton;

        [Header("Pause Menu")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;

        // Services
        private IGameStateService _gameStateService;
        private IGameplaySessionController _sessionController;
        private IScoreManager _scoreManager;
        private IPlayerSpawnController _playerSpawnController;
        private ISceneFlowManager _sceneFlowManager;
        private IMessageBus _messageBus;
        private IAdService _adService;

        // State
        private bool _isPlaying;
        private bool _isPaused;
        private int _currentScore;
        private int _currentCoins;
        private int _currentCombo;
        private int _revivesUsed;

        [Inject]
        public void Construct(
            IGameStateService gameStateService,
            IGameplaySessionController sessionController,
            IScoreManager scoreManager,
            IPlayerSpawnController playerSpawnController,
            ISceneFlowManager sceneFlowManager,
            IMessageBus messageBus,
            IAdService adService)
        {
            _gameStateService = gameStateService;
            _sessionController = sessionController;
            _scoreManager = scoreManager;
            _playerSpawnController = playerSpawnController;
            _sceneFlowManager = sceneFlowManager;
            _messageBus = messageBus;
            _adService = adService;
        }

        private void Start()
        {
            // Setup button listeners
            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryClicked);

            if (menuButton != null)
                menuButton.onClick.AddListener(OnMenuClicked);

            if (reviveButton != null)
                reviveButton.onClick.AddListener(OnReviveClicked);

            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);

            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            // Subscribe to events
            _scoreManager.OnScoreChanged += OnScoreChanged;
            _scoreManager.OnComboChanged += OnComboChanged;

            _messageBus.Subscribe<GamePhaseChangedMessage>(OnPhaseChanged);

            // Start gameplay
            StartGameplay().Forget();
        }

        private void OnDestroy()
        {
            if (_scoreManager != null)
            {
                _scoreManager.OnScoreChanged -= OnScoreChanged;
                _scoreManager.OnComboChanged -= OnComboChanged;
            }
        }

        private void Update()
        {
            // Handle pause input
            if (_isPlaying && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isPaused)
                    ResumeGame();
                else
                    PauseGame();
            }
        }

        #region Gameplay Flow

        private async UniTaskVoid StartGameplay()
        {
            ShowLoading("Loading...");

            // Initialize gameplay systems
            _scoreManager.Reset();
            _currentScore = 0;
            _currentCoins = 0;
            _currentCombo = 0;
            _revivesUsed = 0;

            // Spawn player
            await _playerSpawnController.SpawnPlayerAsync();

            // Hide loading, show gameplay UI
            HideLoading();
            ShowGameplayUI();

            // Start playing
            _isPlaying = true;
            _isPaused = false;

            Debug.Log("[GameplaySceneController] Gameplay started");
        }

        private void PauseGame()
        {
            if (!_isPlaying) return;

            _isPaused = true;
            Time.timeScale = 0f;
            pausePanel?.SetActive(true);

            Debug.Log("[GameplaySceneController] Game paused");
        }

        private void ResumeGame()
        {
            if (!_isPaused) return;

            _isPaused = false;
            Time.timeScale = 1f;
            pausePanel?.SetActive(false);

            Debug.Log("[GameplaySceneController] Game resumed");
        }

        /// <summary>
        /// Called when player dies or game ends
        /// </summary>
        public void OnPlayerDied()
        {
            if (!_isPlaying) return;

            _isPlaying = false;
            ShowGameOver(false);
        }

        /// <summary>
        /// Called when player completes level (for Chapter mode)
        /// </summary>
        public void OnLevelComplete()
        {
            if (!_isPlaying) return;

            _isPlaying = false;
            ShowGameOver(true);
        }

        private async void ShowGameOver(bool success)
        {
            Debug.Log($"[GameplaySceneController] Game over - Success: {success}");

            // Submit session results
            await SubmitSessionResults(success);

            // Show game over UI
            gameOverPanel?.SetActive(true);

            if (finalScoreText != null)
                finalScoreText.text = _currentScore.ToString();

            if (finalCoinsText != null)
                finalCoinsText.text = _currentCoins.ToString();

            // Show/hide revive button based on availability
            if (reviveButton != null)
            {
                var canRevive = _revivesUsed < 1 && _adService.IsRewardedAdReady;
                reviveButton.gameObject.SetActive(canRevive && !success);
            }
        }

        private async UniTask SubmitSessionResults(bool success)
        {
            // Use the domain GameStateService to end session
            if (_gameStateService is GRoll.Domain.Gameplay.GameStateService gameStateService)
            {
                var endResult = await gameStateService.EndSessionAsync(_currentScore, _currentCoins, success);

                if (endResult)
                {
                    Debug.Log("[GameplaySceneController] Session submitted successfully");
                }
                else
                {
                    Debug.LogWarning("[GameplaySceneController] Failed to submit session");
                }
            }
            else
            {
                Debug.LogWarning("[GameplaySceneController] GameStateService cast failed");
            }
        }

        #endregion

        #region Button Handlers

        private async void OnRetryClicked()
        {
            gameOverPanel?.SetActive(false);

            // Start new game
            var result = await _gameStateService.StartGameplayAsync(_gameStateService.CurrentMode);

            if (result.Success)
            {
                // Restart gameplay
                StartGameplay().Forget();
            }
            else
            {
                // Return to menu if can't retry
                await _gameStateService.ReturnToMetaAsync();
                await _sceneFlowManager.TransitionToAsync(SceneType.Meta);
            }
        }

        private async void OnMenuClicked()
        {
            gameOverPanel?.SetActive(false);
            Time.timeScale = 1f;

            await _gameStateService.ReturnToMetaAsync();
            await _sceneFlowManager.TransitionToAsync(SceneType.Meta);
        }

        private async void OnReviveClicked()
        {
            Debug.Log("[GameplaySceneController] Revive clicked");

            var adResult = await _adService.ShowRewardedAdAsync("revive");

            if (adResult.Success || adResult.ResultType == AdResultType.AdsDisabled)
            {
                // Revive player
                _revivesUsed++;
                _isPlaying = true;
                gameOverPanel?.SetActive(false);

                // Reset player position, restore some health, etc.
                _playerSpawnController.RevivePlayer();

                Debug.Log("[GameplaySceneController] Player revived");
            }
            else
            {
                Debug.Log("[GameplaySceneController] Revive ad skipped or failed");
            }
        }

        private void OnResumeClicked()
        {
            ResumeGame();
        }

        private async void OnQuitClicked()
        {
            pausePanel?.SetActive(false);
            Time.timeScale = 1f;

            // Submit partial results
            await SubmitSessionResults(false);

            await _gameStateService.ReturnToMetaAsync();
            await _sceneFlowManager.TransitionToAsync(SceneType.Meta);
        }

        #endregion

        #region UI Updates

        private void ShowLoading(string message)
        {
            loadingPanel?.SetActive(true);
            if (loadingText != null)
                loadingText.text = message;
        }

        private void HideLoading()
        {
            loadingPanel?.SetActive(false);
        }

        private void ShowGameplayUI()
        {
            pauseButton?.SetActive(true);
            gameOverPanel?.SetActive(false);
            pausePanel?.SetActive(false);
            UpdateScoreDisplay();
        }

        private void UpdateScoreDisplay()
        {
            if (scoreText != null)
                scoreText.text = _currentScore.ToString();

            if (coinText != null)
                coinText.text = _currentCoins.ToString();

            if (comboText != null)
            {
                comboText.text = _currentCombo > 1 ? $"x{_currentCombo}" : "";
            }
        }

        #endregion

        #region Event Handlers

        private void OnScoreChanged(int newScore)
        {
            _currentScore = newScore;
            UpdateScoreDisplay();
        }

        private void OnComboChanged(int newCombo)
        {
            _currentCombo = newCombo;
            UpdateScoreDisplay();
        }

        private void OnPhaseChanged(GamePhaseChangedMessage message)
        {
            if (message.NewPhase == GamePhase.Meta)
            {
                // Clean up if transitioning to meta
                _isPlaying = false;
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Called by gameplay systems when coins are collected
        /// </summary>
        public void OnCoinCollected(int amount = 1)
        {
            _currentCoins += amount;
            UpdateScoreDisplay();
        }

        #endregion
    }
}
