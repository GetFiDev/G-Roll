using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

/// <summary>
/// Centralized cache and controller for player's inventory.
/// Syncs with server via InventoryRemoteService.
/// Keeps an in-memory snapshot for fast queries by UI / gameplay.
/// </summary>
public class UserInventoryManager : MonoBehaviour
{
    public static UserInventoryManager Instance { get; private set; }

    public event Action OnInventoryChanged;

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

    /// <summary>
    /// Fetch inventory snapshot from server and cache locally.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        Debug.Log("[UserInventoryManager] Initializing inventory...");

        var snapshot = await InventoryRemoteService.GetInventoryAsync();
        if (snapshot == null || !snapshot.ok)
        {
            Debug.LogWarning("[UserInventoryManager] Failed to fetch inventory snapshot.");
            return;
        }

        ReplaceInventory(snapshot.inventory, snapshot.equippedItemIds);
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
        OnInventoryChanged?.Invoke();
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

                entry.owned = true; // quantity will be reconciled by server in RefreshAsync()
                _inventory[id] = entry;

                // UI'ya hemen yansıt
                OnInventoryChanged?.Invoke();

                // --- Server truth ile arkaplanda senkronize et ---
                _ = RefreshAsync(); // fire-and-forget; UI zaten yukarıda güncellendi

                Debug.Log($"[UserInventoryManager] Purchase success (optimistic) for {id}");
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
    
}