using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Auth
{
    /// <summary>
    /// Auth scene controller.
    /// Handles login UI and profile setup.
    /// Replaces old FirebaseLoginHandler and UILoginPanel functionality.
    /// </summary>
    public class AuthSceneController : MonoBehaviour
    {
        [Header("Login Panel")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private Button googleLoginButton;
        [SerializeField] private Button appleLoginButton;
        [SerializeField] private Button guestLoginButton;

        [Header("Profile Setup Panel")]
        [SerializeField] private GameObject profileSetupPanel;
        [SerializeField] private TMPro.TMP_InputField usernameInput;
        [SerializeField] private TMPro.TMP_InputField referralCodeInput;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMPro.TMP_Text errorText;

        [Header("Loading")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMPro.TMP_Text loadingText;

        private IAuthService _authService;
        private IUserProfileService _profileService;
        private IAppFlowService _appFlowService;
        private ISceneFlowManager _sceneFlowManager;
        private IMessageBus _messageBus;

        [Inject]
        public void Construct(
            IAuthService authService,
            IUserProfileService profileService,
            IAppFlowService appFlowService,
            ISceneFlowManager sceneFlowManager,
            IMessageBus messageBus)
        {
            _authService = authService;
            _profileService = profileService;
            _appFlowService = appFlowService;
            _sceneFlowManager = sceneFlowManager;
            _messageBus = messageBus;
        }

        private void Start()
        {
            // Setup button listeners
            if (googleLoginButton != null)
                googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);

            if (appleLoginButton != null)
                appleLoginButton.onClick.AddListener(OnAppleLoginClicked);

            if (guestLoginButton != null)
                guestLoginButton.onClick.AddListener(OnGuestLoginClicked);

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            // Subscribe to app flow events
            _messageBus.Subscribe<AppFlowWaitingForProfileMessage>(OnWaitingForProfile);
            _messageBus.Subscribe<AppFlowReadyMessage>(OnAppReady);

            // Determine initial state
            DetermineInitialState();
        }

        private void DetermineInitialState()
        {
            var state = _appFlowService.CurrentState;

            if (state == AppFlowState.WaitingForAuth)
            {
                ShowLoginPanel();
            }
            else if (state == AppFlowState.WaitingForProfile)
            {
                ShowProfileSetupPanel();
            }
            else if (state == AppFlowState.Ready)
            {
                // Already ready, transition to Meta
                _sceneFlowManager.TransitionToAsync(SceneType.Meta).Forget();
            }
        }

        private void ShowLoginPanel()
        {
            loginPanel?.SetActive(true);
            profileSetupPanel?.SetActive(false);
            loadingPanel?.SetActive(false);

            // Show/hide Apple button based on platform
#if UNITY_IOS
            appleLoginButton?.gameObject.SetActive(true);
#else
            appleLoginButton?.gameObject.SetActive(false);
#endif
        }

        private void ShowProfileSetupPanel()
        {
            loginPanel?.SetActive(false);
            profileSetupPanel?.SetActive(true);
            loadingPanel?.SetActive(false);

            // Clear previous input
            if (usernameInput != null)
                usernameInput.text = "";
            if (referralCodeInput != null)
                referralCodeInput.text = "";
            if (errorText != null)
                errorText.text = "";
        }

        private void ShowLoading(string message = "Loading...")
        {
            loadingPanel?.SetActive(true);
            if (loadingText != null)
                loadingText.text = message;
        }

        private void HideLoading()
        {
            loadingPanel?.SetActive(false);
        }

        #region Login Methods

        private async void OnGoogleLoginClicked()
        {
            ShowLoading("Signing in with Google...");

            var result = await _authService.SignInWithGoogleAsync();

            if (result.IsSuccess)
            {
                Debug.Log("[AuthSceneController] Google login successful");
                await _appFlowService.OnAuthenticationSuccessAsync();
            }
            else
            {
                HideLoading();
                ShowError($"Login failed: {result.ErrorMessage}");
            }
        }

        private async void OnAppleLoginClicked()
        {
            ShowLoading("Signing in with Apple...");

            var result = await _authService.SignInWithAppleAsync();

            if (result.IsSuccess)
            {
                Debug.Log("[AuthSceneController] Apple login successful");
                await _appFlowService.OnAuthenticationSuccessAsync();
            }
            else
            {
                HideLoading();
                ShowError($"Login failed: {result.ErrorMessage}");
            }
        }

        private async void OnGuestLoginClicked()
        {
            ShowLoading("Signing in as guest...");

            var result = await _authService.SignInAnonymouslyAsync();

            if (result.IsSuccess)
            {
                Debug.Log("[AuthSceneController] Guest login successful");
                await _appFlowService.OnAuthenticationSuccessAsync();
            }
            else
            {
                HideLoading();
                ShowError($"Login failed: {result.ErrorMessage}");
            }
        }

        #endregion

        #region Profile Setup Methods

        private async void OnContinueClicked()
        {
            var username = usernameInput?.text?.Trim();

            if (string.IsNullOrEmpty(username))
            {
                ShowError("Please enter a username");
                return;
            }

            if (username.Length < 3)
            {
                ShowError("Username must be at least 3 characters");
                return;
            }

            ShowLoading("Setting up profile...");

            var result = await _profileService.SetUsernameAsync(username);

            if (result.IsSuccess)
            {
                // Apply referral code if provided
                var referralCode = referralCodeInput?.text?.Trim();
                if (!string.IsNullOrEmpty(referralCode))
                {
                    await _profileService.ApplyReferralCodeAsync(referralCode);
                }

                Debug.Log("[AuthSceneController] Profile setup successful");
                await _appFlowService.OnProfileCompletedAsync();
            }
            else
            {
                HideLoading();

                // Handle specific error codes
                var errorMessage = result.ErrorMessage switch
                {
                    "USERNAME_TAKEN" => "This username is already taken",
                    "INVALID_USERNAME" => "Invalid username format",
                    _ => $"Failed to set username: {result.ErrorMessage}"
                };

                ShowError(errorMessage);
            }
        }

        #endregion

        #region Event Handlers

        private void OnWaitingForProfile(AppFlowWaitingForProfileMessage message)
        {
            HideLoading();
            ShowProfileSetupPanel();
        }

        private void OnAppReady(AppFlowReadyMessage message)
        {
            Debug.Log("[AuthSceneController] App ready - transitioning to Meta scene");
            _sceneFlowManager.TransitionToAsync(SceneType.Meta).Forget();
        }

        #endregion

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
            Debug.LogWarning($"[AuthSceneController] Error: {message}");
        }
    }
}
