using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Functions;
using System.Linq;

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

    // -------- Robust dictionary & list converters (Firebase callable variations) --------
    private static IDictionary<string, object> AsStringObjectDict(object obj)
    {
        if (obj == null) return null;
        if (obj is IDictionary<string, object> d1) return d1;

        // Handle Hashtable / Dictionary<object, object> etc.
        if (obj is System.Collections.IDictionary id)
        {
            var outDict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry de in id)
            {
                var key = de.Key?.ToString() ?? "";
                if (key.Length == 0) continue;
                outDict[key] = de.Value;
            }
            return outDict;
        }

        return null;
    }

    private static IEnumerable<object> AsObjectEnumerable(object obj)
    {
        if (obj == null) return null;

        // IList (ArrayList, List<object>, etc.)
        if (obj is System.Collections.IList il)
        {
            return il.Cast<object>();
        }

        // object[]
        if (obj is object[] oa)
        {
            return oa.Cast<object>();
        }

        // IEnumerable<object>
        if (obj is IEnumerable<object> en)
        {
            return en;
        }

        // string (single)
        if (obj is string s)
        {
            return new object[] { s };
        }

        return null;
    }

    // -------- Helpers for ownership bootstrap (getAllItems + checkOwnership) --------
    private static async Task<List<string>> FetchAllItemIdsAsync()
    {
        try
        {
            var callable = Fn.GetHttpsCallable("getAllItems");
            var resp = await callable.CallAsync(null);

            if (resp == null || resp.Data == null)
            {
                Debug.LogWarning("[InventoryRemoteService] getAllItems returned null Data");
                return new List<string>();
            }

            var root = AsStringObjectDict(resp.Data);
            if (root == null)
            {
                Debug.LogWarning($"[InventoryRemoteService] getAllItems Data not dict: {resp.Data.GetType().FullName}");
                return new List<string>();
            }

            if (!root.TryGetValue("items", out var itemsRaw) || itemsRaw == null)
            {
                Debug.Log("[InventoryRemoteService] getAllItems has no 'items' field");
                return new List<string>();
            }

            var itemsDict = AsStringObjectDict(itemsRaw);
            if (itemsDict == null)
            {
                Debug.LogWarning($"[InventoryRemoteService] 'items' is not a dict: {itemsRaw.GetType().FullName}");
                return new List<string>();
            }

            var ids = new List<string>();
            foreach (var kv in itemsDict)
            {
                var id = IdUtil.NormalizeId(kv.Key);
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);

            Debug.Log($"[InventoryRemoteService] getAllItems OK, count={ids.Count}, sample=[{string.Join(", ", ids.Take(5))}]");
            return ids;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] FetchAllItemIdsAsync FAILED: {e}");
            return new List<string>();
        }
    }

    private static async Task<HashSet<string>> FetchOwnedIdsAsync()
    {
        try
        {
            var callable = Fn.GetHttpsCallable("checkOwnership");
            var resp = await callable.CallAsync(null);

            if (resp == null || resp.Data == null)
            {
                Debug.LogWarning("[InventoryRemoteService] checkOwnership returned null Data");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var root = AsStringObjectDict(resp.Data);
            if (root == null)
            {
                Debug.LogWarning($"[InventoryRemoteService] checkOwnership Data not dict: {resp.Data.GetType().FullName}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!root.TryGetValue("itemIds", out var idsRaw) || idsRaw == null)
            {
                Debug.Log("[InventoryRemoteService] checkOwnership has no 'itemIds' field");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var seq = AsObjectEnumerable(idsRaw);
            if (seq == null)
            {
                Debug.LogWarning($"[InventoryRemoteService] 'itemIds' not an array/list: {idsRaw.GetType().FullName}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in seq)
            {
                var s = o?.ToString();
                var id = IdUtil.NormalizeId(s);
                if (!string.IsNullOrEmpty(id)) set.Add(id);
            }

            Debug.Log($"[InventoryRemoteService] checkOwnership OK, count={set.Count}, sample=[{string.Join(", ", set.Take(5))}]");
            return set;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InventoryRemoteService] FetchOwnedIdsAsync FAILED: {e}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ---------------- API ----------------
    public static async Task<InventorySnapshot> GetInventoryAsync()
    {
        try
        {
            // 1) Orijinal snapshot (equipped/quantity vb. ayrıntılar burada)
            var callable = Fn.GetHttpsCallable("getInventorySnapshot");
            var resp = await callable.CallAsync(null);
            var snap = ParseSnapshot(resp?.Data) ?? new InventorySnapshot { ok = false };

            // 2) Ek aşama: tüm item ID'lerini ve owned set'ini çek
            //    (Public API değişmiyor; sadece snapshot'ı zenginleştiriyoruz.)
            var allIds = await FetchAllItemIdsAsync();
            var owned = await FetchOwnedIdsAsync();

            // 3) Merge:
            //    - Tüm item'ları snapshot.inventory içine ekle (yoksa oluştur)
            //    - Owned bilgisini checkOwnership sonucuyla override et
            //    - Equipped listesi snapshot'tan olduğu gibi kalsın
            if (snap.inventory == null)
                snap.inventory = new Dictionary<string, InventoryEntry>();

            foreach (var raw in allIds)
            {
                var id = IdUtil.NormalizeId(raw);
                if (!snap.inventory.TryGetValue(id, out var entry) || entry == null)
                {
                    entry = new InventoryEntry
                    {
                        owned = false,
                        equipped = false,
                        quantity = 0
                    };
                }

                if (owned.Contains(id))
                    entry.owned = true;

                snap.inventory[id] = entry;
            }

            // Başarı bayrağı: en az bir çağrı başarılıysa true kalsın
            snap.ok = snap.ok || (allIds.Count > 0);

            Debug.Log($"[InventoryRemoteService] GetInventoryAsync: merged all={allIds.Count} owned={owned.Count} invNow={snap.inventory.Count}");
            return snap;
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
        var id = IdUtil.NormalizeId(itemId);
        result.itemId = id;
        try
        {
            var payload = new Dictionary<string, object> {
                { "itemId", id },
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
        var id = IdUtil.NormalizeId(itemId);
        result.itemId = id;
        try
        {
            var payload = new Dictionary<string, object> {
                { "itemId", id },
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
        var id = IdUtil.NormalizeId(itemId);
        result.itemId = id;
        try
        {
            var payload = new Dictionary<string, object> {
                { "itemId", id },
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
        var id = IdUtil.NormalizeId(itemId);
        result.itemId = id;
        try
        {
            var payload = new Dictionary<string, object> { { "itemId", id } };
            var callable = Fn.GetHttpsCallable("equipItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.equippedItemIds = GetStringList(dict, "equippedItemIds").Select(IdUtil.NormalizeId).ToList();
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
        var id = IdUtil.NormalizeId(itemId);
        result.itemId = id;
        try
        {
            var payload = new Dictionary<string, object> { { "itemId", id } };
            var callable = Fn.GetHttpsCallable("unequipItem");
            var resp = await callable.CallAsync(payload);
            var dict = resp?.Data as IDictionary<string, object>;
            if (dict == null) { result.error = "Bad response"; return result; }
            result.ok = GetBool(dict, "ok");
            result.equippedItemIds = GetStringList(dict, "equippedItemIds").Select(IdUtil.NormalizeId).ToList(); // tolerate null/missing
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
        var dict = AsStringObjectDict(data);
        if (dict == null) return snap;
        snap.ok = GetBool(dict, "ok");

        // inventory map
        if (dict.TryGetValue("inventory", out var invRaw))
        {
            var inv = AsStringObjectDict(invRaw);
            if (inv != null)
            {
                foreach (var kv in inv)
                {
                    var rawId = kv.Key;
                    var entryObj = AsStringObjectDict(kv.Value);
                    if (entryObj == null) continue;

                    var entry = new InventoryEntry
                    {
                        owned = GetBool(entryObj, "owned"),
                        equipped = GetBool(entryObj, "equipped"),
                        quantity = (int)GetDouble(entryObj, "quantity", 0)
                    };
                    if (!entry.owned && entry.quantity > 0) entry.owned = true; // safety fallback
                    var id = IdUtil.NormalizeId(rawId);
                    snap.inventory[id] = entry;
                }
            }
        }

        // equippedItemIds
        if (dict.TryGetValue("equippedItemIds", out var eqRaw))
        {
            var seq = AsObjectEnumerable(eqRaw) ?? Array.Empty<object>();
            snap.equippedItemIds = seq.Select(o => IdUtil.NormalizeId(o?.ToString())).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        else
        {
            snap.equippedItemIds = new List<string>();
        }

        return snap;
    }

    private static IDictionary<string, object> GetDict(IDictionary<string, object> src, string key)
    {
        if (src == null) return null;
        if (!src.TryGetValue(key, out var v)) return null;
        return AsStringObjectDict(v);
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
        if (src == null) return list;
        if (!src.TryGetValue(key, out var v) || v == null) return list;

        var seq = AsObjectEnumerable(v);
        if (seq != null)
        {
            foreach (var o in seq)
                if (o != null) list.Add(o.ToString());
        }
        else if (v is string s)
        {
            list.Add(s);
        }
        return list;
    }
}