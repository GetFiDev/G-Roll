using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Inventory full warning popup.
    /// Offers options to go to inventory or dismiss.
    /// </summary>
    public class InventoryFullPopup : UIPopupBase
    {
        public class InventoryFullParams
        {
            public string ItemName { get; set; }
            public int CurrentCount { get; set; }
            public int MaxCapacity { get; set; }
        }

        public class InventoryFullResult
        {
            public bool GoToInventory { get; set; }
        }

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI capacityText;
        [SerializeField] private Image warningIcon;

        [Header("Capacity Display")]
        [SerializeField] private Image capacityFillImage;
        [SerializeField] private Color normalFillColor = new Color(0.2f, 0.7f, 0.2f, 1f);
        [SerializeField] private Color fullFillColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Header("Buttons")]
        [SerializeField] private Button goToInventoryButton;
        [SerializeField] private TextMeshProUGUI goToInventoryButtonText;
        [SerializeField] private Button dismissButton;
        [SerializeField] private TextMeshProUGUI dismissButtonText;

        [Inject] private INavigationService _navigationService;

        private InventoryFullParams _params;

        protected override UniTask OnPopupShowAsync(object parameters)
        {
            _params = parameters as InventoryFullParams ?? new InventoryFullParams();

            SetupUI();
            SetupButtonListeners();

            FeedbackService?.PlayWarningHaptic();

            return UniTask.CompletedTask;
        }

        private void SetupUI()
        {
            if (titleText != null)
            {
                titleText.text = "Inventory Full";
            }

            if (messageText != null)
            {
                if (!string.IsNullOrEmpty(_params.ItemName))
                {
                    messageText.text = $"Cannot add \"{_params.ItemName}\" - your inventory is full!";
                }
                else
                {
                    messageText.text = "Your inventory is full! Free up some space to collect more items.";
                }
            }

            if (capacityText != null && _params.MaxCapacity > 0)
            {
                capacityText.text = $"{_params.CurrentCount}/{_params.MaxCapacity}";
            }
            else if (capacityText != null)
            {
                capacityText.gameObject.SetActive(false);
            }

            UpdateCapacityBar();

            if (goToInventoryButtonText != null)
            {
                goToInventoryButtonText.text = "Go to Inventory";
            }

            if (dismissButtonText != null)
            {
                dismissButtonText.text = "OK";
            }
        }

        private void UpdateCapacityBar()
        {
            if (capacityFillImage == null || _params.MaxCapacity <= 0) return;

            var fillAmount = Mathf.Clamp01((float)_params.CurrentCount / _params.MaxCapacity);
            capacityFillImage.fillAmount = fillAmount;

            // Full or near-full shows warning color
            capacityFillImage.color = fillAmount >= 0.9f ? fullFillColor : normalFillColor;
        }

        private void SetupButtonListeners()
        {
            if (goToInventoryButton != null)
            {
                goToInventoryButton.onClick.RemoveAllListeners();
                goToInventoryButton.onClick.AddListener(OnGoToInventoryClicked);
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveAllListeners();
                dismissButton.onClick.AddListener(OnDismissClicked);
            }
        }

        private void OnGoToInventoryClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(new InventoryFullResult { GoToInventory = true });
        }

        private void OnDismissClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(new InventoryFullResult { GoToInventory = false });
        }

        public override bool OnBackPressed()
        {
            OnDismissClicked();
            return true;
        }
    }
}
