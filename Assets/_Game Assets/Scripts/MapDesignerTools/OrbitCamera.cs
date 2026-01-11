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

    void OnEnable()
    {
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
    }

    void LateUpdate()
    {
        if (!pivot) return;
        
        // Only block INPUT handling, not camera transform updates
        bool shouldHandleInput = !IsInputBlocked;
        
        // Block if pointer is over UI
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            shouldHandleInput = false;

        // Handle input only when not blocked
        if (shouldHandleInput)
        {
            // Prioritize Touch Input
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                HandleTouchInput();
            }
            else
            {
                HandleMouseInput();
            }
        }

        // ALWAYS apply Rotation & Position (so pivot changes are reflected)
        UpdateCameraTransform();
    }

    void HandleTouchInput()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        
        // DPI-based normalization for consistent feel across devices
        float dpiScale = Screen.dpi > 0 ? 160f / Screen.dpi : 1f;

        // 1 Finger: Rotate (Orbit)
        if (touches.Count == 1)
        {
            var touch = touches[0];
            Vector2 d = touch.delta;
            
            // Only rotate if we've moved (with dead zone)
            if (d.sqrMagnitude > 1f)
            {
                // Significantly reduced sensitivity for touch rotation
                // Normalize by screen size for consistent feel
                float screenNormalizer = 1f / Mathf.Max(Screen.width, Screen.height);
                float rotSensitivity = orbitSpeed * 0.15f * dpiScale * screenNormalizer * Screen.height;
                
                yaw   += d.x * rotSensitivity * Time.deltaTime;
                pitch -= d.y * rotSensitivity * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
            }
        }
        // 2 Fingers: Pan & Zoom (no rotation)
        else if (touches.Count >= 2)
        {
            var t1 = touches[0];
            var t2 = touches[1];

            // --- Calculate pinch delta first to determine gesture type ---
            Vector2 t1PrevPos = t1.screenPosition - t1.delta;
            Vector2 t2PrevPos = t2.screenPosition - t2.delta;

            float prevDist = Vector2.Distance(t1PrevPos, t2PrevPos);
            float currDist = Vector2.Distance(t1.screenPosition, t2.screenPosition);
            float deltaDist = prevDist - currDist;
            
            // --- Zoom (Pinch) ---
            // Only apply zoom if pinch delta is significant
            if (Mathf.Abs(deltaDist) > 2f)
            {
                // Normalized zoom sensitivity
                float zoomSensitivity = zoomSpeed * 0.002f * dpiScale;
                distance = Mathf.Clamp(distance + deltaDist * zoomSensitivity, minDistance, maxDistance);
            }

            // --- Pan (Two finger Drag) - Z-axis rail mode ---
            // Average delta of both fingers
            Vector2 avgDelta = (t1.delta + t2.delta) * 0.5f;
            
            // Only pan if average movement is significant
            if (avgDelta.sqrMagnitude > 1f && _cam != null)
            {
                float vfovRad = _cam.fieldOfView * Mathf.Deg2Rad;
                float panDistance = Mathf.Max(distance, 15f);
                float worldPerPixelY = 2f * panDistance * Mathf.Tan(vfovRad * 0.5f) / Mathf.Max(1, Screen.height);
                
                // Significantly reduced pan sensitivity for touch
                float panSensitivity = panSpeed * 0.3f * dpiScale;
                
                // Direct Z-axis movement: vertical drag controls Z position
                // Dragging up (positive Y) moves forward (+Z), dragging down moves backward (-Z)
                float zMovement = -avgDelta.y * worldPerPixelY * panSensitivity;
                pivot.position = new Vector3(0f, 0f, pivot.position.z + zMovement);
            }
        }
    }

    void HandleMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Pan (Left Mouse held) - Z-axis rail mode
        if (mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            
            if (_cam != null)
            {
                float vfovRad = _cam.fieldOfView * Mathf.Deg2Rad;
                float panDistance = Mathf.Max(distance, 15f);
                float worldPerPixelY = 2f * panDistance * Mathf.Tan(vfovRad * 0.5f) / Mathf.Max(1, Screen.height);
                
                // Direct Z-axis movement: vertical drag controls Z position
                // Dragging up (positive Y) moves forward (+Z), dragging down moves backward (-Z)
                float zMovement = -d.y * worldPerPixelY * panSpeed;
                pivot.position = new Vector3(0f, 0f, pivot.position.z + zMovement);
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
    }

    void UpdateCameraTransform()
    {
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
    
    // ==================== PLACEMENT MODE HELPERS ====================
    
    /// <summary>
    /// Smoothly zoom to max (minDistance) for placement mode
    /// </summary>
    public void SmoothZoomToMax(float duration = 0.3f)
    {
        DOVirtual.Float(distance, minDistance, duration, v => distance = v).SetEase(Ease.OutCubic);
    }
    
    /// <summary>
    /// Pan pivot by delta on X and Z axes (for placement edge-pan)
    /// This allows X movement during placement, unlike normal rail mode
    /// </summary>
    public void PanPivotDelta(float deltaX, float deltaZ)
    {
        if (!pivot) return;
        Vector3 pos = pivot.position;
        pivot.position = new Vector3(pos.x + deltaX, 0f, pos.z + deltaZ);
    }
    
    /// <summary>
    /// Smoothly return X to 0 (rail center) after placement, keep Z where it is
    /// </summary>
    public void SmoothReturnToRail(float duration = 0.4f)
    {
        if (!pivot) return;
        
        Vector3 currentPos = pivot.position;
        Vector3 targetPos = new Vector3(0f, 0f, currentPos.z);
        pivot.DOMove(targetPos, duration).SetEase(Ease.OutCubic);
    }
    
    /// <summary>
    /// Smoothly restore zoom to a specific distance
    /// </summary>
    public void SmoothZoomTo(float targetDistance, float duration = 0.3f)
    {
        DOVirtual.Float(distance, targetDistance, duration, v => distance = v).SetEase(Ease.OutCubic);
    }
}