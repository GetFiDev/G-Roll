using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages revive logic for endless mode.
/// Tracks revive count, calculates pricing, and coordinates the revive flow.
/// </summary>
public class ReviveController : MonoBehaviour
{
    public static ReviveController Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Base cost for first revive in GET currency")]
    [SerializeField] private float baseRevivePrice = 0.5f;
    
    [Tooltip("Price multiplier per revive (2 = doubles each time)")]
    [SerializeField] private float priceMultiplier = 2f;
    
    [Tooltip("Radius around player to clear hazards on revive")]
    [SerializeField] private float hazardClearRadius = 15f;

    // Runtime state
    private int _reviveCount;
    private bool _isReviving;

    /// <summary>
    /// Number of revives used in current session.
    /// </summary>
    public int ReviveCount => _reviveCount;

    /// <summary>
    /// Current price for next revive (increases exponentially).
    /// </summary>
    public float CurrentRevivePrice => baseRevivePrice * Mathf.Pow(priceMultiplier, _reviveCount);

    /// <summary>
    /// Whether a revive is currently in progress.
    /// </summary>
    public bool IsReviving => _isReviving;

    public event Action OnReviveStarted;
    public event Action OnReviveCompleted;

    private void Awake()
    {
        Debug.Log($"[ReviveController] Awake called on {gameObject.name}");
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ReviveController] Duplicate instance found, destroying...");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("[ReviveController] Instance initialized.");
    }

    private void Start()
    {
        Debug.Log($"[ReviveController] Start. Hazard Radius: {hazardClearRadius}");
    }

    /// <summary>
    /// Reset revive state for a new session.
    /// Call this when a new endless run starts.
    /// </summary>
    public void ResetForNewSession()
    {
        _reviveCount = 0;
        _isReviving = false;
        Debug.Log("[ReviveController] Reset for new session.");
    }

    /// <summary>
    /// Check if player can afford a currency revive.
    /// </summary>
    public bool CanAffordCurrencyRevive()
    {
        if (UserDatabaseManager.Instance == null) return false;
        if (UserDatabaseManager.Instance.currentUserData == null) return false;
        
        return UserDatabaseManager.Instance.currentUserData.currency >= CurrentRevivePrice;
    }

    /// <summary>
    /// Execute currency-based revive. Deducts currency and triggers revive.
    /// </summary>
    public async void ExecuteCurrencyRevive(Action onComplete = null)
    {
        if (_isReviving)
        {
            Debug.LogWarning("[ReviveController] Revive already in progress.");
            return;
        }

        float cost = CurrentRevivePrice;
        
        if (!CanAffordCurrencyRevive())
        {
            Debug.LogWarning($"[ReviveController] Cannot afford revive. Cost: {cost}");
            return;
        }

        _isReviving = true;
        OnReviveStarted?.Invoke();

        // Deduct currency locally (server sync handled by normal currency flow)
        if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
        {
            UserDatabaseManager.Instance.currentUserData.currency -= cost;
            Debug.Log($"[ReviveController] Deducted {cost} GET for revive. Remaining: {UserDatabaseManager.Instance.currentUserData.currency}");
        }

        // Execute the actual revive
        // Execute the actual revive
        ExecuteReviveLogic();
        
        _reviveCount++;
        _isReviving = false;
        
        OnReviveCompleted?.Invoke();
        onComplete?.Invoke();
    }

    /// <summary>
    /// Execute ad-based revive. Called after ad is successfully watched.
    /// </summary>
    public void ExecuteAdRevive(Action onComplete = null)
    {
        if (_isReviving)
        {
            Debug.LogWarning("[ReviveController] Revive already in progress.");
            return;
        }

        _isReviving = true;
        OnReviveStarted?.Invoke();

        ExecuteReviveLogic();
        
        _reviveCount++;
        _isReviving = false;
        
        OnReviveCompleted?.Invoke();
        onComplete?.Invoke();
    }

    /// <summary>
    /// Core revive logic - resets player and clears hazards.
    /// Can be called externally (e.g. from UILevelEnd) after payment verification.
    /// </summary>
    public void ExecuteReviveLogic()
    {
        Debug.Log($"[ReviveController] Executing revive #{_reviveCount + 1}");

        // Get player reference
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[ReviveController] Player not found!");
            return;
        }

        var playerCtrl = player.GetComponent<PlayerController>();
        var playerMov = player.GetComponent<PlayerMovement>();

        // 1. Clear nearby hazards
        ClearNearbyHazards(player.transform.position);

        // 2. Reset player position (X = 0, keep Z)
        Vector3 newPos = player.transform.position;
        newPos.x = 0f;
        newPos.y = 0.5f; // Ensure player is at proper height
        player.transform.position = newPos;
        
        // 3. Reset player state
        if (playerCtrl != null)
        {
            playerCtrl.ResetPlayerForRevive();
        }

        // 4. Reset direction to forward
        if (playerMov != null)
        {
            playerMov._isFrozen = false;
            playerMov.SetDirection(Vector3.forward);
        }

        // 5. Tell GameplayManager to resume
        if (GameplayManager.Instance != null)
        {
            GameplayManager.Instance.ResumeAfterRevive();
        }

        Debug.Log("[ReviveController] Revive complete. Waiting for player input.");
    }

    /// <summary>
    /// Clear obstacles, collectibles, and boosters near the player.
    /// Map chunks are preserved.
    /// </summary>
    /// <summary>
    /// Clear obstacles, collectibles, and boosters near the player.
    /// Map chunks are preserved.
    /// </summary>
    private void ClearNearbyHazards(Vector3 centerPos)
    {
        // Use a position on the track (y=0) for the check to ensure we catch objects 
        // even if the player fell off the map.
        Vector3 checkPos = new Vector3(0f, 0f, centerPos.z);
        
        var allColliders = Physics.OverlapSphere(checkPos, hazardClearRadius);
        HashSet<GameObject> objectsToDestroy = new HashSet<GameObject>();
        int clearedCount = 0;

        foreach (var col in allColliders)
        {
            if (col == null) continue;
            
            // Skip player
            if (col.CompareTag("Player")) continue;
            
            // Skip the Map object itself
            if (col.GetComponent<Map>() != null) continue;

            // Skip LevelSide limiters
            if (col.gameObject.name.Contains("LevelSide")) continue;

            // Check for Obstacles (Inheriting from Wall)
            // This covers Wall, MovingWall, RotatorHammer, Piston, LaserGate, etc.
            var wall = col.GetComponentInParent<Wall>();
            if (wall != null)
            {
                objectsToDestroy.Add(wall.gameObject);
                continue;
            }
            
            // Collectibles
            var coin = col.GetComponentInParent<Coin>();
            if (coin != null)
            {
                objectsToDestroy.Add(coin.gameObject);
                continue;
            }
            
            // Boosters
            var booster = col.GetComponentInParent<BoosterBase>();
            if (booster != null)
            {
                objectsToDestroy.Add(booster.gameObject);
                continue;
            }
        }

        foreach (var obj in objectsToDestroy)
        {
            if (obj != null)
            {
                Debug.Log($"[ReviveController] Clearing: {obj.name}");
                Destroy(obj);
                clearedCount++;
            }
        }

        Debug.Log($"[ReviveController] Cleared {clearedCount} objects around {checkPos} within {hazardClearRadius}m radius.");
    }
}
