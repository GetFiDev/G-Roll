using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Orchestrates the rewarded tasks list within the achievement/task panel.
/// Fetches available tasks from server and populates the UI.
/// </summary>
public class UITaskPanel : MonoBehaviour
{
    [Header("Task List")]
    [SerializeField] private Transform taskListContainer;
    [SerializeField] private UITaskListElement taskItemPrefab;
    
    [Header("Loading")]
    [SerializeField] private GameObject loadingPanel;
    
    [Header("Visibility")]
    [Tooltip("This object will be hidden if there are no tasks available")]
    [SerializeField] private GameObject tasksContainerRoot;

    private List<UITaskListElement> _activeTaskElements = new List<UITaskListElement>();
    private bool _isLoading = false;

    private void OnEnable()
    {
        _ = RefreshTaskListAsync();
    }

    /// <summary>
    /// Fetches available tasks from server and populates the list.
    /// </summary>
    public async Task RefreshTaskListAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        // Show loading
        if (loadingPanel) loadingPanel.SetActive(true);

        try
        {
            // Clear existing items
            ClearTaskList();

            // Fetch from server
            var response = await TaskService.FetchAvailableTasksAsync();

            if (!response.ok || response.tasks == null)
            {
                Debug.LogWarning($"[UITaskPanel] Failed to fetch tasks: {response.error}");
                // Hide container if fetch failed
                if (tasksContainerRoot) tasksContainerRoot.SetActive(false);
                return;
            }

            // Hide container if no tasks available
            if (response.tasks.Count == 0)
            {
                if (tasksContainerRoot) tasksContainerRoot.SetActive(false);
                return;
            }

            // Show container since we have tasks
            if (tasksContainerRoot) tasksContainerRoot.SetActive(true);

            // Populate list
            foreach (var taskData in response.tasks)
            {
                if (taskItemPrefab == null || taskListContainer == null)
                {
                    Debug.LogError("[UITaskPanel] taskItemPrefab or taskListContainer not assigned");
                    break;
                }

                var element = Instantiate(taskItemPrefab, taskListContainer);
                element.name = $"TaskItem_{taskData.taskId}";
                
                // Bind with completion callback
                element.Bind(taskData, () => OnTaskCompleted(element));
                
                _activeTaskElements.Add(element);
            }
        }
        finally
        {
            _isLoading = false;
            if (loadingPanel) loadingPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Called when a task is completed. Removes the element from the list.
    /// </summary>
    private void OnTaskCompleted(UITaskListElement element)
    {
        if (element == null) return;

        _activeTaskElements.Remove(element);
        
        // Destroy the element after a short delay to allow any animations
        Destroy(element.gameObject, 0.1f);
    }

    /// <summary>
    /// Clears all task items from the list.
    /// </summary>
    private void ClearTaskList()
    {
        foreach (var element in _activeTaskElements)
        {
            if (element != null)
                Destroy(element.gameObject);
        }
        _activeTaskElements.Clear();

        // Also destroy any orphaned children
        if (taskListContainer != null)
        {
            foreach (Transform child in taskListContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
