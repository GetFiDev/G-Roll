using System.ComponentModel;
using UnityEngine;

/// <summary>
/// SRDebugger Camera Options - Runtime adjustment of gameplay camera parameters
/// </summary>
public partial class SROptions
{
    private const string CameraCategory = "Camera";
    
    // Cache to avoid null checks every frame
    private GameplayCameraManager CameraManager => GameplayCameraManager.Instance;
    
    #region Position Offset
    
    [Category(CameraCategory)]
    [DisplayName("Offset X")]
    [NumberRange(-50, 50)]
    [Increment(0.5)]
    [Sort(10)]
    public float CameraOffsetX
    {
        get => CameraManager != null ? CameraManager.GameplayOffset.x : 0f;
        set
        {
            if (CameraManager == null) return;
            var offset = CameraManager.GameplayOffset;
            offset.x = value;
            CameraManager.GameplayOffset = offset;
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
        get => CameraManager != null ? CameraManager.GameplayOffset.y : 0f;
        set
        {
            if (CameraManager == null) return;
            var offset = CameraManager.GameplayOffset;
            offset.y = value;
            CameraManager.GameplayOffset = offset;
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
        get => CameraManager != null ? CameraManager.GameplayOffset.z : 0f;
        set
        {
            if (CameraManager == null) return;
            var offset = CameraManager.GameplayOffset;
            offset.z = value;
            CameraManager.GameplayOffset = offset;
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
        get => CameraManager != null ? CameraManager.GameplayRotation.x : 0f;
        set
        {
            if (CameraManager == null) return;
            var rot = CameraManager.GameplayRotation;
            rot.x = value;
            CameraManager.GameplayRotation = rot;
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
        get => CameraManager != null ? CameraManager.GameplayRotation.y : 0f;
        set
        {
            if (CameraManager == null) return;
            var rot = CameraManager.GameplayRotation;
            rot.y = value;
            CameraManager.GameplayRotation = rot;
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
        get => CameraManager != null ? CameraManager.GameplayRotation.z : 0f;
        set
        {
            if (CameraManager == null) return;
            var rot = CameraManager.GameplayRotation;
            rot.z = value;
            CameraManager.GameplayRotation = rot;
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
        get => CameraManager != null ? CameraManager.GameplayFOV : 60f;
        set
        {
            if (CameraManager == null) return;
            CameraManager.GameplayFOV = value;
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
        if (CameraManager == null)
        {
            Debug.LogWarning("[SROptions.Camera] GameplayCameraManager not found!");
            return;
        }
        
        var offset = CameraManager.GameplayOffset;
        var rotation = CameraManager.GameplayRotation;
        var fov = CameraManager.GameplayFOV;
        
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
}
