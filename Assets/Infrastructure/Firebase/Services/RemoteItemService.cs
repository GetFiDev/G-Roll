using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase.Interfaces;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Remote item service implementation.
    /// Firebase Functions ile item verilerini ceker.
    /// </summary>
    public class RemoteItemService : IRemoteItemService
    {
        private readonly IGRollLogger _logger;
        private FirebaseFunctions _functions;

        private const string Region = "us-central1";

        [Inject]
        public RemoteItemService(IGRollLogger logger)
        {
            _logger = logger;
        }

        private FirebaseFunctions Functions =>
            _functions ??= FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, Region);

        public async UniTask<Dictionary<string, RemoteItemData>> FetchAllItemsAsync()
        {
            try
            {
                _logger.Log("[RemoteItemService] Fetching all items");

                var call = Functions.GetHttpsCallable("getAllItems");
                var res = await call.CallAsync(new Dictionary<string, object>());

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null)
                {
                    _logger.LogWarning("[RemoteItemService] Invalid response root");
                    return new Dictionary<string, RemoteItemData>();
                }

                var dict = new Dictionary<string, RemoteItemData>();
                if (!root.TryGetValue("items", out var itemsObj) || itemsObj == null)
                {
                    _logger.Log("[RemoteItemService] No items in response");
                    return dict;
                }

                var items = NormalizeToStringKeyDict(itemsObj);
                if (items == null)
                {
                    _logger.LogWarning("[RemoteItemService] Items is not a dictionary");
                    return dict;
                }

                foreach (var kv in items)
                {
                    string id = kv.Key;
                    var fields = NormalizeToStringKeyDict(kv.Value);
                    if (fields == null) continue;

                    var data = new RemoteItemData
                    {
                        ItemName = GetAs<string>(fields, "itemName", string.Empty),
                        ItemDescription = GetAs<string>(fields, "itemDescription", string.Empty),
                        ItemIconUrl = GetAs<string>(fields, "itemIconUrl", string.Empty),
                        ItemPremiumPrice = GetAs<double>(fields, "itemPremiumPrice", 0d),
                        ItemGetPrice = GetAs<double>(fields, "itemGetPrice", 0d),
                        ItemIsConsumable = GetAs<bool>(fields, "itemIsConsumable", false),
                        ItemIsRewardedAd = GetAs<bool>(fields, "itemIsRewardedAd", false),
                        ItemReferralThreshold = GetAs<int>(fields, "itemReferralThreshold", 0),
                        CoinMultiplierPercent = GetAs<double>(fields, "itemstat_coinMultiplierPercent", 0d),
                        ComboPower = GetAs<double>(fields, "itemstat_comboPower", 0d),
                        GameplaySpeedMultiplierPercent = GetAs<double>(fields, "itemstat_gameplaySpeedMultiplierPercent", 0d),
                        MagnetPowerPercent = GetAs<double>(fields, "itemstat_magnetPowerPercent", 0d),
                        PlayerAcceleration = GetAs<double>(fields, "itemstat_playerAcceleration", 0d),
                        PlayerSizePercent = GetAs<double>(fields, "itemstat_playerSizePercent", 0d),
                        PlayerSpeed = GetAs<double>(fields, "itemstat_playerSpeed", 0d),
                    };

                    dict[id] = data;
                }

                _logger.Log($"[RemoteItemService] Fetched {dict.Count} items");
                return dict;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] Fetch error: {ex.Message}");
                return new Dictionary<string, RemoteItemData>();
            }
        }

        public async UniTask<Dictionary<string, RemoteItemData>> FetchAllItemsWithIconsAsync()
        {
            var items = await FetchAllItemsAsync();

            foreach (var kvp in items)
            {
                var item = kvp.Value;
                if (!string.IsNullOrEmpty(item.ItemIconUrl) &&
                    (item.ItemIconUrl.StartsWith("http://") || item.ItemIconUrl.StartsWith("https://")))
                {
                    var texture = await DownloadTextureAsync(item.ItemIconUrl);
                    if (texture != null)
                    {
                        item.IconSprite = CreateSprite(texture);
                    }
                }
            }

            return items;
        }

        public async UniTask<Texture2D> DownloadTextureAsync(string url)
        {
            try
            {
                using var uwr = UnityWebRequestTexture.GetTexture(url);
                await uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    _logger.LogWarning($"[RemoteItemService] Failed to download texture from {url}: {uwr.error}");
                    return null;
                }

                return DownloadHandlerTexture.GetContent(uwr);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[RemoteItemService] Download texture error: {ex.Message}");
                return null;
            }
        }

        private static Sprite CreateSprite(Texture2D tex)
        {
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        public async UniTask<ItemOwnershipResponse> CheckOwnershipAsync()
        {
            try
            {
                _logger.Log("[RemoteItemService] Checking ownership");

                var call = Functions.GetHttpsCallable("checkOwnership");
                var res = await call.CallAsync(new Dictionary<string, object>());

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null || !GetAs<bool>(root, "ok", false))
                {
                    return new ItemOwnershipResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to check ownership"
                    };
                }

                var itemIds = new List<string>();
                if (root.TryGetValue("itemIds", out var ids) && ids is IList<object> idList)
                {
                    foreach (var id in idList)
                    {
                        if (id != null)
                            itemIds.Add(id.ToString());
                    }
                }

                return new ItemOwnershipResponse
                {
                    Success = true,
                    OwnedItemIds = itemIds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] CheckOwnership error: {ex.Message}");
                return new ItemOwnershipResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async UniTask<InventorySnapshotResponse> GetInventorySnapshotAsync()
        {
            try
            {
                _logger.Log("[RemoteItemService] Getting inventory snapshot");

                var call = Functions.GetHttpsCallable("getInventorySnapshot");
                var res = await call.CallAsync(new Dictionary<string, object>());

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null || !GetAs<bool>(root, "ok", false))
                {
                    return new InventorySnapshotResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to get inventory"
                    };
                }

                var inventory = new Dictionary<string, InventoryItemData>();
                if (root.TryGetValue("inventory", out var invObj))
                {
                    var invDict = NormalizeToStringKeyDict(invObj);
                    if (invDict != null)
                    {
                        foreach (var kv in invDict)
                        {
                            var itemDict = NormalizeToStringKeyDict(kv.Value);
                            if (itemDict == null) continue;

                            inventory[kv.Key] = new InventoryItemData
                            {
                                Id = kv.Key,
                                Owned = GetAs<bool>(itemDict, "owned", false),
                                Equipped = GetAs<bool>(itemDict, "equipped", false),
                                Quantity = GetAs<int>(itemDict, "quantity", 0),
                                IsConsumable = GetAs<bool>(itemDict, "itemIsConsumable", false)
                            };
                        }
                    }
                }

                var equippedIds = new List<string>();
                if (root.TryGetValue("equippedItemIds", out var eqIds) && eqIds is IList<object> eqList)
                {
                    foreach (var id in eqList)
                    {
                        if (id != null)
                            equippedIds.Add(id.ToString());
                    }
                }

                return new InventorySnapshotResponse
                {
                    Success = true,
                    Inventory = inventory,
                    EquippedItemIds = equippedIds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] GetInventorySnapshot error: {ex.Message}");
                return new InventorySnapshotResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async UniTask<PurchaseItemResponse> PurchaseItemAsync(string itemId, string method, string adToken = null)
        {
            try
            {
                _logger.Log($"[RemoteItemService] Purchasing item: {itemId} with method: {method}");

                var call = Functions.GetHttpsCallable("purchaseItem");
                var data = new Dictionary<string, object>
                {
                    { "itemId", itemId },
                    { "method", method }
                };

                if (!string.IsNullOrEmpty(adToken))
                {
                    data["adToken"] = adToken;
                }

                var res = await call.CallAsync(data);

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null || !GetAs<bool>(root, "ok", false))
                {
                    return new PurchaseItemResponse
                    {
                        Success = false,
                        ErrorMessage = "Purchase failed"
                    };
                }

                return new PurchaseItemResponse
                {
                    Success = true,
                    ItemId = GetAs<string>(root, "itemId", itemId),
                    Owned = GetAs<bool>(root, "owned", false),
                    IsConsumable = GetAs<bool>(root, "isConsumable", false),
                    CurrencyLeft = GetAs<double>(root, "currencyLeft", 0),
                    PremiumCurrencyLeft = GetAs<double>(root, "premiumCurrencyLeft", 0),
                    ExpiresAtMillis = root.TryGetValue("expiresAtMillis", out var exp) && exp != null
                        ? (long?)Convert.ToInt64(exp)
                        : null
                };
            }
            catch (FunctionsException fex)
            {
                _logger.LogError($"[RemoteItemService] Purchase error: {fex.ErrorCode} - {fex.Message}");
                return new PurchaseItemResponse
                {
                    Success = false,
                    ErrorMessage = fex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] Purchase error: {ex.Message}");
                return new PurchaseItemResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async UniTask<ItemEquipResponse> EquipItemAsync(string itemId)
        {
            try
            {
                _logger.Log($"[RemoteItemService] Equipping item: {itemId}");

                var call = Functions.GetHttpsCallable("equipItem");
                var res = await call.CallAsync(new Dictionary<string, object> { { "itemId", itemId } });

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null || !GetAs<bool>(root, "ok", false))
                {
                    return new ItemEquipResponse
                    {
                        Success = false,
                        ErrorMessage = "Equip failed"
                    };
                }

                return new ItemEquipResponse
                {
                    Success = true,
                    ItemId = GetAs<string>(root, "itemId", itemId)
                };
            }
            catch (FunctionsException fex)
            {
                _logger.LogError($"[RemoteItemService] Equip error: {fex.ErrorCode} - {fex.Message}");
                return new ItemEquipResponse
                {
                    Success = false,
                    ErrorMessage = fex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] Equip error: {ex.Message}");
                return new ItemEquipResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async UniTask<ItemEquipResponse> UnequipItemAsync(string itemId)
        {
            try
            {
                _logger.Log($"[RemoteItemService] Unequipping item: {itemId}");

                var call = Functions.GetHttpsCallable("unequipItem");
                var res = await call.CallAsync(new Dictionary<string, object> { { "itemId", itemId } });

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null || !GetAs<bool>(root, "ok", false))
                {
                    return new ItemEquipResponse
                    {
                        Success = false,
                        ErrorMessage = "Unequip failed"
                    };
                }

                return new ItemEquipResponse
                {
                    Success = true,
                    ItemId = GetAs<string>(root, "itemId", itemId)
                };
            }
            catch (FunctionsException fex)
            {
                _logger.LogError($"[RemoteItemService] Unequip error: {fex.ErrorCode} - {fex.Message}");
                return new ItemEquipResponse
                {
                    Success = false,
                    ErrorMessage = fex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] Unequip error: {ex.Message}");
                return new ItemEquipResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async UniTask<ActiveConsumablesResponse> GetActiveConsumablesAsync()
        {
            try
            {
                _logger.Log("[RemoteItemService] Getting active consumables");

                var call = Functions.GetHttpsCallable("getActiveConsumables");
                var res = await call.CallAsync(new Dictionary<string, object>());

                var root = NormalizeToStringKeyDict(res.Data);
                if (root == null || !GetAs<bool>(root, "ok", false))
                {
                    return new ActiveConsumablesResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to get active consumables"
                    };
                }

                var items = new List<ActiveConsumableData>();
                if (root.TryGetValue("items", out var itemsObj) && itemsObj is IList<object> itemList)
                {
                    foreach (var item in itemList)
                    {
                        var itemDict = NormalizeToStringKeyDict(item);
                        if (itemDict == null) continue;

                        items.Add(new ActiveConsumableData
                        {
                            ItemId = GetAs<string>(itemDict, "itemId", string.Empty),
                            Active = GetAs<bool>(itemDict, "active", false),
                            ExpiresAtMillis = itemDict.TryGetValue("expiresAtMillis", out var exp) && exp != null
                                ? (long?)Convert.ToInt64(exp)
                                : null
                        });
                    }
                }

                return new ActiveConsumablesResponse
                {
                    Success = true,
                    ServerNowMillis = GetAs<long>(root, "serverNowMillis", 0),
                    Items = items
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RemoteItemService] GetActiveConsumables error: {ex.Message}");
                return new ActiveConsumablesResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #region Helpers

        private static Dictionary<string, object> NormalizeToStringKeyDict(object dataObj)
        {
            if (dataObj == null) return null;

            if (dataObj is IDictionary<string, object> dso)
                return new Dictionary<string, object>(dso);

            if (dataObj is IDictionary dio)
            {
                var outDict = new Dictionary<string, object>();
                foreach (DictionaryEntry kv in dio)
                {
                    var key = kv.Key?.ToString();
                    if (!string.IsNullOrEmpty(key))
                        outDict[key] = kv.Value;
                }
                return outDict;
            }

            return null;
        }

        private static T GetAs<T>(IDictionary<string, object> dict, string key, T fallback = default)
        {
            if (dict == null) return fallback;
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;

            try
            {
                if (typeof(T) == typeof(string)) return (T)(object)v.ToString();
                if (typeof(T) == typeof(bool)) return (T)(object)Convert.ToBoolean(v);
                if (typeof(T) == typeof(int)) return (T)(object)Convert.ToInt32(v);
                if (typeof(T) == typeof(long)) return (T)(object)Convert.ToInt64(v);
                if (typeof(T) == typeof(float)) return (T)(object)Convert.ToSingle(v);
                if (typeof(T) == typeof(double)) return (T)(object)Convert.ToDouble(v);
                return (T)v;
            }
            catch
            {
                return fallback;
            }
        }

        #endregion
    }
}
