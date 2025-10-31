using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildZoneGridDisplay : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform gridRoot;                  // GridLayoutGroup bu objede olmalı
    [SerializeField] private GameObject fetchingPanel;            // “Loading…” gibi
    [SerializeField] private GameObject emptyStatePanel;          // (opsiyonel) hiç equipped yoksa
    [SerializeField] private EquippedItemDisplay itemPrefab;      // Spawn edilecek kart

    [Header("Behavior")]
    [SerializeField] private float refreshDebounceSec = 0.1f;     // event fırtınasında tek refresh

    private Coroutine _refreshRoutine;
    private bool _pending;

    private void OnEnable()
    {
        if (UserInventoryManager.Instance != null)
            UserInventoryManager.Instance.OnInventoryChanged += OnInventoryChanged;

        _refreshRoutine = StartCoroutine(RefreshCoroutine());
    }

    private void OnDisable()
    {
        if (UserInventoryManager.Instance != null)
            UserInventoryManager.Instance.OnInventoryChanged -= OnInventoryChanged;

        if (_refreshRoutine != null)
        {
            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }
    }

    private void OnInventoryChanged()
    {
        // Debounce
        if (!_pending)
        {
            _pending = true;
            StartCoroutine(DebouncedRefresh());
        }
    }

    private IEnumerator DebouncedRefresh()
    {
        yield return new WaitForSeconds(refreshDebounceSec);
        _pending = false;
        if (isActiveAndEnabled)
            yield return RefreshCoroutine();
    }

    private IEnumerator RefreshCoroutine()
    {
        if (fetchingPanel) fetchingPanel.SetActive(true);
        if (emptyStatePanel) emptyStatePanel.SetActive(false);

        // Ensure manager ready
        var inv = UserInventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[BuildZoneGridDisplay] UserInventoryManager.Instance is null");
            if (fetchingPanel) fetchingPanel.SetActive(false);
            yield break;
        }

        if (!inv.IsInitialized)
        {
            var initTask = inv.InitializeAsync();
            while (!initTask.IsCompleted) yield return null;
        }

        // Get equipped list
        List<string> equipped = inv.GetEquippedItemIds() ?? new List<string>();

        // Clear grid
        if (gridRoot != null)
        {
            for (int i = gridRoot.childCount - 1; i >= 0; i--)
            {
                var c = gridRoot.GetChild(i);
                Destroy(c.gameObject);
            }
        }

        // Spawn
        if (equipped.Count == 0)
        {
            if (emptyStatePanel) emptyStatePanel.SetActive(true);
        }
        else
        {
            foreach (var id in equipped)
            {
                if (itemPrefab == null || gridRoot == null) break;
                var go = Instantiate(itemPrefab, gridRoot);
                go.Bind(id); // sadece gösterim, tıklama yok
            }
        }

        if (fetchingPanel) fetchingPanel.SetActive(false);
        yield return null;
    }
}