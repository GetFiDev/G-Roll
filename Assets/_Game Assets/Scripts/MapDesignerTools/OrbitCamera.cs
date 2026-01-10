using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening; // Added for FocusOn animation

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    public Transform pivot;
    public float distance = 20f;
    public float minDistance = 5f, maxDistance = 60f;
    public float orbitSpeed = 180f;   // deg/sec
    public float zoomSpeed = 10f;     // units per scroll
    public float panSpeed = 1.0f;      // screen delta → world delta scaler
    public float pitchMin = 10f, pitchMax = 80f;

    float yaw = 45f, pitch = 45f;
    private Camera _cam;

    // Initial state for reset
    private float _initialYaw;
    private float _initialPitch;
    private float _initialDistance;
    private Vector3 _initialPivotPosition;

    /// <summary>
    /// If true, camera will ignore mouse input (pan/orbit/zoom).
    /// </summary>
    public bool IsInputBlocked { get; set; } = false;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        
        // Save initial state for reset
        _initialYaw = yaw;
        _initialPitch = pitch;
        _initialDistance = distance;
        if (pivot) _initialPivotPosition = pivot.position;
    }

    void LateUpdate()
    {
        if (!pivot) return;
        if (IsInputBlocked) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Block if pointer is over UI (prevents camera movement while dragging sliders)
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        // Pan (Left Mouse held)
        if (mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            
            if (_cam != null)
            {
                float vfovRad = _cam.fieldOfView * Mathf.Deg2Rad;
                
                // User Feedback: Panning is too slow/hard when zoomed in.
                // We map the distance used for calculation to a minimum (e.g. 15f) so it stays responsive close up.
                float panDistance = Mathf.Max(distance, 15f);

                // world units per pixel at current distance along vertical axis
                float worldPerPixelY = 2f * panDistance * Mathf.Tan(vfovRad * 0.5f) / Mathf.Max(1, Screen.height);
                float worldPerPixelX = worldPerPixelY * _cam.aspect;
                Vector3 move = (-transform.right * d.x * worldPerPixelX) + (-transform.up * d.y * worldPerPixelY);
                pivot.position += move * panSpeed;
            }
        }

        // Orbit (Right Mouse held)
        if (mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw   += d.x * orbitSpeed * Time.deltaTime * 0.02f;
            pitch -= d.y * orbitSpeed * Time.deltaTime * 0.02f;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        // Zoom
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(distance - scroll * (zoomSpeed * 0.1f), minDistance, maxDistance);
        }

        var rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = pivot.position - rot * Vector3.forward * distance;
        transform.rotation = rot;
    }

    public void FocusOn(Vector3 targetPos, float targetDist, float duration)
    {
        if (pivot)
        {
            pivot.DOMove(targetPos, duration).SetEase(Ease.OutCubic);
        }
        
        // Tween 'distance' value. We need a proxy or DOVirtual since it's a field.
        DOVirtual.Float(distance, targetDist, duration, (v) => distance = v).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// Reset camera to initial position and zoom (saved at game start)
    /// </summary>
    public void ResetToDefault(float duration = 0.3f)
    {
        // Animate to initial state
        DOVirtual.Float(yaw, _initialYaw, duration, v => yaw = v).SetEase(Ease.OutCubic);
        DOVirtual.Float(pitch, _initialPitch, duration, v => pitch = v).SetEase(Ease.OutCubic);
        DOVirtual.Float(distance, _initialDistance, duration, v => distance = v).SetEase(Ease.OutCubic);

        if (pivot)
        {
            pivot.DOMove(_initialPivotPosition, duration).SetEase(Ease.OutCubic);
        }
    }
}