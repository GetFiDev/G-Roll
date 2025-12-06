using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class BootManager : MonoBehaviour
{
    [SerializeField] private UILoading uiLoading;
    [SerializeField] private VideoPlayer introVideo;
    [SerializeField] private CanvasGroup videoCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.75f;
    
    private static bool _appReady = false;
    private bool _videoFinished = false;
    
    public static void ContinueToGameScene() => _appReady = true;

    private void Awake()
    {
        if (introVideo != null)
        {
            introVideo.loopPointReached += OnVideoFinished;
        }
        else
        {
            // Eğer video atanmamışsa, video zaten "bitti" kabul edilir
            _videoFinished = true;
        }
    }

    private void OnDestroy()
    {
        if (introVideo != null)
        {
            introVideo.loopPointReached -= OnVideoFinished;
        }
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        _videoFinished = true;
    }

    private IEnumerator Start()
    {
        // Video başlasın
        if (introVideo != null && !introVideo.isPlaying)
        {
            introVideo.Play();
        }
        
        // Arkada sahne yüklenmeye başlasın
        var asyncLoad = SceneManager.LoadSceneAsync("Game Scene");
        asyncLoad.allowSceneActivation = false;

        var currentProgress = 0f;

        // Hem yükleme %99'a gelsin, hem appReady true olsun, hem de video bitsin
        while (currentProgress < 0.99f || !_appReady || !_videoFinished)
        {
            var progress = Mathf.Clamp01(asyncLoad.progress + .1f);
            currentProgress = Mathf.Lerp(currentProgress, progress, 5 * Time.deltaTime);
            
            uiLoading.SetProgress(currentProgress);

            yield return null;
        }
        
        // Video yavaşça fade out olsun
        if (videoCanvasGroup != null && fadeOutDuration > 0f)
        {
            float t = 0f;
            float startAlpha = videoCanvasGroup.alpha;

            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                float normalized = Mathf.Clamp01(t / fadeOutDuration);
                float alpha = Mathf.Lerp(startAlpha, 0f, normalized);
                videoCanvasGroup.alpha = alpha;
                yield return null;
            }

            videoCanvasGroup.alpha = 0f;
        }

        if (introVideo != null && introVideo.isPlaying)
        {
            introVideo.Stop();
        }
        
        // Son bir küçük bekleme
        yield return new WaitForSeconds(.2f);
        
        asyncLoad.allowSceneActivation = true;
    }
}
