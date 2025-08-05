#if !DISABLE_SRDEBUGGER
using UnityEngine;

// ReSharper disable once InconsistentNaming
public abstract class SRDebuggerInitializer
{
    private const string NotStore = "Non Store Install";
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void RunTimeInitialize()
    {
        var installerName = Application.installerName;
        var storeName = installerName switch
        {
            "com.android.vending" => "Google Play",
            "com.amazon.venezia" => "Amazon Appstore",
            "com.sec.android.app.samsungapps" => "Samsung Galaxy Apps",
            "com.apple.appstore" => "Apple App Store",
            "com.microsoft.windowsstore" => "Windows Store",
            "com.nvidia.nvgamestore" => "Nvidia",
            "com.xiaomi.market" => "Xiaomi",
            "com.oppo.market" => "Oppo",
            "com.huawei.appmarket" => "Huawei",
            "com.lenovo.leos.appstore" => "Lenovo",
            "com.asus.appmarket" => "Asus",
            "com.zing.zalo" => "Zalo",
            _ => NotStore
        };

        Debug.Log("Installed From : " + storeName);
        
        if (storeName != NotStore)
            return;

        SRDebug.Init();
        SRDebug.Instance.AddOptionContainer(GameSROptions.Instance);
    }
}
#endif