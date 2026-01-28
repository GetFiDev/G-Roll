using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Pause popup shown during gameplay.
    /// Provides resume, settings, and quit options.
    /// </summary>
    public class PausePopup : UIPopupBase
    {
        public class PauseResult
        {
            public bool Quit { get; set; }
        }

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Audio Toggles")]
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;

        [Inject] private IAudioService _audioService;
        [Inject] private IDialogService _dialogService;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            SetupButtonListeners();
            SetupAudioToggles();

            return UniTask.CompletedTask;
        }

        private void SetupButtonListeners()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        private void SetupAudioToggles()
        {
            if (musicToggle != null)
            {
                musicToggle.isOn = _audioService?.IsMusicEnabled ?? true;
                musicToggle.onValueChanged.RemoveAllListeners();
                musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
            }

            if (sfxToggle != null)
            {
                sfxToggle.isOn = _audioService?.IsSfxEnabled ?? true;
                sfxToggle.onValueChanged.RemoveAllListeners();
                sfxToggle.onValueChanged.AddListener(OnSfxToggleChanged);
            }
        }

        private void OnResumeClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(new PauseResult { Quit = false });
        }

        private void OnSettingsClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            // Settings are inline in pause popup for this implementation
        }

        private void OnQuitClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            ConfirmQuitAsync().Forget();
        }

        private async UniTaskVoid ConfirmQuitAsync()
        {
            if (_dialogService == null) return;

            var confirmed = await _dialogService.ShowConfirmAsync(
                "Quit Game",
                "Are you sure you want to quit? Your progress will be lost.",
                "Quit",
                "Cancel"
            );

            if (confirmed)
            {
                CloseWithResult(new PauseResult { Quit = true });
            }
        }

        private void OnMusicToggleChanged(bool isOn)
        {
            _audioService?.SetMusicEnabled(isOn);
        }

        private void OnSfxToggleChanged(bool isOn)
        {
            _audioService?.SetSfxEnabled(isOn);
        }

        public override bool OnBackPressed()
        {
            OnResumeClicked();
            return true;
        }
    }
}
