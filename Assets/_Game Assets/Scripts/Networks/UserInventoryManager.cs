using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.Collections.ObjectModel;

/// <summary>
/// Centralized cache and controller for player's inventory.
/// Syncs with server via InventoryRemoteService.
/// Keeps an in-memory snapshot for fast queries by UI / gameplay.
/// </summary>
public class UserInventoryManager : MonoBehaviour
{
    public static UserInventoryManager Instance { get; private set; }

    public event Action OnInventoryChanged;

    public event Action OnActiveConsumablesChanged;

    // Active consumables (server-authoritative). Key = normalized itemId
    private readonly Dictionary<string, InventoryRemoteService.ActiveConsumable> _activeConsumables
        = new Dictionary<string, InventoryRemoteService.ActiveConsumable>(StringComparer.OrdinalIgnoreCase);

    // Server time anchoring to compute stable countdowns client-side
    private long _serverNowAnchorMillis = 0;
    private DateTime _localAnchorUtc;

    private Dictionary<string, InventoryRemoteService.InventoryEntry> _inventory =
        new Dictionary<string, InventoryRemoteService.InventoryEntry>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _equipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool IsInitialized { get; private set; }

    // Replace local caches with normalized IDs and case-insensitive collections
    private void ReplaceInventory(
        Dictionary<string, InventoryRemoteService.InventoryEntry> src,
        IEnumerable<string> equippedIds)
    {
        _inventory.Clear();
        if (src != null)
        {
            foreach (var kv in src)
            {
                var nid = IdUtil.NormalizeId(kv.Key);
                var entry = kv.Value ?? new InventoryRemoteService.InventoryEntry();
                // Safety: quantity>0 implies owned (server already sends owned:true, but keep tolerant)
                if (!entry.owned && entry.quantity > 0) entry.owned = true;
                _inventory[nid] = entry;
            }
        }

        _equipped = new HashSet<string>(
            (equippedIds ?? Array.Empty<string>()).Select(IdUtil.NormalizeId),
            StringComparer.OrdinalIgnoreCase
        );
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void AnchorServerNow(long serverNowMillis)
    {
        if (serverNowMillis <= 0) return;
        _serverNowAnchorMillis = serverNowMillis;
        _localAnchorUtc = DateTime.UtcNow;
    }

    private long GetApproxServerNowMillis()
    {
        if (_serverNowAnchorMillis <= 0) return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var delta = (long)(DateTime.UtcNow - _localAnchorUtc).TotalMilliseconds;
        return _serverNowAnchorMillis + Math.Max(0, delta);
    }

    public bool IsConsumableActive(string itemId)
    {
        var id = IdUtil.NormalizeId(itemId);
        if (!_activeConsumables.TryGetValue(id, out var ac) || ac == null) return false;
        var nowMs = GetApproxServerNowMillis();
        return ac.expiresAtMillis > nowMs;
    }

    public TimeSpan GetConsumableRemaining(string itemId)
    {
        var id = IdUtil.NormalizeId(itemId);
        if (!_activeConsumables.TryGetValue(id, out var ac) || ac == null) return TimeSpan.Zero;
        var nowMs = GetApproxServerNowMillis();
        var remain = Math.Max(0, ac.expiresAtMillis - nowMs);
        return TimeSpan.FromMilliseconds(remain);
    }

    public IReadOnlyDictionary<string, InventoryRemoteService.ActiveConsumable> GetActiveConsumablesSnapshot()
        => new ReadOnlyDictionary<string, InventoryRemoteService.ActiveConsumable>(_activeConsumables);

    /// <summary>
    /// Fetch inventory snapshot from server and cache locally.
    /// </summary>
    /// <summary>
    /// Fetch inventory snapshot from server and cache locally.
    /// Called strictly by AppFlowManager after Auth & Profile are ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        Debug.Log("[UserInventoryManager] InitializeAsync called.");

        // Strict Check: Auth MUST be ready by now
        if (UserDatabaseManager.Instance == null || !UserDatabaseManager.Instance.IsAuthenticated())
        {
            Debug.LogError("[UserInventoryManager] CRITICAL: Auth not ready during explicit initialization!");
            return;
        }

        Debug.Log("[UserInventoryManager] Fetching inventory...");

        var snapshot = await InventoryRemoteService.GetInventoryAsync();
        if (snapshot == null || !snapshot.ok)
        {
            Debug.LogWarning("[UserInventoryManager] Failed to fetch inventory snapshot.");
            // We do NOT mark initialized if failed, allowing retry? 
            // Or should we mark it to avoid blocking loop? 
            // For now, let's allow retry or handle error upstream.
            return;
        }

        ReplaceInventory(snapshot.inventory, snapshot.equippedItemIds);
        // Also pull active consumables so shop/UI can render countdowns immediately
        await RefreshActiveConsumablesAsync();
        
        IsInitialized = true;
        OnInventoryChanged?.Invoke();
        Debug.Log($"[UserInventoryManager] Initialized. Items={_inventory.Count} Equipped={_equipped.Count}");
    }

    /// <summary>
    /// Refresh inventory manually (e.g. after a purchase).
    /// </summary>
    public async Task RefreshAsync()
    {
        Debug.Log("[UserInventoryManager] Refreshing inventory...");
        var snapshot = await InventoryRemoteService.GetInventoryAsync();
        if (snapshot == null || !snapshot.ok) return;

        ReplaceInventory(snapshot.inventory, snapshot.equippedItemIds);
        // Keep active consumables in sync as well (in case purchases happened)
        await RefreshActiveConsumablesAsync();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Resets the local inventory state. Call this on logout or before a new login.
    /// </summary>
    public void Reset()
    {
        Debug.Log("[UserInventoryManager] Resetting local state.");
        _inventory.Clear();
        _equipped.Clear();
        _activeConsumables.Clear();
        IsInitialized = false;
        OnInventoryChanged?.Invoke();
        OnActiveConsumablesChanged?.Invoke();
    }

    /// <summary>
    /// Returns true if player owns this item.
    /// </summary>
    public bool Owns(string itemId)
    {
        var id = IdUtil.NormalizeId(itemId);
        return _inventory.TryGetValue(id, out var e) && e.owned;
    }

    public bool IsOwned(string itemId)
    {
        return Owns(itemId);
    }

    /// <summary>
    /// Returns true if this item is equipped.
    /// </summary>
    public bool IsEquipped(string itemId)
    {
        return _equipped.Contains(IdUtil.NormalizeId(itemId));
    }

    public enum PurchaseMethod { Get, Ad, IAP }

    /// <summary>
    /// Try to purchase an item with a specific method (currency/ad/iap).
    /// </summary>
    public async Task<InventoryRemoteService.PurchaseResult> TryPurchaseItemAsync(string itemId, PurchaseMethod method)
    {
        var id = IdUtil.NormalizeId(itemId);
        InventoryRemoteService.PurchaseResult result = null;
        try
        {
            // call server (InventoryRemoteService will uppercase method internally)
            result = await InventoryRemoteService.PurchaseItemAsync(id, method.ToString());
            if (result == null)
            {
                Debug.LogWarning($"[UserInventoryManager] Null result for purchase {id}");
                return new InventoryRemoteService.PurchaseResult { ok = false, error = "Null result", itemId = id };
            }

            if (result.ok || result.alreadyOwned)
            {
                // --- Optimistic local update ---
                if (!_inventory.TryGetValue(id, out var entry) || entry == null)
                    entry = new InventoryRemoteService.InventoryEntry();

                entry.owned = true;
                _inventory[id] = entry;

                // UI'ya hemen yansıt
                OnInventoryChanged?.Invoke();

                // Bull Market items like to auto-activate on purchase, ensuring UI reflects this immediately (e.g. countdowns)
                await RefreshActiveConsumablesAsync();

                // REMOVED REFRESH: Trusting the transaction result to avoid race conditions.
                // Do not call RefreshAsync() here!

                Debug.Log($"[UserInventoryManager] Purchase success (server-confirmed) for {id}");
                return result;
            }

            // Hata: server'dan gelen mesajı yüzeye taşı
            Debug.LogWarning($"[UserInventoryManager] Purchase failed for {id}: {result.error}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UserInventoryManager] TryPurchaseItemAsync exception: {ex.Message}");
            return new InventoryRemoteService.PurchaseResult { ok = false, error = ex.Message, itemId = id };
        }
    }

    /// <summary>
    /// Try to equip an owned item (server authoritative).
    /// </summary>
    public async Task<bool> EquipAsync(string itemId)
    {
        var id = IdUtil.NormalizeId(itemId);
        if (!Owns(id))
        {
            Debug.LogWarning($"[UserInventoryManager] Can't equip unowned item {id}");
            return false;
        }

        var result = await InventoryRemoteService.EquipItemAsync(id);
        if (!result.ok)
        {
            Debug.LogWarning($"[UserInventoryManager] Equip failed: {result.error}");
            if (!string.IsNullOrEmpty(result.error) && result.error.Contains("MAX_EQUIPPED_REACHED"))
            {
                if (UIManager.Instance && UIManager.Instance.overlay)
                {
                    UIManager.Instance.overlay.ShowInventoryFullPanel();
                }
            }
            return false;
        }

        var equippedList = result.equippedItemIds;
        if (equippedList != null && equippedList.Count > 0)
        {
            _equipped = new HashSet<string>(
                equippedList.Select(IdUtil.NormalizeId),
                StringComparer.OrdinalIgnoreCase
            );
        }
        else
        {
            // Fallback: some server versions may not return equippedItemIds
            // Apply optimistic local toggle so UI doesn't blank out
            _equipped.Add(id);
            Debug.Log("[UserInventoryManager] EquipAsync: server returned no equipped list; applied local add.");
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Unequip an equipped item (server authoritative).
    /// </summary>
    public async Task<bool> UnequipAsync(string itemId)
    {
        var id = IdUtil.NormalizeId(itemId);
        if (!_equipped.Contains(id)) return true;

        var result = await InventoryRemoteService.UnequipItemAsync(id);
        if (!result.ok)
        {
            Debug.LogWarning($"[UserInventoryManager] Unequip failed: {result.error}");
            return false;
        }

        var equippedList = result.equippedItemIds;
        if (equippedList != null && equippedList.Count > 0)
        {
            _equipped = new HashSet<string>(
                equippedList.Select(IdUtil.NormalizeId),
                StringComparer.OrdinalIgnoreCase
            );
        }
        else
        {
            // Fallback: some server versions may not return equippedItemIds
            _equipped.Remove(id);
            Debug.Log("[UserInventoryManager] UnequipAsync: server returned no equipped list; applied local remove.");
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Get all owned item IDs.
    /// </summary>
    public List<string> GetOwnedItemIds()
    {
        var list = new List<string>();
        foreach (var kv in _inventory)
            if (kv.Value.owned)
                list.Add(kv.Key);
        return list;
    }

    /// <summary>
    /// Get all equipped item IDs.
    /// </summary>
    public List<string> GetEquippedItemIds() => new(_equipped);
    
    /// <summary>
    /// Fetch active consumables from server and update local cache.
    /// Fires OnActiveConsumablesChanged when changed.
    /// </summary>
    public async Task RefreshActiveConsumablesAsync()
    {
        try
        {
            var res = await InventoryRemoteService.GetActiveConsumablesAsync();
            if (res == null || !res.ok)
            {
                if (res != null && !string.IsNullOrEmpty(res.error))
                    Debug.LogWarning($"[UserInventoryManager] RefreshActiveConsumablesAsync error: {res.error}");
                return;
            }

            AnchorServerNow(res.serverNowMillis);

            bool changed = false;
            // Build new map
            var newMap = new Dictionary<string, InventoryRemoteService.ActiveConsumable>(StringComparer.OrdinalIgnoreCase);
            foreach (var ac in res.items ?? Enumerable.Empty<InventoryRemoteService.ActiveConsumable>())
            {
                if (string.IsNullOrEmpty(ac?.itemId)) continue;
                var nid = IdUtil.NormalizeId(ac.itemId);
                newMap[nid] = ac;
            }

            // Detect changes quickly by count or any differing expiry
            if (newMap.Count != _activeConsumables.Count) changed = true;
            else
            {
                foreach (var kv in newMap)
                {
                    if (!_activeConsumables.TryGetValue(kv.Key, out var old) || old.expiresAtMillis != kv.Value.expiresAtMillis)
                    {
                        changed = true; break;
                    }
                }
            }

            if (changed)
            {
                _activeConsumables.Clear();
                foreach (var kv in newMap) _activeConsumables[kv.Key] = kv.Value;
                OnActiveConsumablesChanged?.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UserInventoryManager] RefreshActiveConsumablesAsync EX: {e.Message}");
        }
    }
}