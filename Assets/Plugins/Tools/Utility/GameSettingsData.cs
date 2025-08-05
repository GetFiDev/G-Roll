using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "New Game Settings Data", menuName = "Scriptable Object / Game Settings Data")]
public class GameSettingsData : ScriptableObject
{
    private static GameSettingsData _instance;

    public static GameSettingsData Instance =>
        _instance ??= Resources.Load<GameSettingsData>("Game Resources/Game Settings Data");
    
    [Title("Game Settings", "Contains Game Settings Data", TitleAlignments.Centered, Bold = true)]
    private const float Time = 1f;
    
    [Header("Touch Settings")]
    public bool ignoreUITouches = true;
    
    [Header("Skip Main Menu UI")]
    public bool skipReadyState = true;

    [Header("Ad Settings")] 
    public int initialInterstitialDuration;
    public int interstitialInterval;
}