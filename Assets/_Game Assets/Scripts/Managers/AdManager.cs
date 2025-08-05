using System;
using System.Collections;
using UnityEngine;

public abstract class AdManager
{
    private static GameSettingsData _gameSettingsData;
    
    private static float _lastInterstitialRequestTime;
    private static float _nextInterstitialTime;

    public static void Initialize()
    {
        _gameSettingsData = GameSettingsData.Instance;

        _nextInterstitialTime = Time.time + _gameSettingsData.initialInterstitialDuration;
    }
    
    public static void ShowInterstitial(string placement)
    {
        if (Time.time < _lastInterstitialRequestTime)
            return;

        if (Time.time < _nextInterstitialTime)
            return;

        placement += "_interstitial";

        //TODO: REQUEST AD HERE

        _lastInterstitialRequestTime = Time.time + 1;
        _nextInterstitialTime = Time.time + _gameSettingsData.interstitialInterval;
    }

    public static void ShowRewarded(string placement, Action<bool> onComplete)
    {
        placement += "_rewarded";

#if UNITY_EDITOR
        onComplete?.Invoke(true);
#else
        //TODO: REQUEST AD HERE
#endif
    }

    public static void ActivateBanner()
    {
        GameManager.Instance.StartCoroutine(BannerActivationCheck());
    }

    private static IEnumerator BannerActivationCheck()
    {
        var checkInterval = new WaitForSeconds(.5f);

        //TODO: REQUEST AD HERE
        
        yield return null;
    }
}