using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using GRoll.Infrastructure.Persistence;
using VContainer;

namespace GRoll.Domain.Shop
{
    /// <summary>
    /// Item service implementation.
    /// Firebase uzerinden item veritabani, satin alma ve envanter yonetimi.
    /// </summary>
    public class ItemService : IItemService
    {
        private readonly IRemoteItemService _remoteItemService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private Dictionary<string, ItemData> _items = new();
        private Dictionary<string, ItemOwnershipState> _ownershipCache = new();
        private bool _isInitialized;
        private bool _isInitializing;

        // Cache age tracking
        private DateTime _lastRemoteFetchTime = DateTime.MinValue;
        private const int CACHE_MAX_AGE_MINUTES = 60;

        public bool IsReady => _isInitialized && _items.Count > 0;

        [Inject]
        public ItemService(
            IRemoteItemService remoteItemService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _remoteItemService = remoteItemService;
            _messageBus = messageBus;
            _logger = logger;
        }

        public async UniTask InitializeAsync()
        {
            if (_isInitialized || _isInitializing) return;
            _isInitializing = true;

            try
            {
                _logger.Log("[ItemService] Initializing...");

                // 1. Try to load from local cache first (fast startup)
                var localCache = ItemLocalDatabase.Load();
                if (localCache != null && localCache.Count > 0)
                {
                    PopulateItemsFromRemoteData(localCache);
                    _logger.Log($"[ItemService] Loaded {_items.Count} items from local cache");
                }

                // 2. Check if we need to refresh from remote
                var cacheAge = DateTime.Now - _lastRemoteFetchTime;
                bool shouldFetchRemote = _lastRemoteFetchTime == DateTime.MinValue ||
                                          cacheAge.TotalMinutes >= CACHE_MAX_AGE_MINUTES;

                if (shouldFetchRemote)
                {
                    // Fetch from remote
                    var remoteItems = await _remoteItemService.FetchAllItemsWithIconsAsync();
                    if (remoteItems != null && remoteItems.Count > 0)
                    {
                        PopulateItemsFromRemoteData(remoteItems);
                        _lastRemoteFetchTime = DateTime.Now;

                        // Save to local cache
                        ItemLocalDatabase.Save(remoteItems);
                        _logger.Log($"[ItemService] Fetched {_items.Count} items from remote and cached");
                    }
                }

                // 3. Load ownership data
                await RefreshOwnershipCacheAsync();

                _isInitialized = true;
                _logger.Log("[ItemService] Initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ItemService] Initialization error: {ex.Message}");
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void PopulateItemsFromRemoteData(Dictionary<string, RemoteItemData> remoteData)
        {
            _items.Clear();

            foreach (var kv in remoteData)
            {
                var item = new ItemData
                {
                    Id = kv.Key,
                    Name = kv.Value.ItemName ?? string.Empty,
                    Description = kv.Value.ItemDescription ?? string.Empty,
                    IconUrl = kv.Value.ItemIconUrl ?? string.Empty,
                    IconSprite = kv.Value.IconSprite,
                    PremiumPrice = kv.Value.ItemPremiumPrice,
                    GetPrice = kv.Value.ItemGetPrice,
                    IsConsumable = kv.Value.ItemIsConsumable,
                    IsRewardedAd = kv.Value.ItemIsRewardedAd,
                    ReferralThreshold = kv.Value.ItemReferralThreshold,
                    Stats = new ItemStats
                    {
                        CoinMultiplierPercent = kv.Value.CoinMultiplierPercent,
                        ComboPower = kv.Value.ComboPower,
                        GameplaySpeedMultiplierPercent = kv.Value.GameplaySpeedMultiplierPercent,
                        MagnetPowerPercent = kv.Value.MagnetPowerPercent,
                        PlayerAcceleration = kv.Value.PlayerAcceleration,
                        PlayerSizePercent = kv.Value.PlayerSizePercent,
                        PlayerSpeed = kv.Value.PlayerSpeed
                    }
                };

                _items[kv.Key] = item;
            }
        }

        private async UniTask RefreshOwnershipCacheAsync()
        {
            var snapshot = await _remoteItemService.GetInventorySnapshotAsync();
            if (!snapshot.Success) return;

            _ownershipCache.Clear();

            foreach (var kv in snapshot.Inventory)
            {
                _ownershipCache[kv.Key] = new ItemOwnershipState
                {
                    Owned = kv.Value.Owned,
                    Equipped = kv.Value.Equipped,
                    Quantity = kv.Value.Quantity,
                    IsConsumable = kv.Value.IsConsumable
                };
            }

            // Mark equipped items
            foreach (var equippedId in snapshot.EquippedItemIds)
            {
                if (_ownershipCache.TryGetValue(equippedId, out var state))
                {
                    state.Equipped = true;
                    _ownershipCache[equippedId] = state;
                }
            }
        }

        public ItemData GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return _items.TryGetValue(itemId, out var item) ? item : null;
        }

        public IReadOnlyList<ItemData> GetAllItems()
        {
            return new List<ItemData>(_items.Values);
        }

        public async UniTask<ItemOwnershipState> GetOwnershipStateAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return default;

            // Return from cache if available
            if (_ownershipCache.TryGetValue(itemId, out var cached))
                return cached;

            // Refresh cache and try again
            await RefreshOwnershipCacheAsync();

            return _ownershipCache.TryGetValue(itemId, out var state) ? state : default;
        }

        public async UniTask<ItemPurchaseResult> PurchaseAsync(string itemId, PurchaseMethod method, string adToken = null)
        {
            _logger.Log($"[ItemService] Purchasing {itemId} with method {method}");

            var methodStr = method switch
            {
                PurchaseMethod.GET => "GET",
                PurchaseMethod.PREMIUM => "PREMIUM",
                PurchaseMethod.AD => "AD",
                _ => "GET"
            };

            var response = await _remoteItemService.PurchaseItemAsync(itemId, methodStr, adToken);

            if (response.Success)
            {
                // Update ownership cache
                _ownershipCache[itemId] = new ItemOwnershipState
                {
                    Owned = response.Owned,
                    Equipped = false,
                    IsConsumable = response.IsConsumable,
                    Quantity = response.IsConsumable ? 1 : 0
                };

                // Publish event
                _messageBus.Publish(new ItemPurchasedMessage(itemId, response.IsConsumable));
            }

            return new ItemPurchaseResult
            {
                Success = response.Success,
                ItemId = response.ItemId,
                Owned = response.Owned,
                IsConsumable = response.IsConsumable,
                CurrencyLeft = response.CurrencyLeft,
                PremiumCurrencyLeft = response.PremiumCurrencyLeft,
                ExpiresAtMillis = response.ExpiresAtMillis,
                ErrorMessage = response.ErrorMessage
            };
        }

        public async UniTask<bool> EquipAsync(string itemId)
        {
            _logger.Log($"[ItemService] Equipping {itemId}");

            var response = await _remoteItemService.EquipItemAsync(itemId);

            if (response.Success)
            {
                // Update ownership cache
                if (_ownershipCache.TryGetValue(itemId, out var state))
                {
                    state.Equipped = true;
                    _ownershipCache[itemId] = state;
                }

                // Publish event
                _messageBus.Publish(new ItemEquippedMessage(itemId, true));
            }

            return response.Success;
        }

        public async UniTask<bool> UnequipAsync(string itemId)
        {
            _logger.Log($"[ItemService] Unequipping {itemId}");

            var response = await _remoteItemService.UnequipItemAsync(itemId);

            if (response.Success)
            {
                // Update ownership cache
                if (_ownershipCache.TryGetValue(itemId, out var state))
                {
                    state.Equipped = false;
                    _ownershipCache[itemId] = state;
                }

                // Publish event
                _messageBus.Publish(new ItemEquippedMessage(itemId, false));
            }

            return response.Success;
        }

        public async UniTask<List<string>> GetOwnedItemIdsAsync()
        {
            var response = await _remoteItemService.CheckOwnershipAsync();
            return response.Success ? response.OwnedItemIds : new List<string>();
        }

        public async UniTask<List<string>> GetEquippedItemIdsAsync()
        {
            var snapshot = await _remoteItemService.GetInventorySnapshotAsync();
            return snapshot.Success ? snapshot.EquippedItemIds : new List<string>();
        }

        public async UniTask<List<ActiveConsumable>> GetActiveConsumablesAsync()
        {
            var response = await _remoteItemService.GetActiveConsumablesAsync();

            if (!response.Success)
                return new List<ActiveConsumable>();

            var result = new List<ActiveConsumable>();
            foreach (var item in response.Items)
            {
                result.Add(new ActiveConsumable
                {
                    ItemId = item.ItemId,
                    Active = item.Active,
                    ExpiresAtMillis = item.ExpiresAtMillis
                });
            }

            return result;
        }
    }

    #region Messages

    public readonly struct ItemPurchasedMessage : IMessage
    {
        public string ItemId { get; }
        public bool IsConsumable { get; }

        public ItemPurchasedMessage(string itemId, bool isConsumable)
        {
            ItemId = itemId;
            IsConsumable = isConsumable;
        }
    }

    public readonly struct ItemEquippedMessage : IMessage
    {
        public string ItemId { get; }
        public bool IsEquipped { get; }

        public ItemEquippedMessage(string itemId, bool isEquipped)
        {
            ItemId = itemId;
            IsEquipped = isEquipped;
        }
    }

    #endregion
}
