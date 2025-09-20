using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RemoteApp;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UIGalleryView : MonoBehaviour
{
    [Header("Service")]
    public RemoteAppDataService remote; // drag & drop

    [Header("Config")]
    public string collectionPath = "appdata/galleryitems/itemdata";
    public string[] itemIds = new string[] { "galleryview_1", "galleryview_2", "galleryview_3" };

    [Header("UI Targets")]
    public Image[]           imageSlots       = new Image[3];
    public TextMeshProUGUI[] descriptionSlots = new TextMeshProUGUI[3];
    public Button[]          clickAreas       = new Button[3];

    public event Action<string> OnActionRequested;

    private readonly string[] _guidanceKeys = new string[3];
    private CancellationTokenSource _cts;
    private static readonly Dictionary<string, Sprite> _cache = new();

    private void Awake()
    {
        // Click bağları
        if (clickAreas != null)
        {
            for (int i = 0; i < clickAreas.Length; i++)
            {
                int idx = i;
                if (clickAreas[idx] != null)
                {
                    clickAreas[idx].onClick.RemoveAllListeners();
                    clickAreas[idx].onClick.AddListener(() => HandleClick(idx));
                }
            }
        }
    }

    private void OnEnable()  => StartRefresh();
    private void OnDisable() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; }

    public void StartRefresh()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = RefreshAsync(_cts.Token);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (remote == null)
        {
            Debug.LogWarning("[UIGalleryView] remote null");
            return;
        }

        var items = await remote.FetchGalleryItemsAsync(collectionPath, itemIds);
        if (ct.IsCancellationRequested) return;

        Debug.Log($"[UIGalleryView] fetched {(items==null?0:items.Count)} items from '{collectionPath}'");

        for (int i = 0; i < 3; i++)
        {
            GalleryItemDTO dto = (items != null && i < items.Count) ? items[i] : null;

            _guidanceKeys[i] = dto?.guidanceKey ?? "";
            Debug.Log($"[UIGalleryView] slot {i} key='{_guidanceKeys[i]}' descLen={(dto?.descriptionText?.Length ?? 0)} url='{dto?.pngUrl}'");

            if (i < descriptionSlots.Length && descriptionSlots[i] != null)
            {
                descriptionSlots[i].text = dto?.descriptionText ?? "";
            }

            if (i < imageSlots.Length && imageSlots[i] != null)
            {
                var img = imageSlots[i];
                if (!string.IsNullOrWhiteSpace(dto?.pngUrl))
                {
                    var sp = await GetSpriteAsync(dto.pngUrl, ct);
                    if (ct.IsCancellationRequested) return;
                    if (sp != null) img.sprite = sp;
                }
                // İsterseniz else: img.sprite = null; diyebilirsiniz.
            }
        }
    }

    private async Task<Sprite> GetSpriteAsync(string url, CancellationToken ct)
    {
        if (_cache.TryGetValue(url, out var sp) && sp != null)
            return sp;

        using var req = UnityWebRequestTexture.GetTexture(url);
        var op = req.SendWebRequest();
        while (!op.isDone)
        {
            if (ct.IsCancellationRequested)
            {
                try { req.Abort(); } catch {}
                return null;
            }
            await Task.Yield();
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[UIGalleryView] download failed: {url} => {req.error}");
            return null;
        }

        var tex = DownloadHandlerTexture.GetContent(req);
        if (tex == null) return null;

        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), 100f);
        _cache[url] = sprite;
        return sprite;
    }

    private void HandleClick(int index)
    {
        if (index < 0 || index >= _guidanceKeys.Length) return;
        var key = _guidanceKeys[index] ?? "";
        Debug.Log($"[UIGalleryView] Click idx={index}, guidance='{key}'");
        OnActionRequested?.Invoke(key);
    }
}