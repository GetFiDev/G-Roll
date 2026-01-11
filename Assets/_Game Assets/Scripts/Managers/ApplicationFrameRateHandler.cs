using UnityEngine;

public class ApplicationFrameRateHandler : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Check if it already exists to avoid duplicates in case of domain reloads or other edge cases
        if (FindFirstObjectByType<ApplicationFrameRateHandler>() == null)
        {
            GameObject appFrameRateHandler = new GameObject("ApplicationFrameRateHandler");
            appFrameRateHandler.AddComponent<ApplicationFrameRateHandler>();
            DontDestroyOnLoad(appFrameRateHandler);
        }
    }

    private void Awake()
    {
        Application.targetFrameRate = 120;
        // Optional: Log to confirm it's working
        // Debug.Log($"Application target frame rate set to {Application.targetFrameRate}");
    }
}
