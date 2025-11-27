using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Sych.ShareAssets.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UINewHighScorePanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Button shareButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panelContent; // To hide during screenshot if needed

    [Header("Share Settings")]
    [SerializeField] private string shareMessage = "I just beat my high score in G-Roll! Can you beat me?";
    [SerializeField] private string screenshotFilename = "high_score.png";

    private void Awake()
    {
        if (shareButton) shareButton.onClick.AddListener(OnShareClicked);
        if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        if (shareButton) shareButton.onClick.RemoveListener(OnShareClicked);
        if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    public void Show(double score)
    {
        if (scoreText) scoreText.text = score.ToString("N0");
        gameObject.SetActive(true);
        
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeInRoutine());
        }
        else
        {
            Debug.LogWarning("[UINewHighScorePanel] GameObject is not active in hierarchy (Parent might be disabled). Skipping animation.");
            canvasGroup.alpha = 1f; // Ensure visible if it becomes active later
        }
    }

    private IEnumerator FadeInRoutine()
    {
        canvasGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public event Action OnClosed;

    private void OnCloseClicked()
    {
        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }

    private void OnShareClicked()
    {
        StartCoroutine(ShareScreenshotRoutine());
    }

    private IEnumerator ShareScreenshotRoutine()
    {
        // Optional: Hide UI for screenshot
        // if (panelContent) panelContent.SetActive(false);
        // yield return new WaitForEndOfFrame();

        // Capture Screenshot
        string path = Path.Combine(Application.persistentDataPath, screenshotFilename);
        
        // Clean up old file
        if (File.Exists(path)) File.Delete(path);

        // Wait for end of frame to ensure UI is rendered (if needed, though we are in coroutine)
        yield return new WaitForEndOfFrame();

        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        
        if (texture != null)
        {
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            
            // Cleanup texture memory
            Destroy(texture);
        }
        else
        {
            Debug.LogError("Failed to capture screenshot texture.");
            yield break;
        }

        // Wait for file to be written (usually instant with WriteAllBytes, but good practice)
        float timeout = 2f;
        while (!File.Exists(path) && timeout > 0)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Optional: Show UI again
        // if (panelContent) panelContent.SetActive(true);

        if (!File.Exists(path))
        {
            Debug.LogError("Failed to capture screenshot.");
            yield break;
        }

        // Share
        var items = new List<string>();
        items.Add(shareMessage);
        items.Add(path);

#pragma warning disable CS0618
        Share.Items(items, success =>
        {
            Debug.Log($"Share result: {success}");
        });
#pragma warning restore CS0618
    }
}
