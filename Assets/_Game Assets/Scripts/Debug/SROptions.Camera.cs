using System.ComponentModel;
using GRoll.Core.Interfaces.Services;
using UnityEngine;
using VContainer;

/// <summary>
/// SRDebugger Camera Options - Runtime adjustment of gameplay camera parameters
/// Uses ICameraService (replaces GameplayCameraManager)
/// </summary>
public partial class SROptions
{
    private const string CameraCategory = "Camera";

    // Cached camera service - resolved from DI container
    private ICameraService _cameraService;
    private ICameraService CameraService
    {
        get
        {
            if (_cameraService != null) return _cameraService;

            // Try to resolve from VContainer
            var lifetimeScope = UnityEngine.Object.FindObjectOfType<VContainer.Unity.LifetimeScope>();
            if (lifetimeScope != null)
            {
                try
                {
                    _cameraService = lifetimeScope.Container.Resolve<ICameraService>();
                }
                catch
                {
                    // Service not registered or container not ready
                }
            }

            return _cameraService;
        }
    }

    #region Position Offset

    [Category(CameraCategory)]
    [DisplayName("Offset X")]
    [NumberRange(-50, 50)]
    [Increment(0.5)]
    [Sort(10)]
    public float CameraOffsetX
    {
        get => CameraService != null ? CameraService.GameplayOffset.x : 0f;
        set
        {
            if (CameraService == null) return;
            var offset = CameraService.GameplayOffset;
            offset.x = value;
            CameraService.GameplayOffset = offset;
            OnPropertyChanged(nameof(CameraOffsetX));
        }
    }

    [Category(CameraCategory)]
    [DisplayName("Offset Y")]
    [NumberRange(-50, 50)]
    [Increment(0.5)]
    [Sort(11)]
    public float CameraOffsetY
    {
        get => CameraService != null ? CameraService.GameplayOffset.y : 0f;
        set
        {
            if (CameraService == null) return;
            var offset = CameraService.GameplayOffset;
            offset.y = value;
            CameraService.GameplayOffset = offset;
            OnPropertyChanged(nameof(CameraOffsetY));
        }
    }

    [Category(CameraCategory)]
    [DisplayName("Offset Z")]
    [NumberRange(-50, 50)]
    [Increment(0.5)]
    [Sort(12)]
    public float CameraOffsetZ
    {
        get => CameraService != null ? CameraService.GameplayOffset.z : 0f;
        set
        {
            if (CameraService == null) return;
            var offset = CameraService.GameplayOffset;
            offset.z = value;
            CameraService.GameplayOffset = offset;
            OnPropertyChanged(nameof(CameraOffsetZ));
        }
    }

    #endregion

    #region Rotation

    [Category(CameraCategory)]
    [DisplayName("Rotation X")]
    [NumberRange(-90, 90)]
    [Increment(1)]
    [Sort(20)]
    public float CameraRotationX
    {
        get => CameraService != null ? CameraService.GameplayRotation.x : 0f;
        set
        {
            if (CameraService == null) return;
            var rot = CameraService.GameplayRotation;
            rot.x = value;
            CameraService.GameplayRotation = rot;
            OnPropertyChanged(nameof(CameraRotationX));
        }
    }

    [Category(CameraCategory)]
    [DisplayName("Rotation Y")]
    [NumberRange(-180, 180)]
    [Increment(1)]
    [Sort(21)]
    public float CameraRotationY
    {
        get => CameraService != null ? CameraService.GameplayRotation.y : 0f;
        set
        {
            if (CameraService == null) return;
            var rot = CameraService.GameplayRotation;
            rot.y = value;
            CameraService.GameplayRotation = rot;
            OnPropertyChanged(nameof(CameraRotationY));
        }
    }

    [Category(CameraCategory)]
    [DisplayName("Rotation Z")]
    [NumberRange(-180, 180)]
    [Increment(1)]
    [Sort(22)]
    public float CameraRotationZ
    {
        get => CameraService != null ? CameraService.GameplayRotation.z : 0f;
        set
        {
            if (CameraService == null) return;
            var rot = CameraService.GameplayRotation;
            rot.z = value;
            CameraService.GameplayRotation = rot;
            OnPropertyChanged(nameof(CameraRotationZ));
        }
    }

    #endregion

    #region FOV

    [Category(CameraCategory)]
    [DisplayName("FOV")]
    [NumberRange(20, 120)]
    [Increment(1)]
    [Sort(30)]
    public float CameraFOV
    {
        get => CameraService != null ? CameraService.GameplayFOV : 60f;
        set
        {
            if (CameraService == null) return;
            CameraService.GameplayFOV = value;
            OnPropertyChanged(nameof(CameraFOV));
        }
    }

    #endregion

    #region Copy Config

    [Category(CameraCategory)]
    [DisplayName("Copy Config JSON")]
    [Sort(100)]
    public void CopyCameraConfigToClipboard()
    {
        if (CameraService == null)
        {
            Debug.LogWarning("[SROptions.Camera] ICameraService not found!");
            return;
        }

        var offset = CameraService.GameplayOffset;
        var rotation = CameraService.GameplayRotation;
        var fov = CameraService.GameplayFOV;

        // Build JSON manually for clean formatting
        string json = $@"{{
  ""gameplayOffset"": {{ ""x"": {offset.x:F2}, ""y"": {offset.y:F2}, ""z"": {offset.z:F2} }},
  ""gameplayRotation"": {{ ""x"": {rotation.x:F2}, ""y"": {rotation.y:F2}, ""z"": {rotation.z:F2} }},
  ""gameplayFOV"": {fov:F2}
}}";

        GUIUtility.systemCopyBuffer = json;
        Debug.Log($"[SROptions.Camera] Config copied to clipboard:\n{json}");
    }

    #endregion

    #region Clear Cache

    /// <summary>
    /// Clears the cached camera service reference.
    /// Call this when scene changes or DI container is rebuilt.
    /// </summary>
    public void ClearCameraServiceCache()
    {
        _cameraService = null;
    }

    #endregion
}
