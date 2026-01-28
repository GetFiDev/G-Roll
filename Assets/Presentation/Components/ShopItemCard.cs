using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Interfaces.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Shop item card component displaying purchasable/equippable items.
    /// Handles different states: Locked, Available, Owned, Equipped.
    /// </summary>
    public class ShopItemCard : MonoBehaviour
    {
        public enum ItemState
        {
            Locked,
            Available,
            Owned,
            Equipped
        }

        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private GameObject equippedBadge;
        [SerializeField] private GameObject ownedBadge;
        [SerializeField] private Image backgroundImage;

        [Header("Currency Icons")]
        [SerializeField] private Sprite softCurrencyIcon;
        [SerializeField] private Sprite hardCurrencyIcon;

        [Header("State Colors")]
        [SerializeField] private Color lockedBackgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color availableBackgroundColor = Color.white;
        [SerializeField] private Color ownedBackgroundColor = new Color(0.8f, 1f, 0.8f, 1f);
        [SerializeField] private Color equippedBackgroundColor = new Color(0.8f, 0.9f, 1f, 1f);

        [Header("Processing")]
        [SerializeField] private GameObject processingOverlay;
        [SerializeField] private CanvasGroup canvasGroup;

        [Inject] private IInventoryService _inventoryService;
        [Inject] private ICurrencyService _currencyService;
        [Inject] private IDialogService _dialogService;
        [Inject] private IFeedbackService _feedbackService;

        private string _itemId;
        private string _slotId;
        private int _price;
        private CurrencyType _currencyType;
        private ItemState _currentState;
        private bool _isProcessing;

        public event Action<ShopItemCard> OnPurchased;
        public event Action<ShopItemCard> OnEquipped;

        private void Awake()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            if (processingOverlay != null)
            {
                processingOverlay.SetActive(false);
            }
        }

        public void SetData(
            string itemId,
            string name,
            Sprite icon,
            int price,
            CurrencyType currencyType,
            string slotId,
            ItemState state)
        {
            _itemId = itemId;
            _slotId = slotId;
            _price = price;
            _currencyType = currencyType;
            _currentState = state;

            if (nameText != null)
            {
                nameText.text = name;
            }

            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
            }

            UpdatePriceDisplay();
            UpdateState(state);
        }

        private void UpdatePriceDisplay()
        {
            if (priceText != null)
            {
                priceText.text = _price.ToString("N0");
            }

            if (currencyIcon != null)
            {
                currencyIcon.sprite = _currencyType == CurrencyType.SoftCurrency
                    ? softCurrencyIcon
                    : hardCurrencyIcon;
            }
        }

        public void UpdateState(ItemState state)
        {
            _currentState = state;

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(state == ItemState.Locked);
            }

            if (equippedBadge != null)
            {
                equippedBadge.SetActive(state == ItemState.Equipped);
            }

            if (ownedBadge != null)
            {
                ownedBadge.SetActive(state == ItemState.Owned);
            }

            if (priceText != null)
            {
                priceText.gameObject.SetActive(state == ItemState.Available);
            }

            if (currencyIcon != null)
            {
                currencyIcon.gameObject.SetActive(state == ItemState.Available);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = state switch
                {
                    ItemState.Locked => lockedBackgroundColor,
                    ItemState.Available => availableBackgroundColor,
                    ItemState.Owned => ownedBackgroundColor,
                    ItemState.Equipped => equippedBackgroundColor,
                    _ => availableBackgroundColor
                };
            }

            if (actionButton != null)
            {
                actionButton.interactable = state != ItemState.Locked && state != ItemState.Equipped;
            }

            if (actionButtonText != null)
            {
                actionButtonText.text = state switch
                {
                    ItemState.Locked => "LOCKED",
                    ItemState.Available => "BUY",
                    ItemState.Owned => "EQUIP",
                    ItemState.Equipped => "EQUIPPED",
                    _ => ""
                };
            }
        }

        private void OnActionButtonClicked()
        {
            if (_isProcessing) return;

            switch (_currentState)
            {
                case ItemState.Available:
                    TryPurchaseAsync().Forget();
                    break;
                case ItemState.Owned:
                    TryEquipAsync().Forget();
                    break;
            }
        }

        private async UniTaskVoid TryPurchaseAsync()
        {
            if (_currencyService == null) return;

            if (!_currencyService.CanAfford(_currencyType, _price))
            {
                _feedbackService?.ShowErrorToast("Not enough currency");
                _feedbackService?.PlayErrorHaptic();
                return;
            }

            if (_dialogService == null) return;

            var confirmed = await _dialogService.ShowConfirmAsync(
                "Purchase Item",
                $"Buy this item for {_price}?",
                "Buy",
                "Cancel"
            );

            if (!confirmed) return;

            await ProcessPurchaseAsync();
        }

        private async UniTask ProcessPurchaseAsync()
        {
            SetProcessing(true);

            try
            {
                var spendResult = await _currencyService.SpendCurrencyOptimisticAsync(
                    _currencyType,
                    _price,
                    $"shop_purchase_{_itemId}"
                );

                if (!spendResult.IsSuccess)
                {
                    _feedbackService?.ShowErrorToast(spendResult.Message ?? "Purchase failed");
                    return;
                }

                if (_inventoryService != null)
                {
                    var acquireResult = await _inventoryService.AcquireItemOptimisticAsync(_itemId, "shop");

                    if (acquireResult.IsSuccess)
                    {
                        UpdateState(ItemState.Owned);
                        _feedbackService?.ShowSuccessToast("Item purchased!");
                        _feedbackService?.PlaySuccessHaptic();
                        OnPurchased?.Invoke(this);
                    }
                    else
                    {
                        _feedbackService?.ShowErrorToast(acquireResult.Message ?? "Failed to acquire item");
                    }
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private async UniTaskVoid TryEquipAsync()
        {
            if (_inventoryService == null) return;

            SetProcessing(true);

            try
            {
                var result = await _inventoryService.EquipItemOptimisticAsync(_itemId, _slotId);

                if (result.IsSuccess)
                {
                    UpdateState(ItemState.Equipped);
                    _feedbackService?.ShowSuccessToast("Item equipped!");
                    _feedbackService?.PlaySuccessHaptic();
                    OnEquipped?.Invoke(this);
                }
                else
                {
                    _feedbackService?.ShowErrorToast(result.Message ?? "Failed to equip item");
                    _feedbackService?.PlayErrorHaptic();
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void SetProcessing(bool processing)
        {
            _isProcessing = processing;

            if (processingOverlay != null)
            {
                processingOverlay.SetActive(processing);
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = !processing;
            }

            if (actionButton != null)
            {
                actionButton.interactable = !processing && _currentState != ItemState.Locked && _currentState != ItemState.Equipped;
            }
        }

        public string ItemId => _itemId;
        public string SlotId => _slotId;
        public ItemState CurrentState => _currentState;
    }
}
