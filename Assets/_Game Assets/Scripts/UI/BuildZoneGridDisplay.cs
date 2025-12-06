using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildZoneGridDisplay : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform gridRoot;                  // GridLayoutGroup bu objede olmalı
    [SerializeField] private GameObject fetchingPanel;            // “Loading…” gibi
    [SerializeField] private EquippedItemDisplay itemPrefab;  // Her slot bir EquippedItemDisplay (root'ta Button olmalı)

    [Header("Behavior")]
    [SerializeField] private float refreshDebounceSec = 0.1f;     // event fırtınasında tek refresh
    [SerializeField] private int maxSlots = 6;                    // Oyunda maksimum 6 giyilebilir slot

    private Coroutine _refreshRoutine;
    private bool _pending;

    private void OnEnable()
    {
        if (UserInventoryManager.Instance != null)
            UserInventoryManager.Instance.OnInventoryChanged += OnInventoryChanged;

        _refreshRoutine = StartCoroutine(RefreshCoroutine());
    }

    public void Refresh()
    {
        if (_refreshRoutine != null) StopCoroutine(_refreshRoutine);
        if (isActiveAndEnabled)
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

        // Get equipped list (length 0..N). Biz 6 slota pad edeceğiz.
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

        // Spawn exactly maxSlots buttons
        for (int i = 0; i < maxSlots; i++)
        {
            if (itemPrefab == null || gridRoot == null) break;

            string itemId = (i < equipped.Count) ? equipped[i] : null;
            bool hasItem = !string.IsNullOrEmpty(itemId);

            var display = Instantiate(itemPrefab, gridRoot);
            Button btn = display.GetComponent<Button>();
            if (btn == null)
                btn = display.GetComponentInChildren<Button>(true);
            if (btn != null) btn.onClick.RemoveAllListeners();

            // Visual ve ikon yönetimi EquippedItemDisplay içinde
            // Not: itemId null/empty ise, EquippedItemDisplay boş frame göstermekten sorumlu olmalı.
            if (hasItem)
            {
                display.Bind(itemId);
            }
            else
            {
                // Boş slot; null/id'siz bind. EquippedItemDisplay boş görseli göstermeli.
                display.Bind(null);
            }

            if (btn != null)
            {
                if (hasItem)
                {
                    btn.onClick.AddListener(() => OnEquippedSlotClicked(display));
                }
                else
                {
                    btn.onClick.AddListener(() => OnEmptySlotClicked(display));
                }
            }

            display.gameObject.name = hasItem ? $"Slot_{i}_Item_{itemId}" : $"Slot_{i}_Empty";
        }

        if (fetchingPanel) fetchingPanel.SetActive(false);
        yield return null;
    }

    // --- Click handlers (İÇLERİNİ SEN DOLDURACAKSIN) ---

    private void OnEmptySlotClicked(EquippedItemDisplay clickedEquippedItemDisplay)
    {
        UIBottomPanel.Instance.OnClickShopButton();
        Debug.Log("empty slot clicked");
    }

    private void OnEquippedSlotClicked(EquippedItemDisplay clickedEquippedItemDisplay)
    {
        clickedEquippedItemDisplay.TurnAround();
        Debug.Log("full slot clicked");
    }
}