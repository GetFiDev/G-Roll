using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Application
{
    /// <summary>
    /// Uygulama başlatma ve akış yönetimi servisi.
    /// Boot sequence'i orchestrate eder: Firebase Init -> Auth Check -> Profile Check -> Game Ready
    /// Eski AppFlowManager'ın yerine geçer.
    /// </summary>
    public class AppFlowService : IAppFlowService
    {
        private readonly IFirebaseGateway _firebaseGateway;
        private readonly IAuthService _authService;
        private readonly IUserProfileService _profileService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private AppFlowState _currentState = AppFlowState.None;

        public AppFlowState CurrentState => _currentState;
        public bool IsReady => _currentState == AppFlowState.Ready;

        public event Action<AppFlowStateChangedEventArgs> OnStateChanged;
        public event Action OnBootCompleted;
        public event Action<string> OnBootFailed;

        [Inject]
        public AppFlowService(
            IFirebaseGateway firebaseGateway,
            IAuthService authService,
            IUserProfileService profileService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _firebaseGateway = firebaseGateway;
            _authService = authService;
            _profileService = profileService;
            _messageBus = messageBus;
            _logger = logger;

            // Auth state değişikliklerini dinle
            _authService.OnAuthStateChanged += HandleAuthStateChanged;
        }

        public async UniTask StartBootSequenceAsync()
        {
            if (_currentState != AppFlowState.None)
            {
                _logger.LogWarning("[AppFlowService] Boot sequence already started.");
                return;
            }

            _logger.LogInfo("[AppFlowService] Starting boot sequence...");

            try
            {
                // Step 1: Initialize Firebase
                SetState(AppFlowState.Initializing, "Initializing Firebase...");

                if (!_firebaseGateway.IsInitialized)
                {
                    await _firebaseGateway.InitializeAsync();
                }

                if (!_firebaseGateway.IsInitialized)
                {
                    SetState(AppFlowState.Error, "Firebase initialization failed");
                    OnBootFailed?.Invoke("Firebase initialization failed");
                    return;
                }

                _logger.LogInfo("[AppFlowService] Firebase initialized successfully.");

                // Step 2: Check Auth
                await CheckAuthStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AppFlowService] Boot sequence failed: {ex.Message}");
                SetState(AppFlowState.Error, ex.Message);
                OnBootFailed?.Invoke(ex.Message);
            }
        }

        private async UniTask CheckAuthStateAsync()
        {
            SetState(AppFlowState.CheckingAuth, "Checking authentication...");

            if (_authService.IsAuthenticated)
            {
                _logger.LogInfo("[AppFlowService] User is authenticated. Checking profile...");
                await CheckProfileAsync();
            }
            else
            {
                _logger.LogInfo("[AppFlowService] User not authenticated. Waiting for login...");
                SetState(AppFlowState.WaitingForAuth, "Waiting for login...");
                // UI will show login panel
                _messageBus.Publish(new AppFlowWaitingForAuthMessage());
            }
        }

        public async UniTask OnAuthenticationSuccessAsync()
        {
            if (_currentState != AppFlowState.WaitingForAuth)
            {
                _logger.LogWarning($"[AppFlowService] OnAuthenticationSuccess called but state was {_currentState}");
            }

            _logger.LogInfo("[AppFlowService] Authentication successful. Checking profile...");
            await CheckProfileAsync();
        }

        private async UniTask CheckProfileAsync()
        {
            SetState(AppFlowState.CheckingProfile, "Checking profile...");

            var result = await _profileService.LoadProfileAsync();

            if (!result.IsSuccess)
            {
                _logger.LogError($"[AppFlowService] Failed to load profile: {result.ErrorMessage}");
                SetState(AppFlowState.Error, "Failed to load profile");
                OnBootFailed?.Invoke("Failed to load profile");
                return;
            }

            if (_profileService.IsComplete)
            {
                _logger.LogInfo("[AppFlowService] Profile is complete. Loading game data...");
                await LoadGameDataAsync();
            }
            else
            {
                _logger.LogInfo("[AppFlowService] Profile incomplete. Waiting for profile setup...");
                SetState(AppFlowState.WaitingForProfile, "Waiting for profile setup...");
                // UI will show set name panel
                _messageBus.Publish(new AppFlowWaitingForProfileMessage());
            }
        }

        public async UniTask OnProfileCompletedAsync()
        {
            if (_currentState != AppFlowState.WaitingForProfile)
            {
                _logger.LogWarning($"[AppFlowService] OnProfileCompleted called but state was {_currentState}");
            }

            _logger.LogInfo("[AppFlowService] Profile completed. Re-verifying...");

            // Re-verify profile
            var result = await _profileService.LoadProfileAsync();

            if (result.IsSuccess && _profileService.IsComplete)
            {
                await LoadGameDataAsync();
            }
            else
            {
                _logger.LogWarning("[AppFlowService] Profile still incomplete after completion callback.");
                SetState(AppFlowState.WaitingForProfile, "Profile still incomplete");
            }
        }

        private async UniTask LoadGameDataAsync()
        {
            SetState(AppFlowState.LoadingGameData, "Loading game data...");

            try
            {
                // Notify that game data loading started
                _messageBus.Publish(new AppFlowLoadingGameDataMessage());

                // Game data loading is handled by individual services
                // They will be initialized by VContainer's IAsyncStartable or on-demand

                // Small delay to ensure all services are ready
                await UniTask.Delay(100);

                // Boot complete!
                SetState(AppFlowState.Ready, "Ready");
                _logger.LogInfo("[AppFlowService] Boot sequence complete. App is ready.");

                _messageBus.Publish(new AppFlowReadyMessage());
                OnBootCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AppFlowService] Failed to load game data: {ex.Message}");
                SetState(AppFlowState.Error, ex.Message);
                OnBootFailed?.Invoke(ex.Message);
            }
        }

        private void HandleAuthStateChanged(AuthStateChangedEventArgs args)
        {
            if (args.Reason == AuthStateChangeReason.SignedOut)
            {
                _logger.LogInfo("[AppFlowService] User signed out. Resetting to WaitingForAuth.");
                SetState(AppFlowState.WaitingForAuth, "Signed out");
                _messageBus.Publish(new AppFlowWaitingForAuthMessage());
            }
        }

        private void SetState(AppFlowState newState, string message = null)
        {
            var previousState = _currentState;
            _currentState = newState;

            _logger.LogInfo($"[AppFlowService] State: {previousState} -> {newState}");

            OnStateChanged?.Invoke(new AppFlowStateChangedEventArgs
            {
                PreviousState = previousState,
                NewState = newState,
                Message = message
            });

            _messageBus.Publish(new AppFlowStateChangedMessage(previousState, newState, message));
        }
    }
}
