using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Components;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Screens
{
    /// <summary>
    /// Shop screen with horizontal scrolling tabs and item grid.
    /// Supports multiple categories: MyItems, Referral, Core, Pro, Bull.
    /// </summary>
    public class ShopScreen : UIScreenBase
    {
        public enum ShopTab
        {
            MyItems,
            Referral,
            Core,
            Pro,
            Bull
        }

        [Serializable]
        public class TabButton
        {
            public Button button;
            public Image iconImage;
            public TextMeshProUGUI labelText;
            public ShopTab tab;
            public Sprite activeSprite;
            public Sprite inactiveSprite;
        }

        [Header("Tabs")]
        [SerializeField] private TabButton[] tabButtons;

        [Header("Scroll View")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentPanel;
        [SerializeField] private float snapDuration = 0.35f;

        [Header("Item Grid")]
        [SerializeField] private Transform itemGridContainer;
        [SerializeField] private ShopItemCard itemCardPrefab;

        [Header("Currency Display")]
        [SerializeField] private OptimisticCurrencyDisplay softCurrencyDisplay;
        [SerializeField] private OptimisticCurrencyDisplay hardCurrencyDisplay;

        [Header("Bottom Navigation")]
        [SerializeField] private BottomNavigation bottomNavigation;

        [Header("Loading")]
        [SerializeField] private GameObject loadingPanel;

        [Inject] private IInventoryService _inventoryService;
        [Inject] private ICurrencyService _currencyService;

        private ShopTab _currentTab = ShopTab.Core;
        private List<ShopItemCard> _instantiatedCards = new();
        private Coroutine _snapCoroutine;
        private bool _isSnapping;

        protected override async UniTask OnScreenEnterAsync(object parameters)
        {
            SetupTabListeners();
            SubscribeToMessages();

            if (bottomNavigation != null)
            {
                bottomNavigation.SelectTab(BottomNavigation.NavTab.Shop, navigate: false);
            }

            SelectTab(ShopTab.Core);
            await LoadItemsForTabAsync(_currentTab);
        }

        private void SetupTabListeners()
        {
            foreach (var tabButton in tabButtons)
            {
                if (tabButton.button != null)
                {
                    var tab = tabButton.tab;
                    tabButton.button.onClick.RemoveAllListeners();
                    tabButton.button.onClick.AddListener(() => OnTabClicked(tab));
                }
            }
        }

        private void SubscribeToMessages()
        {
            SubscribeToMessage<InventoryChangedMessage>(OnInventoryChanged);
            SubscribeToMessage<CurrencyChangedMessage>(OnCurrencyChanged);
        }

        private void OnTabClicked(ShopTab tab)
        {
            if (_isSnapping || tab == _currentTab) return;

            SelectTab(tab);
            LoadItemsForTabAsync(tab).Forget();
        }

        private void SelectTab(ShopTab tab)
        {
            _currentTab = tab;

            foreach (var tabButton in tabButtons)
            {
                var isActive = tabButton.tab == tab;
                UpdateTabVisual(tabButton, isActive);
            }

            SnapToTab(tab);
        }

        private void UpdateTabVisual(TabButton tabButton, bool isActive)
        {
            if (tabButton.iconImage != null)
            {
                tabButton.iconImage.sprite = isActive ? tabButton.activeSprite : tabButton.inactiveSprite;
            }

            if (tabButton.labelText != null)
            {
                tabButton.labelText.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }

        private void SnapToTab(ShopTab tab)
        {
            if (scrollRect == null || contentPanel == null) return;

            var tabIndex = (int)tab;
            var tabCount = Enum.GetValues(typeof(ShopTab)).Length;
            var targetPosition = tabCount > 1 ? (float)tabIndex / (tabCount - 1) : 0f;

            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
            }

            _snapCoroutine = StartCoroutine(SmoothSnapCoroutine(targetPosition));
        }

        private IEnumerator SmoothSnapCoroutine(float targetPosition)
        {
            _isSnapping = true;

            var startPosition = scrollRect.horizontalNormalizedPosition;
            var elapsed = 0f;

            while (elapsed < snapDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / snapDuration);
                var easedT = 1f - Mathf.Pow(1f - t, 3f);

                scrollRect.horizontalNormalizedPosition = Mathf.Lerp(startPosition, targetPosition, easedT);
                yield return null;
            }

            scrollRect.horizontalNormalizedPosition = targetPosition;
            _isSnapping = false;
        }

        private async UniTask LoadItemsForTabAsync(ShopTab tab)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }

            ClearItems();

            try
            {
                // TODO: Load items from item database/service based on tab
                // For now, create placeholder items
                var items = GetItemsForTab(tab);

                foreach (var itemData in items)
                {
                    CreateItemCard(itemData);
                }

                await UniTask.Yield();
            }
            finally
            {
                if (loadingPanel != null)
                {
                    loadingPanel.SetActive(false);
                }
            }
        }

        private List<ShopItemData> GetItemsForTab(ShopTab tab)
        {
            // Placeholder - actual implementation will fetch from item database
            return new List<ShopItemData>();
        }

        private void CreateItemCard(ShopItemData itemData)
        {
            if (itemCardPrefab == null || itemGridContainer == null) return;

            var card = Instantiate(itemCardPrefab, itemGridContainer);

            var state = GetItemState(itemData.itemId);

            card.SetData(
                itemData.itemId,
                itemData.name,
                itemData.icon,
                itemData.price,
                itemData.currencyType,
                itemData.slotId,
                state
            );

            card.OnPurchased += OnItemPurchased;
            card.OnEquipped += OnItemEquipped;

            _instantiatedCards.Add(card);
        }

        private ShopItemCard.ItemState GetItemState(string itemId)
        {
            if (_inventoryService == null)
                return ShopItemCard.ItemState.Available;

            if (_inventoryService.IsEquipped(itemId))
                return ShopItemCard.ItemState.Equipped;

            if (_inventoryService.HasItem(itemId))
                return ShopItemCard.ItemState.Owned;

            return ShopItemCard.ItemState.Available;
        }

        private void ClearItems()
        {
            foreach (var card in _instantiatedCards)
            {
                if (card != null)
                {
                    card.OnPurchased -= OnItemPurchased;
                    card.OnEquipped -= OnItemEquipped;
                    Destroy(card.gameObject);
                }
            }
            _instantiatedCards.Clear();
        }

        private void OnItemPurchased(ShopItemCard card)
        {
            RefreshCardStates();
        }

        private void OnItemEquipped(ShopItemCard card)
        {
            foreach (var otherCard in _instantiatedCards)
            {
                if (otherCard != card && otherCard.SlotId == card.SlotId)
                {
                    if (otherCard.CurrentState == ShopItemCard.ItemState.Equipped)
                    {
                        otherCard.UpdateState(ShopItemCard.ItemState.Owned);
                    }
                }
            }
        }

        private void RefreshCardStates()
        {
            foreach (var card in _instantiatedCards)
            {
                var newState = GetItemState(card.ItemId);
                card.UpdateState(newState);
            }
        }

        private void OnInventoryChanged(InventoryChangedMessage msg)
        {
            RefreshCardStates();
        }

        private void OnCurrencyChanged(CurrencyChangedMessage msg)
        {
            // Currency displays update themselves
        }

        protected override UniTask OnScreenExitAsync()
        {
            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
            }

            ClearItems();

            return base.OnScreenExitAsync();
        }

        public ShopTab CurrentTab => _currentTab;

        private class ShopItemData
        {
            public string itemId;
            public string name;
            public Sprite icon;
            public int price;
            public CurrencyType currencyType;
            public string slotId;
        }
    }
}
