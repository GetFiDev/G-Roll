using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Individual task list item that displays task info and handles go button click.
/// </summary>
public class UITaskListElement : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text taskNameText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button goButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image iconImage;

    [Header("Settings")]
    [SerializeField] private float processingAlpha = 0.5f;

    private TaskService.TaskData _taskData;
    private Action _onCompleted;
    private bool _isProcessing = false;
    private bool _awaitingFocusReturn = false;

    /// <summary>
    /// Binds task data to this UI element.
    /// </summary>
    public void Bind(TaskService.TaskData task, Action onCompleted)
    {
        _taskData = task;
        _onCompleted = onCompleted;

        // Update UI
        if (taskNameText) taskNameText.text = task.taskDisplayName;
        if (rewardText) rewardText.text = $"+{task.taskCurrencyReward:F1}";

        // Reset state
        if (canvasGroup) canvasGroup.alpha = 1f;
        _isProcessing = false;
        _awaitingFocusReturn = false;

        // Setup button
        if (goButton)
        {
            goButton.onClick.RemoveAllListeners();
            goButton.onClick.AddListener(() => _ = OnGoButtonClick());
        }

        // Load icon if URL provided
        if (!string.IsNullOrEmpty(task.taskIconUrl) && iconImage != null)
        {
            _ = LoadIconAsync(task.taskIconUrl);
        }
    }

    private async Task OnGoButtonClick()
    {
        if (_isProcessing) return;
        _isProcessing = true;

        // Show processing state
        if (canvasGroup) canvasGroup.alpha = processingAlpha;
        if (goButton) goButton.interactable = false;

        try
        {
            // Call server to complete task and grant reward BEFORE opening URL
            var result = await TaskService.CompleteTaskAsync(_taskData.taskId);

            if (result.ok)
            {
                // Open external URL
                if (!string.IsNullOrEmpty(_taskData.taskDirectionUrl))
                {
                    _awaitingFocusReturn = true;
                    Application.OpenURL(_taskData.taskDirectionUrl);
                }

                // Refresh top panel currency display
                if (UITopPanel.Instance != null)
                {
                    UITopPanel.Instance.Initialize();
                }

                // Notify parent to remove this element
                _onCompleted?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[UITaskListElement] Task completion failed: {result.error}");
                // Reset UI on failure
                if (canvasGroup) canvasGroup.alpha = 1f;
                if (goButton) goButton.interactable = true;
                _isProcessing = false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UITaskListElement] Error completing task: {ex.Message}");
            // Reset UI on error
            if (canvasGroup) canvasGroup.alpha = 1f;
            if (goButton) goButton.interactable = true;
            _isProcessing = false;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _awaitingFocusReturn)
        {
            _awaitingFocusReturn = false;
            
            // Task was already completed before opening URL
            // The element should already be removed by onCompleted callback
            // But if still present, ensure visual state shows completion
            if (canvasGroup) canvasGroup.alpha = 1f;
        }
    }

    private async Task LoadIconAsync(string url)
    {
        try
        {
            using (var www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                var op = www.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(www);
                    if (iconImage != null && texture != null)
                    {
                        // Create sprite from texture for UI Image
                        var sprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f)
                        );
                        iconImage.sprite = sprite;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UITaskListElement] Failed to load icon: {ex.Message}");
        }
    }
}
