using System;
using UnityEngine;
using AppodealAds.Unity.Api;
using AppodealAds.Unity.Common;

public abstract class AdManager
{
    // A simplified listener class to bridge callbacks to Actions
    private class AdListener : IRewardedVideoAdListener, IInterstitialAdListener, IAppodealInitializationListener
    {
        private Action<bool> _onRewardedComplete;
        private bool _hasReceivedReward;

        public void SetRewardedCallback(Action<bool> onComplete)
        {
            _onRewardedComplete = onComplete;
            _hasReceivedReward = false;
        }

        // --- Rewarded Video ---
        public void onRewardedVideoLoaded(bool precache) { }
        public void onRewardedVideoFailedToLoad() { }
        public void onRewardedVideoShowFailed() 
        { 
             _onRewardedComplete?.Invoke(false);
             _onRewardedComplete = null;
             _hasReceivedReward = false;
        }
        public void onRewardedVideoShown() { }
        public void onRewardedVideoFinished(double amount, string name) 
        { 
            // FIX: Sadece reward alındığını işaretle, callback'i onRewardedVideoClosed'da çağır
            // Bu, kullanıcının videoyu tam izlediğini garanti eder
            Debug.Log($"[AdManager] onRewardedVideoFinished: amount={amount}, name={name}");
            _hasReceivedReward = true;
        }
        public void onRewardedVideoClosed(bool finished) 
        { 
            Debug.Log($"[AdManager] onRewardedVideoClosed: finished={finished}, hasReceivedReward={_hasReceivedReward}");
            if (_onRewardedComplete != null)
            {
                // FIX: Hem finished hem de reward received olmalı
                bool success = finished && _hasReceivedReward;
                Debug.Log($"[AdManager] Invoking callback with success={success}");
                _onRewardedComplete.Invoke(success);
                _onRewardedComplete = null;
            }
            _hasReceivedReward = false;
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

        // --- Initialization ---
        public void onInitializationFinished(System.Collections.Generic.List<string> errors)
        {
            string output = errors == null ? string.Empty : string.Join(", ", errors);
            Debug.Log($"[AdManager] onInitializationFinished(errors:[{output}])");
        }
    }

    private static AdListener _listener;
    
#if UNITY_ANDROID
    private const string AppKey = "a19618d3fac34820a6ca644fb52240e658699d884f46fd2a";
#elif UNITY_IOS
    private const string AppKey = "";
#else
    private const string AppKey = "";
#endif

    public static void Initialize()
    {
        if (_listener == null)
        {
            _listener = new AdListener();
            
            // Basic settings
            // Appodeal.setTesting(true); // Uncomment if testing is needed
            // Appodeal.setLogLevel(Appodeal.LogLevel.Verbose);

            int adTypes = Appodeal.INTERSTITIAL | Appodeal.REWARDED_VIDEO;
            
            Appodeal.setRewardedVideoCallbacks(_listener);
            Appodeal.setInterstitialCallbacks(_listener);
            
            // Initialize SDK
            Appodeal.initialize(AppKey, adTypes, _listener);
            Debug.Log("[AdManager] Initializing Appodeal with Key: " + AppKey);
        }
    }

    public static void ShowInterstitial(string placement)
    {
        if (Appodeal.isLoaded(Appodeal.INTERSTITIAL))
        {
             Appodeal.show(Appodeal.INTERSTITIAL, placement);
        }
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
            Debug.Log("[AdManager] Rewarded Ad not loaded yet. Attempting cache or wait.");
            // Optional: call cache if needed, though autocheck is usually on
            onComplete?.Invoke(false);
        }
    }
}