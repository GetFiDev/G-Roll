using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
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
    /// Settings screen with audio, haptics, account management, and links.
    /// </summary>
    public class SettingsScreen : UIScreenBase
    {
        [Header("Audio Settings")]
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;

        [Header("Haptics")]
        [SerializeField] private Toggle hapticToggle;

        [Header("User Info")]
        [SerializeField] private TextMeshProUGUI displayNameText;
        [SerializeField] private Button editNameButton;
        [SerializeField] private TextMeshProUGUI userIdText;
        [SerializeField] private Button copyUserIdButton;

        [Header("Links")]
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button supportButton;
        [SerializeField] private Button rateAppButton;

        [Header("Account")]
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button deleteAccountButton;
        [SerializeField] private GameObject accountSectionContainer;

        [Header("App Info")]
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("Navigation")]
        [SerializeField] private Button backButton;
        [SerializeField] private BottomNavigation bottomNavigation;

        [Header("URLs")]
        [SerializeField] private string termsUrl = "https://example.com/terms";
        [SerializeField] private string privacyUrl = "https://example.com/privacy";
        [SerializeField] private string supportUrl = "https://example.com/support";

        [Inject] private IAudioService _audioService;
        [Inject] private IHapticService _hapticService;
        [Inject] private IUserProfileService _userProfileService;
        [Inject] private IAuthService _authService;
        [Inject] private IDialogService _dialogService;
        [Inject] private INavigationService _navigationService;

        protected override UniTask OnScreenEnterAsync(object parameters)
        {
            SetupUI();
            SetupListeners();
            LoadCurrentSettings();

            if (bottomNavigation != null)
            {
                bottomNavigation.SelectTab(BottomNavigation.NavTab.None, navigate: false);
            }

            return UniTask.CompletedTask;
        }

        private void SetupUI()
        {
            // Display user info
            UpdateUserInfo();

            // App version
            if (versionText != null)
            {
                versionText.text = $"Version {Application.version}";
            }

            // Show account section only if logged in
            if (accountSectionContainer != null)
            {
                accountSectionContainer.SetActive(_authService?.IsLoggedIn ?? false);
            }
        }

        private void SetupListeners()
        {
            // Audio toggles
            if (sfxToggle != null)
            {
                sfxToggle.onValueChanged.RemoveAllListeners();
                sfxToggle.onValueChanged.AddListener(OnSfxToggleChanged);
            }

            if (musicToggle != null)
            {
                musicToggle.onValueChanged.RemoveAllListeners();
                musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
            }

            // Volume sliders
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.RemoveAllListeners();
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            // Haptic toggle
            if (hapticToggle != null)
            {
                hapticToggle.onValueChanged.RemoveAllListeners();
                hapticToggle.onValueChanged.AddListener(OnHapticToggleChanged);
            }

            // User actions
            if (editNameButton != null)
            {
                editNameButton.onClick.RemoveAllListeners();
                editNameButton.onClick.AddListener(OnEditNameClicked);
            }

            if (copyUserIdButton != null)
            {
                copyUserIdButton.onClick.RemoveAllListeners();
                copyUserIdButton.onClick.AddListener(OnCopyUserIdClicked);
            }

            // Links
            if (termsButton != null)
            {
                termsButton.onClick.RemoveAllListeners();
                termsButton.onClick.AddListener(() => OpenUrl(termsUrl));
            }

            if (privacyButton != null)
            {
                privacyButton.onClick.RemoveAllListeners();
                privacyButton.onClick.AddListener(() => OpenUrl(privacyUrl));
            }

            if (supportButton != null)
            {
                supportButton.onClick.RemoveAllListeners();
                supportButton.onClick.AddListener(() => OpenUrl(supportUrl));
            }

            if (rateAppButton != null)
            {
                rateAppButton.onClick.RemoveAllListeners();
                rateAppButton.onClick.AddListener(OnRateAppClicked);
            }

            // Account
            if (logoutButton != null)
            {
                logoutButton.onClick.RemoveAllListeners();
                logoutButton.onClick.AddListener(OnLogoutClicked);
            }

            if (deleteAccountButton != null)
            {
                deleteAccountButton.onClick.RemoveAllListeners();
                deleteAccountButton.onClick.AddListener(OnDeleteAccountClicked);
            }

            // Navigation
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }
        }

        private void LoadCurrentSettings()
        {
            // Audio settings
            if (_audioService != null)
            {
                if (sfxToggle != null)
                {
                    sfxToggle.SetIsOnWithoutNotify(_audioService.IsSfxEnabled);
                }

                if (musicToggle != null)
                {
                    musicToggle.SetIsOnWithoutNotify(_audioService.IsMusicEnabled);
                }

                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.SetValueWithoutNotify(_audioService.SfxVolume);
                }

                if (musicVolumeSlider != null)
                {
                    musicVolumeSlider.SetValueWithoutNotify(_audioService.MusicVolume);
                }
            }

            // Haptic settings
            if (_hapticService != null && hapticToggle != null)
            {
                hapticToggle.SetIsOnWithoutNotify(_hapticService.IsEnabled);
            }
        }

        private void UpdateUserInfo()
        {
            if (_userProfileService != null)
            {
                if (displayNameText != null)
                {
                    displayNameText.text = _userProfileService.DisplayName ?? "Player";
                }
            }

            if (_authService != null && userIdText != null)
            {
                var userId = _authService.CurrentUserId;
                userIdText.text = !string.IsNullOrEmpty(userId) ? userId : "---";
            }
        }

        #region Audio Handlers

        private void OnSfxToggleChanged(bool isOn)
        {
            _audioService?.SetSfxEnabled(isOn);
            FeedbackService?.PlaySelectionHaptic();
        }

        private void OnMusicToggleChanged(bool isOn)
        {
            _audioService?.SetMusicEnabled(isOn);
            FeedbackService?.PlaySelectionHaptic();
        }

        private void OnSfxVolumeChanged(float value)
        {
            _audioService?.SetSfxVolume(value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            _audioService?.SetMusicVolume(value);
        }

        #endregion

        #region Haptic Handlers

        private void OnHapticToggleChanged(bool isOn)
        {
            _hapticService?.SetEnabled(isOn);

            if (isOn)
            {
                FeedbackService?.PlaySelectionHaptic();
            }
        }

        #endregion

        #region User Actions

        private void OnEditNameClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            EditNameAsync().Forget();
        }

        private async UniTaskVoid EditNameAsync()
        {
            if (_dialogService == null) return;

            var result = await _dialogService.ShowPopupAsync<NameInputPopup.NameInputResult>(
                "NameInputPopup",
                new NameInputPopup.NameInputParams
                {
                    CurrentName = _userProfileService?.DisplayName ?? "",
                    Title = "Change Name",
                    MinLength = 3,
                    MaxLength = 20
                }
            );

            if (result != null && result.Success)
            {
                UpdateUserInfo();
            }
        }

        private void OnCopyUserIdClicked()
        {
            var userId = _authService?.CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return;

            GUIUtility.systemCopyBuffer = userId;
            FeedbackService?.ShowSuccessToast("User ID copied!");
            FeedbackService?.PlaySelectionHaptic();
        }

        #endregion

        #region Links

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            FeedbackService?.PlaySelectionHaptic();
            Application.OpenURL(url);
        }

        private void OnRateAppClicked()
        {
            FeedbackService?.PlaySelectionHaptic();

#if UNITY_ANDROID
            Application.OpenURL("market://details?id=" + Application.identifier);
#elif UNITY_IOS
            // Replace with your App Store ID
            Application.OpenURL("itms-apps://itunes.apple.com/app/idYOUR_APP_ID");
#endif
        }

        #endregion

        #region Account Actions

        private void OnLogoutClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            ConfirmLogoutAsync().Forget();
        }

        private async UniTaskVoid ConfirmLogoutAsync()
        {
            if (_dialogService == null) return;

            var confirmed = await _dialogService.ShowConfirmAsync(
                "Logout",
                "Are you sure you want to logout?",
                "Logout",
                "Cancel"
            );

            if (confirmed)
            {
                await PerformLogoutAsync();
            }
        }

        private async UniTask PerformLogoutAsync()
        {
            if (_authService == null) return;

            try
            {
                await _authService.LogoutAsync();
                FeedbackService?.ShowSuccessToast("Logged out");

                // Navigate to login/home
                _navigationService?.NavigateTo("HomeScreen", clearStack: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsScreen] Logout failed: {ex.Message}");
                FeedbackService?.ShowErrorToast("Logout failed");
            }
        }

        private void OnDeleteAccountClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            ConfirmDeleteAccountAsync().Forget();
        }

        private async UniTaskVoid ConfirmDeleteAccountAsync()
        {
            if (_dialogService == null) return;

            var confirmed = await _dialogService.ShowConfirmAsync(
                "Delete Account",
                "This action is permanent and cannot be undone. All your progress will be lost. Are you sure?",
                "Delete",
                "Cancel"
            );

            if (confirmed)
            {
                // Double confirmation for destructive action
                var doubleConfirmed = await _dialogService.ShowConfirmAsync(
                    "Final Confirmation",
                    "Type DELETE to confirm account deletion.",
                    "I Understand",
                    "Cancel"
                );

                if (doubleConfirmed)
                {
                    await PerformDeleteAccountAsync();
                }
            }
        }

        private async UniTask PerformDeleteAccountAsync()
        {
            if (_authService == null) return;

            try
            {
                await _authService.DeleteAccountAsync();
                FeedbackService?.ShowSuccessToast("Account deleted");

                _navigationService?.NavigateTo("HomeScreen", clearStack: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsScreen] Delete account failed: {ex.Message}");
                FeedbackService?.ShowErrorToast("Failed to delete account");
            }
        }

        #endregion

        #region Navigation

        private void OnBackClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            _navigationService?.GoBack();
        }

        public override bool OnBackPressed()
        {
            OnBackClicked();
            return true;
        }

        #endregion
    }
}
