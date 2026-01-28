using Cysharp.Threading.Tasks;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Generic confirmation popup with customizable title, message, and buttons.
    /// Returns bool result indicating user's choice.
    /// </summary>
    public class ConfirmPopup : UIPopupBase
    {
        public class ConfirmParams
        {
            public string Title { get; set; } = "Confirm";
            public string Message { get; set; } = "Are you sure?";
            public string ConfirmText { get; set; } = "Yes";
            public string CancelText { get; set; } = "No";
            public bool ShowCancelButton { get; set; } = true;
            public bool IsDangerous { get; set; } = false;
            public Sprite Icon { get; set; }
        }

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject iconContainer;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;
        [SerializeField] private Image confirmButtonImage;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI cancelButtonText;
        [SerializeField] private GameObject cancelButtonContainer;

        [Header("Styling")]
        [SerializeField] private Color normalConfirmColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color dangerConfirmColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        private ConfirmParams _params;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            _params = parameters as ConfirmParams ?? new ConfirmParams();

            SetupUI();
            SetupButtonListeners();

            return UniTask.CompletedTask;
        }

        private void SetupUI()
        {
            if (titleText != null)
            {
                titleText.text = _params.Title;
            }

            if (messageText != null)
            {
                messageText.text = _params.Message;
            }

            if (iconContainer != null)
            {
                iconContainer.SetActive(_params.Icon != null);
            }

            if (iconImage != null && _params.Icon != null)
            {
                iconImage.sprite = _params.Icon;
            }

            if (confirmButtonText != null)
            {
                confirmButtonText.text = _params.ConfirmText;
            }

            if (cancelButtonText != null)
            {
                cancelButtonText.text = _params.CancelText;
            }

            if (cancelButtonContainer != null)
            {
                cancelButtonContainer.SetActive(_params.ShowCancelButton);
            }

            if (confirmButtonImage != null)
            {
                confirmButtonImage.color = _params.IsDangerous ? dangerConfirmColor : normalConfirmColor;
            }
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

        private void OnConfirmClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(true);
        }

        private void OnCancelClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(false);
        }

        public override bool OnBackPressed()
        {
            if (_params.ShowCancelButton)
            {
                OnCancelClicked();
            }
            return true;
        }
    }
}
