using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;
using Sych;
using Sych.ShareAssets.Runtime;

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
    // ================== Share Button ==================
    /// <summary>
    /// Copies given text and opens native share sheet on mobile.
    /// Call this from your UI Button.
    /// </summary>
    public void OnShareButtonClick()
    {
        ShareReferralText("Hello World");
    }
    public async void ShareReferralText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Copy to clipboard for convenience
        GUIUtility.systemCopyBuffer = text;
        Debug.Log("[UIReferralPanel] Copied to clipboard: " + text);

#if UNITY_ANDROID || UNITY_IOS
        try
        {
            var payload = new List<string> { text };

            // Prefer new API: ShareAsync(string)
            var shareType = typeof(Share);
            var shareAsync = shareType.GetMethod("ShareAsync", new Type[] { typeof(string) });
            if (shareAsync != null)
            {
                var task = shareAsync.Invoke(null, new object[] { text }) as System.Threading.Tasks.Task;
                if (task != null) await task;
                else Debug.LogWarning("[UIReferralPanel] ShareAsync returned null Task; assuming opened.");
                return;
            }

            // Fallback 1: ItemsAsync(List<string>)
            var itemsAsync = shareType.GetMethod("ItemsAsync", new Type[] { typeof(List<string>) });
            if (itemsAsync != null)
            {
                var t = itemsAsync.Invoke(null, new object[] { payload }) as System.Threading.Tasks.Task;
                if (t != null) await t;
                else Debug.LogWarning("[UIReferralPanel] ItemsAsync returned null Task; assuming opened.");
                return;
            }

            // Fallback 2 (legacy/obsolete): Items(List<string>, Action<bool>)
            var items = shareType.GetMethod("Items", new Type[] { typeof(List<string>), typeof(Action<bool>) });
            if (items != null)
            {
                Action<bool> cb = success =>
                {
                    if (success) Debug.Log("[UIReferralPanel] Share window opened and returned.");
                    else Debug.LogWarning("[UIReferralPanel] Failed to open share window.");
                };
                items.Invoke(null, new object[] { payload, cb });
                return;
            }

            Debug.LogWarning("[UIReferralPanel] No compatible Share method found in plugin.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[UIReferralPanel] Share invoke error: " + ex.Message);
        }
#else
        Debug.Log("Sharing is only supported on mobile devices.");
#endif
    }
}