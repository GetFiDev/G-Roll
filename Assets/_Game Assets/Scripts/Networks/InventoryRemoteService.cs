using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Functions;

/// <summary>
/// Remote gateway for inventory operations (server-side authoritative).
/// Pure static service; no MonoBehaviour.
/// Endpoints (index.ts): getInventorySnapshot, purchaseItem, equipItem, unequipItem
/// Note: Always pass Dictionary<string, object> as payload to avoid IL2CPP anonymous-type issues.
/// </summary>
public static class InventoryRemoteService
{
    // ---- Models ----
    [Serializable]
    public class InventoryEntry
    {
        public bool owned;
        public bool equipped;
        public int quantity;
        // Timestamps are optional for client UI; omit to keep it simple
    }

    [Serializable]
    public class InventorySnapshot
    {
        public Dictionary<string, InventoryEntry> inventory = new();
        public List<string> equippedItemIds = new();
        public bool ok;
    }

    [Serializable]
    public class PurchaseResult
    {
        public bool ok;
        public string itemId;
        public bool alreadyOwned;
        public bool owned;           // server returns ownership after purchase
        public double currencyLeft;  // server returns remaining currency
        public string error;
    }

    [Serializable]
    public class EquipResult
    {
        public bool ok;
        public string itemId;
        public List<string> equippedItemIds;
        public string error;
    }

    private static FirebaseFunctions Fn => FirebaseFunctions.GetInstance("us-central1");

    // ---------------- API ----------------
    public static async Task<InventorySnapshot> GetInventoryAsync()
    {
        try
        {
            var callable = Fn.GetHttpsCallable("getInventorySnapshot");
            var resp = await callable.CallAsync(null);
            return ParseSnapshot(resp?.Data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] GetInventoryAsync FAILED: {e.Message}");
            return new InventorySnapshot { ok = false };
        }
    }

    public static async Task<PurchaseResult> PurchaseItemAsync(string itemId, string method = "GET")
    {
        var result = new PurchaseResult { ok = false, itemId = itemId, currencyLeft = 0, error = null };
        try
        {
            var payload = new Dictionary<string, object> {
                { "itemId", itemId },
                { "method", method }
            };
            var callable = Fn.GetHttpsCallable("purchaseItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.alreadyOwned = GetBool(dict, "alreadyOwned");
            result.owned = GetBool(dict, "owned") || result.alreadyOwned;
            result.currencyLeft = GetDouble(dict, "currencyLeft", 0);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] PurchaseItemAsync FAILED: {e.Message}");
            result.error = e.Message;
            return result;
        }
    }

    public static Task<PurchaseResult> PurchaseWithCurrencyAsync(string itemId)
        => PurchaseItemAsync(itemId, "Get");

    public static Task<PurchaseResult> PurchaseWithAdAsync(string itemId, string adToken)
    {
        // purchase with AD requires adToken, so call the function directly here
        return PurchaseItemWithAdInternalAsync(itemId, adToken);
    }

    public static Task<PurchaseResult> PurchaseWithIapAsync(string itemId, string platform, string receipt, string orderId)
    {
        return PurchaseItemWithIapInternalAsync(itemId, platform, receipt, orderId);
    }

    private static async Task<PurchaseResult> PurchaseItemWithAdInternalAsync(string itemId, string adToken)
    {
        var result = new PurchaseResult { ok = false, itemId = itemId, error = null };
        try
        {
            var payload = new Dictionary<string, object> {
                { "itemId", itemId },
                { "method", "AD" },
                { "adToken", adToken ?? string.Empty }
            };
            var callable = Fn.GetHttpsCallable("purchaseItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.alreadyOwned = GetBool(dict, "alreadyOwned");
            result.owned = GetBool(dict, "owned") || result.alreadyOwned;
            result.currencyLeft = GetDouble(dict, "currencyLeft", 0);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] PurchaseItemWithAd FAILED: {e.Message}");
            result.error = e.Message;
            return result;
        }
    }

    private static async Task<PurchaseResult> PurchaseItemWithIapInternalAsync(string itemId, string platform, string receipt, string orderId)
    {
        var result = new PurchaseResult { ok = false, itemId = itemId, error = null };
        try
        {
            var payload = new Dictionary<string, object> {
                { "itemId", itemId },
                { "method", "IAP" },
                { "platform", platform ?? string.Empty },
                { "receipt", receipt ?? string.Empty },
                { "orderId", orderId ?? string.Empty }
            };
            var callable = Fn.GetHttpsCallable("purchaseItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.alreadyOwned = GetBool(dict, "alreadyOwned");
            result.owned = GetBool(dict, "owned") || result.alreadyOwned;
            result.currencyLeft = GetDouble(dict, "currencyLeft", 0);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] PurchaseItemWithIAP FAILED: {e.Message}");
            result.error = e.Message;
            return result;
        }
    }

    public static async Task<EquipResult> EquipItemAsync(string itemId)
    {
        var result = new EquipResult { ok = false, itemId = itemId, equippedItemIds = new List<string>() };
        try
        {
            var payload = new Dictionary<string, object> { { "itemId", itemId } };
            var callable = Fn.GetHttpsCallable("equipItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.equippedItemIds = GetStringList(dict, "equippedItemIds");
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] EquipItemAsync FAILED: {e.Message}");
            result.error = e.Message;
            return result;
        }
    }

    public static async Task<EquipResult> UnequipItemAsync(string itemId)
    {
        var result = new EquipResult { ok = false, itemId = itemId, equippedItemIds = new List<string>() };
        try
        {
            var payload = new Dictionary<string, object> { { "itemId", itemId } };
            var callable = Fn.GetHttpsCallable("unequipItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.equippedItemIds = GetStringList(dict, "equippedItemIds"); // function may or may not return; tolerate null
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] UnequipItemAsync FAILED: {e.Message}");
            result.error = e.Message;
            return result;
        }
    }

    // ---------------- Parsing helpers ----------------
    private static InventorySnapshot ParseSnapshot(object data)
    {
        var snap = new InventorySnapshot { ok = false };
        var dict = data as IDictionary<string, object>;
        if (dict == null) return snap;
        snap.ok = GetBool(dict, "ok");

        // inventory map
        var inv = GetDict(dict, "inventory");
        if (inv != null)
        {
            foreach (var kv in inv)
            {
                var itemId = kv.Key;
                var entryObj = kv.Value as IDictionary<string, object>;
                if (entryObj == null) continue;
                var entry = new InventoryEntry
                {
                    owned = GetBool(entryObj, "owned"),
                    equipped = GetBool(entryObj, "equipped"),
                    quantity = (int)GetDouble(entryObj, "quantity", 0)
                };
                snap.inventory[itemId] = entry;
            }
        }

        snap.equippedItemIds = GetStringList(dict, "equippedItemIds");
        return snap;
    }

    private static IDictionary<string, object> GetDict(IDictionary<string, object> src, string key)
    {
        if (!src.TryGetValue(key, out var v)) return null;
        return v as IDictionary<string, object>;
    }

    private static bool GetBool(IDictionary<string, object> src, string key, bool def = false)
    {
        if (!src.TryGetValue(key, out var v) || v == null) return def;
        if (v is bool b) return b;
        if (v is int i) return i != 0;
        if (v is long l) return l != 0L;
        if (v is double d) return Math.Abs(d) > double.Epsilon;
        if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
        return def;
    }

    private static double GetDouble(IDictionary<string, object> src, string key, double def = 0)
    {
        if (!src.TryGetValue(key, out var v) || v == null) return def;
        if (v is double d) return d;
        if (v is float f) return f;
        if (v is int i) return i;
        if (v is long l) return l;
        double.TryParse(v.ToString(), out var parsed);
        return parsed;
    }

    private static List<string> GetStringList(IDictionary<string, object> src, string key)
    {
        var list = new List<string>();
        if (!src.TryGetValue(key, out var v) || v == null) return list;
        if (v is IEnumerable<object> eo)
        {
            foreach (var o in eo)
                if (o != null) list.Add(o.ToString());
        }
        else if (v is string s)
        {
            list.Add(s);
        }
        return list;
    }
}