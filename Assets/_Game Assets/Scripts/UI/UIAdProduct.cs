using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Functions;
using Firebase.Extensions;
using System.Threading.Tasks;
using System;

public class UIAdProduct : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The ID of the ad product as defined in Cloud Firestore.")]
    [SerializeField] private string adId;

    [Header("Reward")]
    [Tooltip("Optional: Drag an AdRewardGranter component here to grant rewards on success.")]
    [SerializeField] private AdRewardGranter rewardGranter;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI usageDisplayTmpugui;
    [SerializeField] private Button actionButton;

    private int _dailyLimit = 0;
    private int _usedToday = 0;
    private bool _isLoading = false;

    private FirebaseFunctions _functions;

    private void OnEnable()
    {
        // Ensure Firebase is ready before calling. 
        // If UserDatabaseManager handles initialization, we can check it or wait.
        if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.IsFirebaseReady)
        {
            InitializeAndFetch();
        }
        else
        {
            StartCoroutine(WaitForFirebase());
        }
    }

    private IEnumerator WaitForFirebase()
    {
        while (UserDatabaseManager.Instance == null || !UserDatabaseManager.Instance.IsFirebaseReady)
        {
            yield return new WaitForSeconds(0.5f);
        }
        InitializeAndFetch();
    }

    private void InitializeAndFetch()
    {
        _functions = FirebaseFunctions.GetInstance("us-central1");
        
        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnWatchAdClicked);
            // Disable until we know data, or let it be generic? 
            // Better to disable to avoid premature clicks.
            actionButton.interactable = false;
        }

        FetchAdDetails();
    }

    private void FetchAdDetails()
    {
        if (_isLoading) return;
        _isLoading = true;

        var func = _functions.GetHttpsCallable("getAdProductDetails");
        var data = new Dictionary<string, object>
        {
            { "adId", adId }
        };

        func.CallAsync(data).ContinueWithOnMainThread(task =>
        {
            _isLoading = false;
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[UIAdProduct] Fetch failed or canceled: {task.Exception}");
                return;
            }

            // Parse result
            // Returns: { adId, dailyLimit, usedToday }
            if (task.Result.Data is IDictionary result)
            {
                if (result.Contains("dailyLimit")) 
                    _dailyLimit = Convert.ToInt32(result["dailyLimit"]);
                
                if (result.Contains("usedToday")) 
                    _usedToday = Convert.ToInt32(result["usedToday"]);

                UpdateUI();
            }
        });
    }

    private void UpdateUI()
    {
        if (usageDisplayTmpugui != null)
        {
            // Format: "used/limit" -> e.g. "1/5"
            // User requested: users/{uid}/adusagedata/{ad_id}/used_today + "/" + /appdata/adDatas/{documentname}/data/daily_limit
            usageDisplayTmpugui.text = $"{_usedToday}/{_dailyLimit}";
        }

        if (actionButton != null)
        {
            // Can claim if used < limit
            bool canClaim = _usedToday < _dailyLimit;
            actionButton.interactable = canClaim;
        }
    }

    private void OnWatchAdClicked()
    {
        if (_usedToday >= _dailyLimit) return;
        
        actionButton.interactable = false; // Prevent double clicks

#if UNITY_EDITOR
        // Mock success in editor
        Debug.Log("[UIAdProduct] Editor Mock Ad Success");
        OnAdSuccess();
#else
        // Using "product_reward" as placement ID. Assuming standard AdManager.
        AdManager.ShowRewarded("product_reward", (success) =>
        {
            if (success)
            {
                // Must run on main thread if callback is not on main thread?
                // Unity callbacks usually are main thread.
                OnAdSuccess();
            }
            else
            {
                Debug.Log("[UIAdProduct] Ad failed/skipped.");
                actionButton.interactable = true; // Re-enable
            }
        });
#endif
    }

    private void OnAdSuccess()
    {
        Debug.Log("[UIAdProduct] Ad Success! Processing rewards...");

        // 1. Grant Reward (if granter is assigned)
        if (rewardGranter != null)
        {
            rewardGranter.GrantReward(() => {
                Debug.Log("[UIAdProduct] Reward granted callback.");
            });
        }
        else
        {
            Debug.LogWarning("[UIAdProduct] No AdRewardGranter assigned!");
        }

        // 2. Optimistic Update of Usage
        _usedToday++;
        UpdateUI();

        // 3. Server Call for Usage Tracking
        IncrementOnServer();
    }

    private void IncrementOnServer()
    {
        var func = _functions.GetHttpsCallable("incrementAdUsage");
        var data = new Dictionary<string, object>
        {
            { "adId", adId }
        };

        func.CallAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[UIAdProduct] Increment ad usage failed: {task.Exception}");
                // We don't rollback locally because user already watched the ad and got reward.
                // Discrepancy will resolve on next fetch.
            }
            else
            {
                Debug.Log("[UIAdProduct] Usage incremented on server.");
            }
        });
    }
}
