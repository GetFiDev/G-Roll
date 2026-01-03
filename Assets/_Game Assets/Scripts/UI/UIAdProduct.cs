using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIAdProduct : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string productId = "ad_reward_gems";
    [SerializeField] private int dailyLimit = 5;
    [SerializeField] private int rewardAmount = 50;

    [Header("UI References")]
    [SerializeField] private Button watchAdButton;
    [SerializeField] private TextMeshProUGUI statusText; // "Claim Free" or "Come Back Tomorrow"
    [SerializeField] private TextMeshProUGUI countText;  // "1/5"
    [SerializeField] private TextMeshProUGUI rewardAmountText;

    private UserDatabaseManager _userDb;

    private void Start()
    {
        _userDb = UserDatabaseManager.Instance;
        
        if (watchAdButton != null)
        {
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
        }

        if (rewardAmountText != null)
        {
            rewardAmountText.text = rewardAmount.ToString();
        }

        // Subscribe to updates to refresh UI (e.g. after claim)
        if (_userDb != null)
        {
            _userDb.OnUserDataSaved += OnUserDataUpdated;
            RefreshUI();
        }
    }

    private void OnDestroy()
    {
        if (_userDb != null)
        {
            _userDb.OnUserDataSaved -= OnUserDataUpdated;
        }
    }

    private void OnUserDataUpdated(NetworkingData.UserData data)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_userDb == null) return;

        int currentCount = _userDb.GetDailyAdClaimCount(productId);
        bool canClaim = _userDb.CanClaimAdProduct(productId, dailyLimit);

        if (countText != null)
        {
            // Count indicates how many claimed, or remaining? Usually "Claimed/Limit"
            countText.text = $"{currentCount}/{dailyLimit}";
        }

        if (statusText != null)
        {
            statusText.text = canClaim ? "Claim Free" : "Come back tomorrow";
        }

        if (watchAdButton != null)
        {
            watchAdButton.interactable = canClaim;
        }
    }

    private void OnWatchAdClicked()
    {
        if (_userDb == null) return;

        if (!_userDb.CanClaimAdProduct(productId, dailyLimit))
        {
            Debug.Log("[UIAdProduct] Daily limit reached.");
            return;
        }

        // Disable button while showing/processing
        watchAdButton.interactable = false;

        AdManager.ShowRewarded("product_reward", (success) =>
        {
            if (success)
            {
                // Grant Reward & Record Claim
                _ = _userDb.RecordAdClaimAsync(productId, rewardAmount); 
                // UI Refresh will happen via OnUserDataSaved event in RecordAdClaimAsync
            }
            else
            {
                // Ad failed or closed early
                watchAdButton.interactable = true; // Re-enable to try again
                Debug.Log("[UIAdProduct] Ad failed or skipped.");
            }
        });
    }
}
