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
        _ = RefreshAsync(token);
    }

    private void ShowLoading(bool on)
    {
        if (loadingPanel && loadingPanel.activeSelf != on)
            loadingPanel.SetActive(on);
    }

    private async Task RefreshAsync(int token)
    {
        // Open loader immediately
        ShowLoading(true);

        if (manager == null)
        {
            // Manager yoksa loader'ı yine de kapat; yeni bir refresh başlarsa tekrar açacaktır
            ShowLoading(false);
            return;
        }

        try
        {
            await manager.RefreshCacheAsync(100, includeEarnings: true);

            // If a newer refresh started meanwhile, abort UI updates
            if (_refreshSeq != token) return;

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
        }
        finally
        {
            // Her durumda bu refresh bitince loader'ı kapat; daha yeni bir refresh varsa kendi başında tekrar açar
            ShowLoading(false);
        }
    }
}