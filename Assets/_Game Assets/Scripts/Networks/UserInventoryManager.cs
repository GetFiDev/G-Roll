using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Centralized cache and controller for player's inventory.
/// Syncs with server via InventoryRemoteService.
/// Keeps an in-memory snapshot for fast queries by UI / gameplay.
/// </summary>
public class UserInventoryManager : MonoBehaviour
{
    public static UserInventoryManager Instance { get; private set; }

    public event Action OnInventoryChanged;

    private Dictionary<string, InventoryRemoteService.InventoryEntry> _inventory = new();
    private HashSet<string> _equipped = new();

    public bool IsInitialized { get; private set; }

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

        _inventory = snapshot.inventory ?? new Dictionary<string, InventoryRemoteService.InventoryEntry>();
        _equipped = new HashSet<string>(snapshot.equippedItemIds ?? new List<string>());
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

        _inventory = snapshot.inventory ?? new Dictionary<string, InventoryRemoteService.InventoryEntry>();
        _equipped = new HashSet<string>(snapshot.equippedItemIds ?? new List<string>());
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Returns true if player owns this item.
    /// </summary>
    public bool Owns(string itemId)
    {
        return _inventory.ContainsKey(itemId) && _inventory[itemId].owned;
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
        return _equipped.Contains(itemId);
    }

    public enum PurchaseMethod { Get, Ad, IAP }

    /// <summary>
    /// Try to purchase an item with a specific method (currency/ad/iap).
    /// </summary>
    public async Task<InventoryRemoteService.PurchaseResult> TryPurchaseItemAsync(string itemId, PurchaseMethod method)
    {
        InventoryRemoteService.PurchaseResult result = null;
        try
        {
            result = await InventoryRemoteService.PurchaseItemAsync(itemId, method.ToString());
            if (result == null)
            {
                Debug.LogWarning($"[UserInventoryManager] Null result for purchase {itemId}");
                return new InventoryRemoteService.PurchaseResult { ok = false, error = "Null result" };
            }

            if (result.ok || result.alreadyOwned)
            {
                await RefreshAsync();
                Debug.Log($"[UserInventoryManager] Purchase success for {itemId}");
                return result;
            }

            Debug.LogWarning($"[UserInventoryManager] Purchase failed for {itemId}: {result.error}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UserInventoryManager] TryPurchaseItemAsync exception: {ex.Message}");
            return new InventoryRemoteService.PurchaseResult { ok = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Try to equip an owned item (server authoritative).
    /// </summary>
    public async Task<bool> EquipAsync(string itemId)
    {
        if (!Owns(itemId))
        {
            Debug.LogWarning($"[UserInventoryManager] Can't equip unowned item {itemId}");
            return false;
        }

        var result = await InventoryRemoteService.EquipItemAsync(itemId);
        if (!result.ok)
        {
            Debug.LogWarning($"[UserInventoryManager] Equip failed: {result.error}");
            return false;
        }

        _equipped = new HashSet<string>(result.equippedItemIds ?? new List<string>());
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Unequip an equipped item (server authoritative).
    /// </summary>
    public async Task<bool> UnequipAsync(string itemId)
    {
        if (!_equipped.Contains(itemId)) return true;

        var result = await InventoryRemoteService.UnequipItemAsync(itemId);
        if (!result.ok)
        {
            Debug.LogWarning($"[UserInventoryManager] Unequip failed: {result.error}");
            return false;
        }

        _equipped = new HashSet<string>(result.equippedItemIds ?? new List<string>());
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