using System.Collections;
using TMPro;
using UnityEngine;

public class CollectibleNotifier : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI notificationText;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float displayDuration = 0.5f;

    private Coroutine _currentRoutine;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        // Ensure we have a CanvasGroup for fading
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Start hidden
        _canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        GameplayManager.OnCollectibleNotification += OnNotification;
    }

    private void OnDisable()
    {
        GameplayManager.OnCollectibleNotification -= OnNotification;
    }

    private void OnNotification(string message)
    {
        if (notificationText == null) return;
        
        Show(message);
    }

    public void Show(string message)
    {
        if (_currentRoutine != null) StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        notificationText.text = message;

        // Fade In
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;

        // Wait
        yield return new WaitForSeconds(displayDuration);

        // Fade Out
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = 0f;
    }
}
