using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootManager : MonoBehaviour
{
    [SerializeField] private UILoading uiLoading;
    
    private static bool _appReady = false;
    
    public static void ContinueToGameScene() => _appReady = true;

    private IEnumerator Start()
    {
        var asyncLoad = SceneManager.LoadSceneAsync("Game Scene");
        asyncLoad.allowSceneActivation = false;

        var currentProgress = 0f;

        while (currentProgress < 0.99f || !_appReady)
        {
            var progress = Mathf.Clamp01(asyncLoad.progress + .1f);
            currentProgress = Mathf.Lerp(currentProgress, progress, 5 * Time.deltaTime);
            
            uiLoading.SetProgress(currentProgress);

            yield return null;
        }
        
        yield return new WaitForSeconds(.2f);
        
        asyncLoad.allowSceneActivation = true;
    }
}
