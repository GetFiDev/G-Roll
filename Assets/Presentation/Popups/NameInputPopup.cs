using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Name input popup for setting/changing user display name.
    /// Validates input and updates via UserProfileService.
    /// </summary>
    public class NameInputPopup : UIPopupBase
    {
        public class NameInputParams
        {
            public string CurrentName { get; set; }
            public string Title { get; set; } = "Enter Your Name";
            public string Placeholder { get; set; } = "Your name...";
            public int MinLength { get; set; } = 3;
            public int MaxLength { get; set; } = 20;
            public bool AllowCancel { get; set; } = true;
        }

        public class NameInputResult
        {
            public bool Success { get; set; }
            public string NewName { get; set; }
        }

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private TextMeshProUGUI placeholderText;
        [SerializeField] private TextMeshProUGUI charCountText;

        [Header("Validation")]
        [SerializeField] private TextMeshProUGUI validationText;
        [SerializeField] private Image inputBorderImage;
        [SerializeField] private Color normalBorderColor = Color.white;
        [SerializeField] private Color errorBorderColor = Color.red;
        [SerializeField] private Color validBorderColor = Color.green;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private GameObject cancelButtonContainer;

        [Header("Processing")]
        [SerializeField] private GameObject processingPanel;

        [Inject] private IUserProfileService _userProfileService;

        private NameInputParams _params;
        private bool _isValid;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            _params = parameters as NameInputParams ?? new NameInputParams();

            SetupUI();
            SetupInputField();
            SetupButtonListeners();
            ValidateInput();

            return UniTask.CompletedTask;
        }

        private void SetupUI()
        {
            if (titleText != null)
            {
                titleText.text = _params.Title;
            }

            if (placeholderText != null)
            {
                placeholderText.text = _params.Placeholder;
            }

            if (cancelButtonContainer != null)
            {
                cancelButtonContainer.SetActive(_params.AllowCancel);
            }

            if (validationText != null)
            {
                validationText.text = "";
            }
        }

        private void SetupInputField()
        {
            if (nameInputField == null) return;

            nameInputField.text = _params.CurrentName ?? "";
            nameInputField.characterLimit = _params.MaxLength;

            nameInputField.onValueChanged.RemoveAllListeners();
            nameInputField.onValueChanged.AddListener(OnInputChanged);

            nameInputField.onSubmit.RemoveAllListeners();
            nameInputField.onSubmit.AddListener(_ => OnConfirmClicked());

            // Focus input field
            nameInputField.Select();
            nameInputField.ActivateInputField();

            UpdateCharCount();
        }

        private void SetupButtonListeners()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        private void OnInputChanged(string value)
        {
            ValidateInput();
            UpdateCharCount();
        }

        private void UpdateCharCount()
        {
            if (charCountText == null || nameInputField == null) return;

            var current = nameInputField.text?.Length ?? 0;
            charCountText.text = $"{current}/{_params.MaxLength}";
        }

        private void ValidateInput()
        {
            var input = nameInputField?.text?.Trim() ?? "";
            string errorMessage = null;

            if (string.IsNullOrEmpty(input))
            {
                errorMessage = "Name cannot be empty";
                _isValid = false;
            }
            else if (input.Length < _params.MinLength)
            {
                errorMessage = $"Name must be at least {_params.MinLength} characters";
                _isValid = false;
            }
            else if (input.Length > _params.MaxLength)
            {
                errorMessage = $"Name cannot exceed {_params.MaxLength} characters";
                _isValid = false;
            }
            else if (!IsValidName(input))
            {
                errorMessage = "Name contains invalid characters";
                _isValid = false;
            }
            else
            {
                _isValid = true;
            }

            UpdateValidationUI(errorMessage);
        }

        private bool IsValidName(string name)
        {
            // Allow letters, numbers, spaces, underscores, hyphens
            foreach (var c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-')
                {
                    return false;
                }
            }
            return true;
        }

        private void UpdateValidationUI(string errorMessage)
        {
            if (validationText != null)
            {
                validationText.text = errorMessage ?? "";
                validationText.gameObject.SetActive(!string.IsNullOrEmpty(errorMessage));
            }

            if (inputBorderImage != null)
            {
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    inputBorderImage.color = errorBorderColor;
                }
                else if (_isValid && !string.IsNullOrEmpty(nameInputField?.text))
                {
                    inputBorderImage.color = validBorderColor;
                }
                else
                {
                    inputBorderImage.color = normalBorderColor;
                }
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = _isValid;
            }
        }

        private void OnConfirmClicked()
        {
            if (!_isValid) return;

            FeedbackService?.PlaySelectionHaptic();
            SubmitNameAsync().Forget();
        }

        private async UniTaskVoid SubmitNameAsync()
        {
            var newName = nameInputField?.text?.Trim() ?? "";

            SetProcessing(true);

            try
            {
                if (_userProfileService != null)
                {
                    var result = await _userProfileService.UpdateDisplayNameOptimisticAsync(newName);

                    if (result.IsSuccess)
                    {
                        FeedbackService?.ShowSuccessToast("Name updated!");
                        FeedbackService?.PlaySuccessHaptic();

                        CloseWithResult(new NameInputResult
                        {
                            Success = true,
                            NewName = newName
                        });
                    }
                    else
                    {
                        FeedbackService?.ShowErrorToast(result.Message ?? "Failed to update name");
                        FeedbackService?.PlayErrorHaptic();
                    }
                }
                else
                {
                    // No service - just return the name
                    CloseWithResult(new NameInputResult
                    {
                        Success = true,
                        NewName = newName
                    });
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void OnCancelClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(new NameInputResult { Success = false });
        }

        public override bool OnBackPressed()
        {
            if (_params.AllowCancel)
            {
                OnCancelClicked();
                return true;
            }
            return false;
        }

        private void SetProcessing(bool processing)
        {
            if (processingPanel != null)
            {
                processingPanel.SetActive(processing);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = !processing && _isValid;
            }

            if (cancelButton != null)
            {
                cancelButton.interactable = !processing;
            }

            if (nameInputField != null)
            {
                nameInputField.interactable = !processing;
            }
        }
    }
}
