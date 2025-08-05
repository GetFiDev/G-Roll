using System;
using UnityEngine;

// ReSharper disable InconsistentNaming
public class AppManager : MonoSingleton<AppManager>
{
    public Action<bool> OnApplicationPauseListener;
    
    protected override void Init()
    {
        DontDestroyOnLoad(gameObject);
        
        //UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
    }

    private void Start()
    {
        ReviewManager.Initialize();
        HapticManager.Initialize();
        AnalyticsManager.Initialize();
        NotificationManager.Initialize();
        AdManager.Initialize();
        
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Application.targetFrameRate = 60;
        }
        
        BootManager.ContinueToGameScene();
        
        // Facebook.Unity.FB.Init(FBInitCallback);
    }
    
    private void OnApplicationPause(bool paused)
    {
        OnApplicationPauseListener?.Invoke(paused);
    }

    // // ReSharper disable once InconsistentNaming
    // private void FBInitCallback()
    // {
    //     if (Facebook.Unity.FB.IsInitialized)
    //     {
    //         LogManager.Log("Facebook Initialized");
    //         
    //         Facebook.Unity.FB.ActivateApp();
    //     }
    //     else
    //     {
    //         LogManager.LogError("Facebook Initialized Failed", this);
    //     }
    // }
    //
    // private void OnApplicationPause(bool paused)
    // {
    //     if (paused)
    //         return;
    //     
    //     if (Facebook.Unity.FB.IsInitialized)
    //     {
    //         Facebook.Unity.FB.ActivateApp();
    //     }
    // }
}