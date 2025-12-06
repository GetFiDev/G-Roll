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
    [SerializeField] private TMP_Text streakTimerText;
    [SerializeField] private TMP_Text streakClaimValueText;
    [SerializeField] private Sprite streakClaimableSprite;   // Active look
    [SerializeField] private Sprite streakWaitingSprite;     // Inactive look
    [SerializeField] private GameObject streakClaimButtonFetchingPanel; // covers button while fetching
    [SerializeField] private GameObject streakButtonCoinIcon; // Coin icon for claim button

    [Header("Grid")]
    public Transform gridRoot;
    public AchievementItemView itemPrefab;

    [Header("Social Links")]
    public string telegramLink;
    public string xLink;

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
        // if (streakTMP) streakTMP.text = $"{u.streak}"; // Disabled to allow custom animation control
    }

    public async Task RefreshUIAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        if (fetchingPanel) fetchingPanel.SetActive(true);
        // if (streakCounterFetchingPanel) streakCounterFetchingPanel.SetActive(true);
        if (streakTMP) 
        {
            streakTMP.text = "0";
            streakTMP.alpha = 1f; 
        }
        StartCoroutine(PulseStreakCounter());

        if (streakClaimButtonFetchingPanel) streakClaimButtonFetchingPanel.SetActive(true);
        try
        {
            var udm = UserDatabaseManager.Instance;
            if (udm?.currentUser != null)
            {
                var data = await udm.LoadUserData();
                // if (data != null && streakTMP) streakTMP.text = $"{data.streak}";
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

            var renderList = new List<(AchDef def, AchState state, AchievementItemView.AchievementVisualState visualState)>();
            
            foreach (var def in _snapshot.defs)
            {
                var state = byType.ContainsKey(def.typeId)
                    ? byType[def.typeId]
                    : new AchState { typeId = def.typeId, level = 0, progress = 0, claimedLevels = new List<int>() };

                // Determine Visual State
                AchievementItemView.AchievementVisualState vState;
                
                // Logic Fix: Check if any level <= state.level is NOT in claimedLevels
                bool isClaimable = false;
                if (state.level > 0)
                {
                    var claimedSet = new HashSet<int>(state.claimedLevels ?? new List<int>());
                    for (int i = 1; i <= state.level; i++)
                    {
                        if (!claimedSet.Contains(i))
                        {
                            isClaimable = true;
                            break;
                        }
                    }
                }

                bool isMaxLevelReached = state.level >= def.maxLevel;
                bool isCompleted = isMaxLevelReached && !isClaimable;
                bool isInProgress = !isCompleted && !isClaimable && state.progress > 0;

                if (isClaimable)
                    vState = AchievementItemView.AchievementVisualState.Claimable;
                else if (isInProgress)
                    vState = AchievementItemView.AchievementVisualState.InProgress;
                else if (isCompleted)
                    vState = AchievementItemView.AchievementVisualState.Completed;
                else
                    vState = AchievementItemView.AchievementVisualState.Locked; // No progress

                renderList.Add((def, state, vState));
            }

            // Sort: Claimable (0) < InProgress (1) < Locked/Completed (2/3)
            // Secondary sort: def.order
            renderList.Sort((a, b) =>
            {
                int stateA = GetStatePriority(a.visualState);
                int stateB = GetStatePriority(b.visualState);
                if (stateA != stateB) return stateA.CompareTo(stateB);
                return a.def.order.CompareTo(b.def.order);
            });

            foreach (var itemData in renderList)
            {
                var item = Instantiate(itemPrefab, gridRoot);
                item.name = $"AchItem_{itemData.def.typeId}";
                item.Bind(itemData.def, itemData.state, itemData.visualState, OnItemClicked);
            }
        }
        finally
        {
            if (fetchingPanel) fetchingPanel.SetActive(false);
            // if (streakCounterFetchingPanel) streakCounterFetchingPanel.SetActive(false);
            if (streakClaimButtonFetchingPanel) streakClaimButtonFetchingPanel.SetActive(false);
            _refreshing = false;
        }
    }

    private System.Collections.IEnumerator PulseStreakCounter()
    {
        if (!streakTMP) yield break;
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 2f; 
            float a = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(t) + 1f) * 3f);
            streakTMP.alpha = a;
            yield return null;
        }
    }


    private void UpdateStreakUI(StreakService.StreakSnapshot s)
    {
        // Update counter
        StopAllCoroutines(); // Stop pulse or countdown
        if (streakTMP) streakTMP.alpha = 1f;
        StartCoroutine(CountUpStreak(s.totalDays, 0.5f));


        // Reset any running countdown
        if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }

        // Cache timing for countdown
        _nextUtcMidnightMillis = s.nextUtcMidnightMillis;
        _serverNowAtFetchMillis = s.serverNowMillis;

        bool claimable = s.claimAvailable && s.unclaimedDays > 0 && s.pendingTotalReward > 0;
        
        if (streakButtonCoinIcon) streakButtonCoinIcon.SetActive(claimable);

        if (claimable)
        {
            // Button shows Claim {amount}
            if (streakClaimValueText)
            {
                streakClaimValueText.text = $"{s.pendingTotalReward:F2}";
                streakClaimValueText.gameObject.SetActive(true);
            }
            if (streakTimerText) streakTimerText.gameObject.SetActive(false);
            
            if (streakActionButtonImage && streakClaimableSprite) streakActionButtonImage.sprite = streakClaimableSprite;
            if (streakActionButton) streakActionButton.interactable = true;
        }
        else
        {
            // Waiting mode: show countdown HH:MM:SS to nextUtcMidnight
            if (streakClaimValueText) streakClaimValueText.gameObject.SetActive(false);
            if (streakTimerText) streakTimerText.gameObject.SetActive(true);

            if (streakActionButtonImage && streakWaitingSprite) streakActionButtonImage.sprite = streakWaitingSprite;
            if (streakActionButton) streakActionButton.interactable = false;
            _countdownCo = StartCoroutine(CountdownCo());
        }
    }

    private System.Collections.IEnumerator CountUpStreak(int targetVal, float duration)
    {
        if (!streakTMP) yield break;
        if (targetVal <= 0) 
        {
            streakTMP.text = "0";
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int current = Mathf.RoundToInt(Mathf.Lerp(0, targetVal, t));
            streakTMP.text = current.ToString();
            yield return null;
        }
        streakTMP.text = targetVal.ToString();
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
            if (streakTimerText) streakTimerText.text = label;

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

    private int GetStatePriority(AchievementItemView.AchievementVisualState state)
    {
        switch (state)
        {
            case AchievementItemView.AchievementVisualState.Claimable: return 0;
            case AchievementItemView.AchievementVisualState.InProgress: return 1;
            case AchievementItemView.AchievementVisualState.Locked: return 2;
            case AchievementItemView.AchievementVisualState.Completed: return 3; // Or 2 if treated same as Locked
            default: return 99;
        }
    }

    public void OpenTelegramLink()
    {
        if (!string.IsNullOrEmpty(telegramLink))
            Application.OpenURL(telegramLink);
    }

    public void OpenXLink()
    {
        if (!string.IsNullOrEmpty(xLink))
            Application.OpenURL(xLink);
    }
}