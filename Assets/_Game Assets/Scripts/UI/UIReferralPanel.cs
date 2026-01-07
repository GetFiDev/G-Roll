using UnityEngine.UI;
using System.Linq;
using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sych.ShareAssets.Runtime;

public class UIReferralPanel : MonoBehaviour
{
    [Header("Refs")] public ReferralManager manager;

    [Header("Values")]
    public Transform listParent;          // VerticalLayout
    public UIReferralDisplay itemPrefab;  // one per referred user

    [Header("Counter UI")]
    public TextMeshProUGUI friendsCountText; // "--" while fetching, then "N friend(s)"
    
    [Header("Pending Earnings Popup")]
    public UIReferralEarningsPanel earningsPanelPrefab;
    private UIReferralEarningsPanel _currentPopup;

    [Header("Referral Rewards Strip")]
    [Tooltip("HorizontalLayoutGroup parent that will contain small referral reward icons.")]
    public Transform referralStripParent;
    [Tooltip("Prefab with two Images: [0] background, [1] icon.")]
    public GameObject referralRewardPrefab;
    [Tooltip("Background sprite when reward is locked.")]
    public Sprite lockedBackgroundSprite;
    [Tooltip("Background sprite when reward is unlocked.")]
    public Sprite unlockedBackgroundSprite;

    // Optional explicit ordering: if empty, all referral items from ItemLocalDatabase will be used.
    [Tooltip("Optional ordered list of referral itemIds. If empty, all items with itemReferralThreshold > 0 are shown.")]
    public List<string> referralItemOrder = new List<string>();

    // Race-condition guard for fast open/close or multiple refresh calls
    private int _refreshSeq = 0;

    private void OnEnable()
    {
        StartRefresh();
    }

    private void OnDisable()
    {
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

    private System.Collections.IEnumerator Co_Refresh(int token)
    {
        if (manager == null)
        {
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
            yield break;
        }

        // Swallow errors but ensure UI resets from "--" state if it failed
        if (task.IsFaulted)
        {
            Debug.LogWarning(task.Exception?.GetBaseException()?.Message);
            // Fallback: show 0 or keep "--", but maybe better to show 0 if partial data?
            // Actually if it faulted, we might not have data. 
            // Let's set to "0 friends" or "Error" to indicate state change.
            if (friendsCountText) friendsCountText.text = "Error"; 
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

        int referralCount = items != null ? items.Count : 0;
        // Use Global Count if available (more accurate for paging)
        if (manager != null) 
        {
            // Use Global Count if available, but ensure it's at least as large as the local list
            // (In case legacy data has missing 'referralCount' field on user doc)
            referralCount = Mathf.Max(referralCount, manager.GlobalReferralCount);
        }

        // Refresh the referral rewards strip using the same token guard
        RefreshReferralRewardsStrip(referralCount, token);

        // Update counter text (only if this refresh is the latest)
        Debug.Log($"[UIReferralPanel] End Refresh. Token={token}, Seq={_refreshSeq}. Count={referralCount}");
        if (_refreshSeq == token && friendsCountText)
        {
            int count = referralCount;
            friendsCountText.text = count + " " + (count == 1 ? "friend" : "friends");
        }

        // Check Pending Earnings & Popup
        if (_refreshSeq == token && manager.PendingTotal > 0)
        {
            // Only open if not already open
            if (_currentPopup == null && earningsPanelPrefab != null)
            {
                _currentPopup = Instantiate(earningsPanelPrefab, transform.parent); // Spawn in parent (mid-screen?)
                _currentPopup.transform.SetAsLastSibling(); // Top
                _currentPopup.Open(manager.PendingTotal, OnPopupClaimAndRefresh);
            }
        }
    }

    private async Task OnPopupClaimAndRefresh()
    {
        if (manager == null) return;
        
        // Perform claim via manager
        float claimed = await manager.ClaimEarningsAsync();
        
        if (claimed > 0)
        {
            Debug.Log($"[UIReferralPanel] Successfully claimed {claimed} from popup.");
            // Refresh main panel to update total earned (lifetime) if we were showing it somewhere 
            // (we decided not to show Global Lifetime, but maybe per-user items updated? 
            // Actually spec says lifetime ledger updates for each user. 
            // So if we reload list, 'TotalEarned' per user might increase!)
            StartRefresh(); 
        }
    }


    #region Referral Rewards Strip

    // Simple cache so we don't rebuild grayscale sprites repeatedly.
    private static readonly Dictionary<Sprite, Sprite> _grayscaleCache = new Dictionary<Sprite, Sprite>();

    private static Sprite GetGrayscaleSprite(Sprite source)
    {
        if (source == null) return null;

        if (_grayscaleCache.TryGetValue(source, out var cached))
            return cached;

        var tex = source.texture;
        var rect = source.textureRect;
        int x = Mathf.RoundToInt(rect.x);
        int y = Mathf.RoundToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);

        var pixels = tex.GetPixels(x, y, w, h);
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float g = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            pixels[i] = new Color(g, g, g, c.a);
        }

        var grayTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        grayTex.SetPixels(pixels);
        grayTex.Apply();

        var graySprite = Sprite.Create(grayTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        _grayscaleCache[source] = graySprite;
        return graySprite;
    }

    /// <summary>
    /// Builds an ordered list of (itemId, threshold) for referral rewards.
    /// If referralItemOrder is set, it is used for ordering/filtering.
    /// Otherwise all items with itemReferralThreshold > 0 from ItemLocalDatabase are used.
    /// </summary>
    private List<(string itemId, int threshold)> BuildReferralItemList()
    {
        var result = new List<(string, int)>();

        // Use ItemDatabaseManager as the single source of truth for item data
        var allEnum = ItemDatabaseManager.GetAllItems();
        if (allEnum == null)
            return result;

        // Materialize once so we can query multiple times
        var all = allEnum.ToList();
        if (all.Count == 0)
            return result;

        if (referralItemOrder != null && referralItemOrder.Count > 0)
        {
            // Explicit ordering: follow referralItemOrder, but only include valid referral items
            foreach (var id in referralItemOrder)
            {
                var data = all.FirstOrDefault(d => d.id == id);
                if (string.IsNullOrEmpty(data.id))
                    continue; // not found

                if (data.referralThreshold <= 0)
                    continue; // not a referral reward

                result.Add((data.id, data.referralThreshold));
            }
        }
        else
        {
            // Use all items that have a positive referral threshold
            foreach (var data in all)
            {
                if (data.referralThreshold <= 0)
                    continue;

                result.Add((data.id, data.referralThreshold));
            }

            // Order by threshold ascending, then by itemId for stability
            result = result
                .OrderBy(t => t.Item2) // threshold
                .ThenBy(t => t.Item1) // itemId
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// Refresh the small referral rewards strip based on current referral count.
    /// </summary>
    private void RefreshReferralRewardsStrip(int currentReferralCount, int token)
    {
        if (_refreshSeq != token)
            return;

        if (referralStripParent == null || referralRewardPrefab == null)
            return;

        // Clear old children
        for (int i = referralStripParent.childCount - 1; i >= 0; i--)
        {
            Destroy(referralStripParent.GetChild(i).gameObject);
        }

        var list = BuildReferralItemList();
        if (list == null || list.Count == 0)
            return;

        // Get readable item data from ItemDatabaseManager
        var allEnum = ItemDatabaseManager.GetAllItems();
        if (allEnum == null)
            return;

        var all = allEnum.ToList();
        if (all.Count == 0)
            return;

        foreach (var entry in list)
        {
            var itemId = entry.Item1;
            var threshold = entry.Item2;

            var data = all.FirstOrDefault(d => d.id == itemId);
            if (string.IsNullOrEmpty(data.id))
                continue;

            var go = Instantiate(referralRewardPrefab, referralStripParent);

            // Expect exactly two Images: [0] background, [1] icon
            var images = go.GetComponentsInChildren<Image>(true);
            Image bg = null;
            Image icon = null;

            if (images != null && images.Length > 0)
            {
                bg = images[0];
                if (images.Length > 1)
                    icon = images[1];
            }

            bool unlocked = currentReferralCount >= threshold;

            if (bg != null)
            {
                if (unlocked && unlockedBackgroundSprite != null)
                    bg.sprite = unlockedBackgroundSprite;
                else if (!unlocked && lockedBackgroundSprite != null)
                    bg.sprite = lockedBackgroundSprite;
            }

            if (icon != null)
            {
                // Logic: 
                // 1. Try ItemDatabaseManager sprite (preloaded?) -> usually null now
                // 2. Try Local Cache (downloaded previously)
                // 3. If missing, trigger download
                
                Sprite finalSprite = null;

                if (data.iconSprite != null)
                {
                    finalSprite = data.iconSprite;
                }
                else if (!string.IsNullOrEmpty(data.iconUrl))
                {
                    if (_iconCache.TryGetValue(data.iconUrl, out var cached))
                    {
                        finalSprite = cached;
                    }
                    else
                    {
                        // Trigger async load
                        // We use the default icon for now, and update when loaded
                        StartCoroutine(LoadIconRoutine(data.iconUrl, icon, unlocked));
                        finalSprite = icon.sprite; // Keep default/prefab sprite temporarily
                    }
                }
                else
                {
                    // No URL, no Sprite -> fallback to prefab default
                    finalSprite = icon.sprite;
                }

                if (finalSprite != null)
                {
                    icon.sprite = finalSprite;
                }

                // Apply tint state
                icon.color = unlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f); 
            }
        }
    }

    // Cache for downloaded icons to avoid re-downloading every frame/refresh
    private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

    private System.Collections.IEnumerator LoadIconRoutine(string url, Image targetImage, bool isUnlocked)
    {
        // Avoid parallel downloads for same URL? ideally yes, but simple cache check above handles existing.
        // If multiple items request same URL same frame, we might double download. Acceptable for now.
        
        var task = RemoteItemService.DownloadTextureAsync(url);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsCompletedSuccessfully && task.Result != null)
        {
            var tex = task.Result;
            var sprite = RemoteItemService.CreateSprite(tex);
            if (sprite != null)
            {
                // Cache it
                if (!_iconCache.ContainsKey(url)) _iconCache[url] = sprite;
                
                // Update UI if target still exists
                if (targetImage != null)
                {
                     targetImage.sprite = sprite;
                     // Re-apply correct color just in case
                     targetImage.color = isUnlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
                }
            }
        }
    }

    #endregion
    // ================== Share Button ==================
    
    [Header("Sharing")]
    public Sprite referralShareImage;

    /// <summary>
    /// Copies given text and opens native share sheet on mobile.
    /// Call this from your UI Button.
    /// </summary>
    public void OnShareButtonClick()
    {
        var msg = BuildReferralShareMessage();
        ShareReferralText(msg);
    }

    /// <summary>
    /// Shares ONLY the referral code (e.g. "ABC1234").
    /// </summary>
    public void OnShareCodeOnlyButtonClick()
    {
        if (manager == null) return;
        string key = manager.MyReferralKey;
        if (string.IsNullOrWhiteSpace(key) || key == "-")
        {
            // fallback if code is missing, maybe show a toast or nothing?
            // for now, just share empty or log warning
            Debug.LogWarning("[UIReferralPanel] No referral code to share.");
            return;
        }

        ShareReferralText(key, false); // false = do not attach image
    }

    public void OnCopyReferralKeyClick()
    {
        var msg = BuildReferralShareMessage();
        if (!string.IsNullOrWhiteSpace(msg))
        {
            GUIUtility.systemCopyBuffer = msg;
            Debug.Log("[UIReferralPanel] Copied to clipboard: " + msg);
        }
    }
    private string BuildReferralShareMessage()
    {
        if (manager == null)
            return "Join G‑Roll using my referral code!";

        string key = manager.MyReferralKey;

        if (string.IsNullOrWhiteSpace(key) || key == "-")
            return "Join G‑Roll using my referral code!";

        return $"Join G‑Roll using my referral code: {key}";
    }

    public async void ShareReferralText(string text, bool canAttachImage = true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Copy to clipboard for convenience
        GUIUtility.systemCopyBuffer = text;
        Debug.Log("[UIReferralPanel] Copied to clipboard: " + text);

        string imagePath = null;
        if (canAttachImage && referralShareImage != null && referralShareImage.texture != null)
        {
            try 
            {
                // Save texture to temporary file to share it
                // Note: Texture MUST be Read/Write enabled in import settings
                var tex = referralShareImage.texture;
                // If it's a sprite atlas or non-readable, this might fail or need a blit copy.
                // Assuming simple sprite texture for now.
                // To be safe against "Texture not readable", we can use a Render Texture blit approach if needed,
                // but let's try direct Encode first (simpler).
                
                // If texture is not readable, we may need a detour. 
                // Let's implement a safer "GetReadableTexture" helper locally or just try/catch.
                
                byte[] bytes = null;
                if (tex.isReadable)
                {
                    bytes = tex.EncodeToPNG();
                }
                else
                {
                    // Fallback: Create a readable copy via RenderTexture
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var readable = new Texture2D(tex.width, tex.height);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    bytes = readable.EncodeToPNG();
                    Destroy(readable); 
                }

                if (bytes != null)
                {
                    imagePath = System.IO.Path.Combine(Application.temporaryCachePath, "invite_image.png");
                    System.IO.File.WriteAllBytes(imagePath, bytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIReferralPanel] Failed to prepare image for sharing: {ex.Message}");
                imagePath = null;
            }
        }

#if UNITY_ANDROID || UNITY_IOS
        try
        {
            // If we have an image, we MUST use ItemsAsync (list of strings/paths) or similar.
            // Share.ItemAsync(string) is usually for text/url only?
            // Share.ItemsAsync(List<string>) supports paths. 
            
            var payload = new List<string>();
            payload.Add(text);
            if (!string.IsNullOrEmpty(imagePath))
            {
                payload.Add(imagePath);
            }

            // Using ItemsAsync for text+image or just text
            // Prefer new API: ShareAsync(string) if logic was simple, but for multiple items (text+img) we use ItemsAsync
            
            // Note: The library wrapper 'Share.ItemsAsync' takes List<string>. 
            // It handles checking if string is a file path or text.
            
            bool success = await Share.ItemsAsync(payload);
            if (success) Debug.Log("[UIReferralPanel] Share success.");
            else Debug.LogWarning("[UIReferralPanel] Share failed or user cancelled.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[UIReferralPanel] Share invoke error: " + ex.Message);
        }
#else
        Debug.Log($"Sharing (simulated): {text} " + (imagePath != null ? $"[Image: {imagePath}]" : ""));
#endif
    }

    }