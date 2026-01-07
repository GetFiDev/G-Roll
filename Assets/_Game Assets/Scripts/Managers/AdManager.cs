using System;
using UnityEngine;
using AppodealAds.Unity.Api;
using AppodealAds.Unity.Common;

public abstract class AdManager
{
    // A simplified listener class to bridge callbacks to Actions
    private class AdListener : IRewardedVideoAdListener, IInterstitialAdListener
    {
        private Action<bool> _onRewardedComplete;

        public void SetRewardedCallback(Action<bool> onComplete)
        {
            _onRewardedComplete = onComplete;
        }

        // --- Rewarded Video ---
        public void onRewardedVideoLoaded(bool precache) { }
        public void onRewardedVideoFailedToLoad() { }
        public void onRewardedVideoShowFailed() 
        { 
             _onRewardedComplete?.Invoke(false);
             _onRewardedComplete = null;
        }
        public void onRewardedVideoShown() { }
        public void onRewardedVideoFinished(double amount, string name) 
        { 
            // Mark validation success, but wait for closed? 
            // Usually 'Finished' means they watched it all.
            // verifying in 'Closed' is also common pattern but 'Finished' guarantees reward.
             _onRewardedComplete?.Invoke(true);
             _onRewardedComplete = null;
        }
        public void onRewardedVideoClosed(bool finished) 
        { 
            if (_onRewardedComplete != null)
            {
                // If we reach here and callback hasn't fired (e.g. skipped), fire false
                _onRewardedComplete.Invoke(finished);
                _onRewardedComplete = null;
            }
        }
        public void onRewardedVideoExpired() { }
        public void onRewardedVideoClicked() { }

        // --- Interstitial (Stub) ---
        public void onInterstitialLoaded(bool isPrecache) { }
        public void onInterstitialFailedToLoad() { }
        public void onInterstitialShowFailed() { }
        public void onInterstitialShown() { }
        public void onInterstitialClosed() { }
        public void onInterstitialClicked() { }
        public void onInterstitialExpired() { }
    }

    private static AdListener _listener;

    public static void Initialize()
    {
        if (_listener == null)
        {
            _listener = new AdListener();
            Appodeal.setRewardedVideoCallbacks(_listener);
            Appodeal.setInterstitialCallbacks(_listener);
        }
    }

    public static void ShowInterstitial(string placement)
    {
        // Empty as requested
    }

    public static void ShowRewarded(string placement, Action<bool> onComplete)
    {
        if (_listener == null) Initialize();

        // Elite Pass Check: Bypass Ads
        if (UserDatabaseManager.Instance != null && 
            UserDatabaseManager.Instance.currentUserData != null && 
            UserDatabaseManager.Instance.currentUserData.hasElitePass)
        {
            Debug.Log("Elite Pass active: Skipping Ad (Granting Reward).");
            onComplete?.Invoke(true);
            return;
        }

        if (Appodeal.isLoaded(Appodeal.REWARDED_VIDEO))
        {
            _listener.SetRewardedCallback(onComplete);
            Appodeal.show(Appodeal.REWARDED_VIDEO, placement);
        }
        else
        {
            Debug.Log("Rewared Ad not loaded");
            onComplete?.Invoke(false);
        }
    }
}