using System;
using AppodealAds.Unity.Api;
using AppodealAds.Unity.Common;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using VContainer;

namespace GRoll.Infrastructure.Services
{
    /// <summary>
    /// Ad service implementation using Appodeal SDK.
    /// Manages rewarded and interstitial ads.
    /// </summary>
    public class AdService : IAdService, IRewardedVideoAdListener, IInterstitialAdListener
    {
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;
        private readonly IUserProfileService _profileService;

        private bool _isRewardedAdReady;
        private bool _isInterstitialReady;
        private bool _areAdsDisabled;
        private bool _isInitialized;

        // Appodeal app keys
#if UNITY_ANDROID
        private const string AppodealAppKey = "YOUR_ANDROID_APPODEAL_KEY"; // TODO: Replace with actual key
#elif UNITY_IOS
        private const string AppodealAppKey = "YOUR_IOS_APPODEAL_KEY"; // TODO: Replace with actual key
#else
        private const string AppodealAppKey = "YOUR_APPODEAL_KEY";
#endif

        public bool IsRewardedAdReady => _isRewardedAdReady && !_areAdsDisabled;
        public bool IsInterstitialReady => _isInterstitialReady && !_areAdsDisabled;
        public bool AreAdsDisabled => _areAdsDisabled;

        public event Action<AdReadyStateChangedEventArgs> OnAdReadyStateChanged;

        // Callbacks for current ad
        private UniTaskCompletionSource<AdResult> _currentAdCompletionSource;
        private string _currentPlacement;
        private bool _rewardedVideoFinished;

        [Inject]
        public AdService(
            IMessageBus messageBus,
            IGRollLogger logger,
            IUserProfileService profileService)
        {
            _messageBus = messageBus;
            _logger = logger;
            _profileService = profileService;

            // Check if user has elite pass (ads disabled)
            CheckElitePassStatus();

            // Initialize Appodeal SDK
            InitializeAppodeal();
        }

        private void CheckElitePassStatus()
        {
            if (_profileService.CurrentProfile?.HasElitePass == true)
            {
                _areAdsDisabled = true;
                _logger.Log("[AdService] Ads disabled (Elite Pass)");
            }
        }

        private void InitializeAppodeal()
        {
            if (_isInitialized) return;

            _logger.Log("[AdService] Initializing Appodeal SDK...");

            // Set callbacks
            Appodeal.setRewardedVideoCallbacks(this);
            Appodeal.setInterstitialCallbacks(this);

            // Configure SDK
            Appodeal.setTesting(false);
            Appodeal.setUseSafeArea(true);
            Appodeal.muteVideosIfCallsMuted(true);

            // Initialize with rewarded video and interstitial ad types
            int adTypes = Appodeal.REWARDED_VIDEO | Appodeal.INTERSTITIAL;
            Appodeal.initialize(AppodealAppKey, adTypes);

            _isInitialized = true;
            _logger.Log("[AdService] Appodeal SDK initialized");
        }

        #region IAdService Implementation

        public async UniTask<AdResult> ShowRewardedAdAsync(string placement)
        {
            _logger.Log($"[AdService] Showing rewarded ad: {placement}");

            if (_areAdsDisabled)
            {
                _logger.Log("[AdService] Ads disabled, granting reward directly");
                return AdResult.AdsDisabled();
            }

            if (!Appodeal.isLoaded(Appodeal.REWARDED_VIDEO))
            {
                _logger.LogWarning("[AdService] Rewarded ad not ready");
                return AdResult.NotReady();
            }

            _currentAdCompletionSource = new UniTaskCompletionSource<AdResult>();
            _currentPlacement = placement;
            _rewardedVideoFinished = false;

            // Show the ad
            Appodeal.show(Appodeal.REWARDED_VIDEO, placement);

            return await _currentAdCompletionSource.Task;
        }

        public async UniTask<AdResult> ShowInterstitialAsync(string placement)
        {
            _logger.Log($"[AdService] Showing interstitial: {placement}");

            if (_areAdsDisabled)
            {
                return AdResult.AdsDisabled();
            }

            if (!Appodeal.isLoaded(Appodeal.INTERSTITIAL))
            {
                _logger.LogWarning("[AdService] Interstitial not ready");
                return AdResult.NotReady();
            }

            _currentAdCompletionSource = new UniTaskCompletionSource<AdResult>();
            _currentPlacement = placement;

            // Show the ad
            Appodeal.show(Appodeal.INTERSTITIAL, placement);

            return await _currentAdCompletionSource.Task;
        }

        public void DisableAds()
        {
            _areAdsDisabled = true;
            _logger.Log("[AdService] Ads disabled");
            _messageBus.Publish(new AdsDisabledMessage());
        }

        #endregion

        #region IRewardedVideoAdListener Implementation

        public void onRewardedVideoLoaded(bool isPrecache)
        {
            _logger.Log($"[AdService] Rewarded video loaded. Precache: {isPrecache}");
            _isRewardedAdReady = true;
            NotifyAdStateChanged(AdType.Rewarded, true);
        }

        public void onRewardedVideoFailedToLoad()
        {
            _logger.LogWarning("[AdService] Rewarded video failed to load");
            _isRewardedAdReady = false;
            NotifyAdStateChanged(AdType.Rewarded, false);
        }

        public void onRewardedVideoShown()
        {
            _logger.Log("[AdService] Rewarded video shown");
        }

        public void onRewardedVideoShowFailed()
        {
            _logger.LogWarning("[AdService] Rewarded video show failed");
            _currentAdCompletionSource?.TrySetResult(AdResult.Failed("Failed to show rewarded video"));
            _currentAdCompletionSource = null;
        }

        public void onRewardedVideoClosed(bool finished)
        {
            _logger.Log($"[AdService] Rewarded video closed. Finished: {finished}");

            if (_currentAdCompletionSource != null)
            {
                if (_rewardedVideoFinished || finished)
                {
                    _currentAdCompletionSource.TrySetResult(AdResult.Rewarded());
                }
                else
                {
                    _currentAdCompletionSource.TrySetResult(AdResult.Skipped());
                }
                _currentAdCompletionSource = null;
            }

            // Cache next ad
            _isRewardedAdReady = false;
            NotifyAdStateChanged(AdType.Rewarded, false);
            Appodeal.cache(Appodeal.REWARDED_VIDEO);
        }

        public void onRewardedVideoFinished(double amount, string name)
        {
            _logger.Log($"[AdService] Rewarded video finished. Amount: {amount}, Currency: {name}");
            _rewardedVideoFinished = true;
        }

        public void onRewardedVideoExpired()
        {
            _logger.Log("[AdService] Rewarded video expired");
            _isRewardedAdReady = false;
            NotifyAdStateChanged(AdType.Rewarded, false);
            Appodeal.cache(Appodeal.REWARDED_VIDEO);
        }

        public void onRewardedVideoClicked()
        {
            _logger.Log("[AdService] Rewarded video clicked");
        }

        #endregion

        #region IInterstitialAdListener Implementation

        public void onInterstitialLoaded(bool isPrecache)
        {
            _logger.Log($"[AdService] Interstitial loaded. Precache: {isPrecache}");
            _isInterstitialReady = true;
            NotifyAdStateChanged(AdType.Interstitial, true);
        }

        public void onInterstitialFailedToLoad()
        {
            _logger.LogWarning("[AdService] Interstitial failed to load");
            _isInterstitialReady = false;
            NotifyAdStateChanged(AdType.Interstitial, false);
        }

        public void onInterstitialShown()
        {
            _logger.Log("[AdService] Interstitial shown");
        }

        public void onInterstitialShowFailed()
        {
            _logger.LogWarning("[AdService] Interstitial show failed");
            _currentAdCompletionSource?.TrySetResult(AdResult.Failed("Failed to show interstitial"));
            _currentAdCompletionSource = null;
        }

        public void onInterstitialClosed()
        {
            _logger.Log("[AdService] Interstitial closed");
            _currentAdCompletionSource?.TrySetResult(AdResult.Rewarded()); // Success for interstitial
            _currentAdCompletionSource = null;

            // Cache next ad
            _isInterstitialReady = false;
            NotifyAdStateChanged(AdType.Interstitial, false);
            Appodeal.cache(Appodeal.INTERSTITIAL);
        }

        public void onInterstitialClicked()
        {
            _logger.Log("[AdService] Interstitial clicked");
        }

        public void onInterstitialExpired()
        {
            _logger.Log("[AdService] Interstitial expired");
            _isInterstitialReady = false;
            NotifyAdStateChanged(AdType.Interstitial, false);
            Appodeal.cache(Appodeal.INTERSTITIAL);
        }

        #endregion

        private void NotifyAdStateChanged(AdType type, bool isReady)
        {
            OnAdReadyStateChanged?.Invoke(new AdReadyStateChangedEventArgs
            {
                AdType = type,
                IsReady = isReady
            });
        }
    }

    /// <summary>
    /// Message when ads are disabled
    /// </summary>
    public readonly struct AdsDisabledMessage : GRoll.Core.Events.IMessage
    {
    }
}
