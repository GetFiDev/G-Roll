using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using NetworkingData;
using UnityEngine.UI;

public class AchievementsPanelController : MonoBehaviour
{
    [Header("Top UI")]
    public TMP_Text streakTMP;
    public GameObject fetchingPanel;

    [Header("Streak UI")]
    [SerializeField] private Button streakActionButton;
    [SerializeField] private Image streakActionButtonImage;
    [SerializeField] private TMP_Text streakActionLabel;
    [SerializeField] private Sprite streakClaimableSprite;   // Active look
    [SerializeField] private Sprite streakWaitingSprite;     // Inactive look
    [SerializeField] private GameObject streakCounterFetchingPanel;   // covers streakTMP while fetching
    [SerializeField] private GameObject streakClaimButtonFetchingPanel; // covers button while fetching

    [Header("Grid")]
    public Transform gridRoot;
    public AchievementItemView itemPrefab;

    [Header("Detail")]
    public AchievementDetailPanel detailPanel;

    private AchSnapshot _snapshot;
    private bool _refreshing = false;
    private string _openedTypeId = null;

    private Coroutine _countdownCo;
    private long _nextUtcMidnightMillis;
    private long _serverNowAtFetchMillis;

    void OnEnable()
    {
        var udm = UserDatabaseManager.Instance;
        if (udm != null) udm.OnUserDataSaved += HandleUserDataSaved;

        if (streakActionButton != null)
        {
            streakActionButton.onClick.RemoveAllListeners();
            streakActionButton.onClick.AddListener(() => _ = OnClickStreakClaimAsync());
        }

        _ = RefreshUIAsync();
    }

    void OnDisable()
    {
        var udm = UserDatabaseManager.Instance;
        if (udm != null) udm.OnUserDataSaved -= HandleUserDataSaved;

        if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }
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
        if (streakCounterFetchingPanel) streakCounterFetchingPanel.SetActive(true);
        if (streakClaimButtonFetchingPanel) streakClaimButtonFetchingPanel.SetActive(true);
        try
        {
            var udm = UserDatabaseManager.Instance;
            if (udm?.currentUser != null)
            {
                var data = await udm.LoadUserData();
                if (data != null && streakTMP) streakTMP.text = $"{data.streak}";
            }

            // Fetch streak status from server (idempotent: counts today if needed)
            var streakSnap = await StreakService.FetchAsync();
            if (streakSnap.ok)
            {
                UpdateStreakUI(streakSnap);
            }
            else
            {
                Debug.LogWarning("[AchievementsPanel] Streak fetch failed or empty");
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
            if (streakCounterFetchingPanel) streakCounterFetchingPanel.SetActive(false);
            if (streakClaimButtonFetchingPanel) streakClaimButtonFetchingPanel.SetActive(false);
            _refreshing = false;
        }
    }

    private void UpdateStreakUI(StreakService.StreakSnapshot s)
    {
        // Update counter
        if (streakTMP) streakTMP.text = s.totalDays.ToString();

        // Reset any running countdown
        if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }

        // Cache timing for countdown
        _nextUtcMidnightMillis = s.nextUtcMidnightMillis;
        _serverNowAtFetchMillis = s.serverNowMillis;

        bool claimable = s.claimAvailable && s.unclaimedDays > 0 && s.pendingTotalReward > 0;
        if (claimable)
        {
            // Button shows Claim {amount}
            if (streakActionLabel) streakActionLabel.text = $"Claim {s.pendingTotalReward:0.##}";
            if (streakActionButtonImage && streakClaimableSprite) streakActionButtonImage.sprite = streakClaimableSprite;
            if (streakActionButton) streakActionButton.interactable = true;
        }
        else
        {
            // Waiting mode: show countdown HH:MM:SS to nextUtcMidnight
            if (streakActionButtonImage && streakWaitingSprite) streakActionButtonImage.sprite = streakWaitingSprite;
            if (streakActionButton) streakActionButton.interactable = false;
            _countdownCo = StartCoroutine(CountdownCo());
        }
    }

    private System.Collections.IEnumerator CountdownCo()
    {
        // Estimate server offset at fetch time
        long localNow = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long serverOffset = _serverNowAtFetchMillis - localNow; // can be negative/positive

        while (true)
        {
            long nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + serverOffset;
            long remainMs = _nextUtcMidnightMillis - nowMs;
            if (remainMs < 0) remainMs = 0;

            var t = System.TimeSpan.FromMilliseconds(remainMs);
            string label = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
            if (streakActionLabel) streakActionLabel.text = label;

            if (remainMs == 0) yield break; // stop; next refresh will restart
            yield return new WaitForSeconds(1f);
        }
    }

    private async Task OnClickStreakClaimAsync()
    {
        if (streakActionButton) streakActionButton.interactable = false;
        if (streakClaimButtonFetchingPanel) streakClaimButtonFetchingPanel.SetActive(true);
        try
        {
            var result = await StreakService.ClaimAsync();
            if (!result.ok)
            {
                Debug.LogWarning($"[AchievementsPanel] Claim failed: {result.error}");
                return;
            }

            // Currency/top panel refresh
            if (UITopPanel.Instance != null)
                UITopPanel.Instance.Initialize();

            // Re-fetch streak and refresh button
            var streakSnap = await StreakService.FetchAsync();
            if (streakSnap.ok)
                UpdateStreakUI(streakSnap);
        }
        finally
        {
            if (streakClaimButtonFetchingPanel) streakClaimButtonFetchingPanel.SetActive(false);
            // Interactable state is set by UpdateStreakUI according to claimability
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