using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UIGalleryElement : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject loadingSpinner;

    private GalleryElementInfo _info;

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    [Button]
    public void Initialize(GalleryElementInfo elementInfo)
    {
        _info = elementInfo;
        titleText.text = _info.title;
        descriptionText.text = _info.description;

        loadingSpinner.SetActive(true);

        var token = this.GetCancellationTokenOnDestroy();
        UpdateGraphicsAsync(token).Forget();
    }

    public void OnClick()
    {
        if (!string.IsNullOrEmpty(_info.targetLink) && !string.IsNullOrWhiteSpace(_info.targetLink))
        {
            Application.OpenURL(_info.targetLink);
        }
        else
        {
            GameManager.Instance.LevelStart();
        }
    }

    private async UniTaskVoid UpdateGraphicsAsync(CancellationToken token)
    {
        try
        {
            var sprite = await LoadSpriteAsync(_info.backgroundImageUrl, token);
            if (sprite != null && !token.IsCancellationRequested)
            {
                Destroy(backgroundImage.sprite.texture);

                backgroundImage.sprite = sprite;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load image: {ex}");
        }
        finally
        {
            if (loadingSpinner != null)
                loadingSpinner.SetActive(false);
        }
    }

    private static async UniTask<Texture2D> LoadImageAsync(string url, CancellationToken token)
    {
        using var request = UnityWebRequestTexture.GetTexture(url);
        await request.SendWebRequest().ToUniTask(cancellationToken: token);

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Image load failed: {request.error}");
            return null;
        }

        return DownloadHandlerTexture.GetContent(request);
    }

    private static async UniTask<Sprite> LoadSpriteAsync(string url, CancellationToken token)
    {
        if (SpriteCache.TryGetValue(url, out var cached))
            return cached;

        var tex = await LoadImageAsync(url, token);
        if (tex == null) return null;

        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        SpriteCache[url] = sprite;
        return sprite;
    }
}

[Serializable]
public class GalleryElementInfo
{
    private const string PlaceHolderURL = "https://placehold.co/950x500/orange/white.png";

    public string backgroundImageUrl = PlaceHolderURL;
    public string title;
    public string description;

    public string targetLink;
}