using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.SceneManagement;
using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Boot
{
    /// <summary>
    /// Boot scene controller.
    /// Manages the boot sequence and transitions to Auth or Meta scene.
    /// Replaces old BootManager and AppFlowManager functionality.
    /// </summary>
    public class BootSceneController : MonoBehaviour
    {
        [Header("Loading UI")]
        [SerializeField] private CanvasGroup loadingPanel;
        [SerializeField] private TMPro.TMP_Text statusText;
        [SerializeField] private UnityEngine.UI.Slider progressBar;

        private IAppFlowService _appFlowService;
        private ISceneFlowManager _sceneFlowManager;
        private IMessageBus _messageBus;

        [Inject]
        public void Construct(
            IAppFlowService appFlowService,
            ISceneFlowManager sceneFlowManager,
            IMessageBus messageBus)
        {
            _appFlowService = appFlowService;
            _sceneFlowManager = sceneFlowManager;
            _messageBus = messageBus;
        }

        private void Start()
        {
            // Subscribe to app flow events
            _appFlowService.OnStateChanged += HandleAppFlowStateChanged;
            _appFlowService.OnBootCompleted += HandleBootCompleted;
            _appFlowService.OnBootFailed += HandleBootFailed;

            // Subscribe to message bus for UI updates
            _messageBus.Subscribe<AppFlowWaitingForAuthMessage>(OnWaitingForAuth);
            _messageBus.Subscribe<AppFlowWaitingForProfileMessage>(OnWaitingForProfile);
            _messageBus.Subscribe<AppFlowReadyMessage>(OnAppReady);

            // Start boot sequence
            StartBootSequence().Forget();
        }

        private void OnDestroy()
        {
            if (_appFlowService != null)
            {
                _appFlowService.OnStateChanged -= HandleAppFlowStateChanged;
                _appFlowService.OnBootCompleted -= HandleBootCompleted;
                _appFlowService.OnBootFailed -= HandleBootFailed;
            }
        }

        private async UniTaskVoid StartBootSequence()
        {
            UpdateStatus("Initializing...", 0f);

            await _appFlowService.StartBootSequenceAsync();
        }

        private void HandleAppFlowStateChanged(AppFlowStateChangedEventArgs args)
        {
            var progress = args.NewState switch
            {
                AppFlowState.Initializing => 0.1f,
                AppFlowState.CheckingAuth => 0.3f,
                AppFlowState.WaitingForAuth => 0.4f,
                AppFlowState.CheckingProfile => 0.5f,
                AppFlowState.WaitingForProfile => 0.6f,
                AppFlowState.LoadingGameData => 0.8f,
                AppFlowState.Ready => 1f,
                _ => 0f
            };

            var message = args.NewState switch
            {
                AppFlowState.Initializing => "Initializing Firebase...",
                AppFlowState.CheckingAuth => "Checking authentication...",
                AppFlowState.WaitingForAuth => "Waiting for login...",
                AppFlowState.CheckingProfile => "Loading profile...",
                AppFlowState.WaitingForProfile => "Complete your profile...",
                AppFlowState.LoadingGameData => "Loading game data...",
                AppFlowState.Ready => "Ready!",
                AppFlowState.Error => "Error occurred",
                _ => ""
            };

            UpdateStatus(message, progress);
        }

        private void HandleBootCompleted()
        {
            Debug.Log("[BootSceneController] Boot completed!");
            // Transition will be handled by OnAppReady message
        }

        private void HandleBootFailed(string error)
        {
            Debug.LogError($"[BootSceneController] Boot failed: {error}");
            UpdateStatus($"Error: {error}", 0f);

            // Show retry option
            // TODO: Show error dialog with retry button
        }

        private void OnWaitingForAuth(AppFlowWaitingForAuthMessage message)
        {
            Debug.Log("[BootSceneController] Waiting for auth - transitioning to Auth scene");

            // Transition to Auth scene
            _sceneFlowManager.TransitionToAsync(SceneType.Auth).Forget();
        }

        private void OnWaitingForProfile(AppFlowWaitingForProfileMessage message)
        {
            Debug.Log("[BootSceneController] Waiting for profile - already in Auth scene or transitioning");

            // If we're still in boot scene, transition to Auth
            if (!_sceneFlowManager.IsTransitioning)
            {
                _sceneFlowManager.TransitionToAsync(SceneType.Auth).Forget();
            }
        }

        private void OnAppReady(AppFlowReadyMessage message)
        {
            Debug.Log("[BootSceneController] App ready - transitioning to Meta scene");

            // Transition to Meta scene
            _sceneFlowManager.TransitionToAsync(SceneType.Meta).Forget();
        }

        private void UpdateStatus(string message, float progress)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }

            if (progressBar != null)
            {
                progressBar.value = progress;
            }
        }
    }
}
