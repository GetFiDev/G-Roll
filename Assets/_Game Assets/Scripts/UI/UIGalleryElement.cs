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

    [Header("Channels")]
    [SerializeField] private VoidEventChannelSO requestStartGameplay;

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
            // UI doğrudan faza müdahale etmesin; sadece istek yayınlasın
            if (requestStartGameplay != null)
                requestStartGameplay.Raise();
            else
                Debug.LogWarning("[UIGalleryElement] requestStartGameplay channel is not assigned.");
        }
    }

    private async UniTaskVoid UpdateGraphicsAsync(CancellationToken token)
    {
        try
        {
            var sprite = await LoadSpriteAsync(_info.backgroundImageUrl, token);
            if (sprite != null && !token.IsCancellationRequested)
            {
                // Eğer eski sprite cache'ten gelmişse dokunma, yoksa serbest bırak
                if (backgroundImage.sprite != null && !ReferenceEquals(backgroundImage.sprite, sprite))
                {
                    var oldTex = backgroundImage.sprite.texture;
                    if (oldTex != null && !SpriteCache.ContainsValue(backgroundImage.sprite))
                        Destroy(oldTex);
                }

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