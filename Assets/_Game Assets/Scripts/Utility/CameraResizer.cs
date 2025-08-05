using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraResizer : MonoBehaviour
{
    [Header("Target world width (in units) to maintain"), SerializeField]
    private float targetWidth = 10f; // Desired constant width in world units

    [Header("Orthographic Camera (auto-assigned if null)"), SerializeField]
    private Camera orthographicCamera;

    private void Awake()
    {
        orthographicCamera ??= GetComponent<Camera>();
        ResizeCamera();
    }

    private void ResizeCamera()
    {
        var aspect = (float)Screen.width / Screen.height;
        orthographicCamera.orthographicSize = targetWidth / (2f * aspect);
    }

#if UNITY_EDITOR
    // Auto-update in editor when values change
    private void OnValidate()
    {
        return;
        
        orthographicCamera ??= GetComponent<Camera>();
        ResizeCamera();
    }
#endif
}
