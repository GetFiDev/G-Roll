using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIGameplayLoading : MonoBehaviour
{
    [Header("UI References")]
    public Slider loadingBar;
    public Text loadingText;

    void Initialize()
    {
        StartCoroutine(LoadGameplaySceneAsync());
    }

    System.Collections.IEnumerator LoadGameplaySceneAsync()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync("gameplayscene");
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            if (loadingBar != null)
                loadingBar.value = progress;
            if (loadingText != null)
                loadingText.text = $"Loading... {Mathf.RoundToInt(progress * 100)}%";

            // Scene activation at 90% progress
            if (operation.progress >= 0.9f)
            {
                if (loadingBar != null)
                    loadingBar.value = 1f;
                if (loadingText != null)
                    loadingText.text = "Press any key to continue";
                if (Input.anyKeyDown)
                    operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}