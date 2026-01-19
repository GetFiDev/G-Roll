using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Helps create compatible RenderTextures for VideoPlayer on Android devices.
/// Attach this to the same GameObject as VideoPlayer, or assign references manually.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoRenderTextureHelper : MonoBehaviour
{
    [SerializeField] private RawImage targetRawImage;
    [SerializeField] private bool autoCreateRenderTexture = true;
    
    private VideoPlayer _videoPlayer;
    private RenderTexture _runtimeRenderTexture;

    private void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        
        if (autoCreateRenderTexture && _videoPlayer != null)
        {
            SetupCompatibleRenderTexture();
        }
    }

    private void SetupCompatibleRenderTexture()
    {
        // Get video dimensions, fallback to common mobile resolution
        int width = (int)_videoPlayer.width;
        int height = (int)_videoPlayer.height;
        
        // If video not prepared yet, use default dimensions
        if (width <= 0 || height <= 0)
        {
            width = 1080;
            height = 1920;
        }

        // Create a compatible RenderTexture for Android
        // Using RenderTextureFormat.ARGB32 and no depth buffer for maximum compatibility
        _runtimeRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        _runtimeRenderTexture.useMipMap = false;
        _runtimeRenderTexture.autoGenerateMips = false;
        _runtimeRenderTexture.filterMode = FilterMode.Bilinear;
        _runtimeRenderTexture.wrapMode = TextureWrapMode.Clamp;
        
        // For better Android compatibility
        _runtimeRenderTexture.antiAliasing = 1;
        
        if (!_runtimeRenderTexture.Create())
        {
            Debug.LogError("[VideoRenderTextureHelper] Failed to create RenderTexture!");
            return;
        }

        // Assign to VideoPlayer
        _videoPlayer.targetTexture = _runtimeRenderTexture;
        
        // Assign to RawImage if provided
        if (targetRawImage != null)
        {
            targetRawImage.texture = _runtimeRenderTexture;
        }

        Debug.Log($"[VideoRenderTextureHelper] Created compatible RenderTexture: {width}x{height}");
    }

    private void OnDestroy()
    {
        // Clean up runtime RenderTexture
        if (_runtimeRenderTexture != null)
        {
            _runtimeRenderTexture.Release();
            Destroy(_runtimeRenderTexture);
            _runtimeRenderTexture = null;
        }
    }
}
