using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using NetworkingData;

public class AchievementsPanelController : MonoBehaviour
{
    [Header("Top UI")]
    public TMP_Text streakTMP;
    public GameObject fetchingPanel;

    [Header("Grid")]
    public Transform gridRoot;
    public AchievementItemView itemPrefab;

    [Header("Detail")]
    public AchievementDetailPanel detailPanel;

    private AchSnapshot _snapshot;
    private bool _refreshing = false;
    private string _openedTypeId = null;

    void OnEnable()
    {
        var udm = UserDatabaseManager.Instance;
        if (udm != null) udm.OnUserDataSaved += HandleUserDataSaved;

        _ = RefreshUIAsync();
    }

    void OnDisable()
    {
        var udm = UserDatabaseManager.Instance;
        if (udm != null) udm.OnUserDataSaved -= HandleUserDataSaved;
    }

    void HandleUserDataSaved(UserData u)
    {
        if (streakTMP) streakTMP.text = $"{u.streak}";
    }

    public async Task RefreshUIAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        if (fetchingPanel) fetchingPanel.SetActive(true);
        try
        {
            var udm = UserDatabaseManager.Instance;
            if (udm?.currentUser != null)
            {
                var data = await udm.LoadUserData();
                if (data != null && streakTMP) streakTMP.text = $"{data.streak}";
            }

            _snapshot = await AchievementService.GetSnapshotAsync();

            // guard
            if (_snapshot == null || _snapshot.defs == null || _snapshot.defs.Count == 0)
            {
                Debug.LogWarning("[AchievementsPanel] No defs from server.");
                return;
            }
            if (!gridRoot || !itemPrefab)
            {
                Debug.LogError("[AchievementsPanel] gridRoot/itemPrefab not assigned.");
                return;
            }

            // Clear grid
            foreach (Transform c in gridRoot) Destroy(c.gameObject);

            var byType = new Dictionary<string, AchState>();
            foreach (var s in _snapshot.states) byType[s.typeId] = s;

            foreach (var def in _snapshot.defs)
            {
                var state = byType.ContainsKey(def.typeId)
                    ? byType[def.typeId]
                    : new AchState { typeId = def.typeId, level = 0, progress = 0, claimedLevels = new List<int>() };

                var item = Instantiate(itemPrefab, gridRoot);
                item.name = $"AchItem_{def.typeId}"; // aid in debugging hierarchy
                item.Bind(def, state, OnItemClicked);
            }
        }
        finally
        {
            if (fetchingPanel) fetchingPanel.SetActive(false);
            _refreshing = false;
        }
    }

    void OnItemClicked(AchDef def, AchState state)
    {
        if (detailPanel == null)
        {
            Debug.LogError("[AchievementsPanel] detailPanel is not assigned.");
            return;
        }

        _openedTypeId = def.typeId; // remember which detail is open

        detailPanel.Open(def, state, async () =>
        {
            // 1) Full refresh (like restarting the game’s achievements UI)
            await RefreshUIAsync();

            // 2) Re-open the same achievement with the latest snapshot so UI never stays in a stale state
            if (!string.IsNullOrEmpty(_openedTypeId))
            {
                var newDef = _snapshot?.defs?.Find(d => d.typeId == _openedTypeId);
                var newSt  = _snapshot?.states?.Find(s => s.typeId == _openedTypeId)
                             ?? new AchState { typeId = _openedTypeId, level = 0, progress = 0, claimedLevels = new List<int>() };

                if (newDef != null)
                {
                    detailPanel.Open(newDef, newSt, async () => { await RefreshUIAsync(); });
                }
            }
        });
    }
}