using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class UIReferralPanel : MonoBehaviour
{
    [Header("Refs")] public ReferralManager manager;

    [Header("List UI")]
    public Transform listParent;          // VerticalLayout
    public UIReferralDisplay itemPrefab;  // one per referred user
    public GameObject loadingPanel;

    [Header("Counter UI")]
    public TextMeshProUGUI friendsCountText; // "--" while fetching, then "N friend(s)"

    // Race-condition guard for fast open/close or multiple refresh calls
    private int _refreshSeq = 0;

    private void OnEnable()
    {
        StartRefresh();
    }

    private void OnDisable()
    {
        // Hide loader when panel is disabled
        ShowLoading(false);
        // Bump sequence so any in-flight RefreshAsync won't touch UI after disable
        _refreshSeq++;
    }

    /// <summary>
    /// Public entry to (re)load the list. Safe to call multiple times.
    /// </summary>
    public void StartRefresh()
    {
        int token = ++_refreshSeq;
        if (friendsCountText) friendsCountText.text = "--";
        StopAllCoroutines();
        StartCoroutine(Co_Refresh(token));
    }

    private void ShowLoading(bool on)
    {
        if (loadingPanel && loadingPanel.activeSelf != on)
            loadingPanel.SetActive(on);
    }

    private System.Collections.IEnumerator Co_Refresh(int token)
    {
        // Open loader immediately
        ShowLoading(true);

        if (manager == null)
        {
            ShowLoading(false);
            yield break;
        }

        // Kick the async work
        var task = manager.RefreshCacheAsync(100, includeEarnings: true);
        // Wait until it completes (success or fault), staying on Unity thread
        while (!task.IsCompleted)
            yield return null;

        // If a newer refresh started meanwhile, abort UI updates
        if (_refreshSeq != token)
        {
            ShowLoading(false);
            yield break;
        }

        // Swallow errors (optional: log)
        if (task.IsFaulted)
        {
            Debug.LogWarning(task.Exception?.GetBaseException()?.Message);
            ShowLoading(false);
            yield break;
        }

        // clear old items
        if (listParent)
        {
            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);
        }

        var items = manager.Cached;
        if (items != null && items.Count > 0)
        {
            foreach (var r in items)
            {
                if (!itemPrefab || !listParent) break;
                var go = Instantiate(itemPrefab, listParent);
                go.Set(r.username, r.earnedTotal);
            }
        }

        // Update counter text (only if this refresh is the latest)
        if (_refreshSeq == token && friendsCountText)
        {
            int count = items != null ? items.Count : 0;
            friendsCountText.text = count + " " + (count == 1 ? "friend" : "friends");
        }

        // Close loader at the end of THIS refresh
        ShowLoading(false);
    }
}