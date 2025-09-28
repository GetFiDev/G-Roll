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
}