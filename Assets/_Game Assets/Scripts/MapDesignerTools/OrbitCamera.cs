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
        if (IsInputBlocked) return;

        // Block if pointer is over UI
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        // Prioritize Touch Input
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
        {
            HandleTouchInput();
        }
        else
        {
            HandleMouseInput();
        }

        // Apply Rotation & Position
        UpdateCameraTransform();
    }

    void HandleTouchInput()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        // 1 Finger: Pan
        if (touches.Count == 1)
        {
            var touch = touches[0];
            Vector2 d = touch.delta;

            // Adjust sensitivity for touch (pixels) vs mouse
            // Using existing panSpeed, but touch deltas can be large.
            // Pan logic from Mouse
            if (_cam != null)
            {
                float vfovRad = _cam.fieldOfView * Mathf.Deg2Rad;
                float panDistance = Mathf.Max(distance, 15f);
                float worldPerPixelY = 2f * panDistance * Mathf.Tan(vfovRad * 0.5f) / Mathf.Max(1, Screen.height);
                float worldPerPixelX = worldPerPixelY * _cam.aspect;
                
                // Invert X/Y for natural drag (finger drags functionality, so camera moves opposite? 
                // "Palm view" usually means dragging the world, so moving finger RIGHT moves camera LEFT.
                // Existing code: move = (-right * dx) + (-up * dy). This moves camera LEFT when mouse moves RIGHT. 
                // This matches "drag the world".
                
                Vector3 move = (-transform.right * d.x * worldPerPixelX) + (-transform.up * d.y * worldPerPixelY);
                pivot.position += move * panSpeed; // panSpeed might need tuning for mobile
            }
        }
        // 2 Fingers: Orbit & Zoom
        else if (touches.Count >= 2)
        {
            var t1 = touches[0];
            var t2 = touches[1];

            // --- Orbit (Two finger Drag) ---
            // Average delta
            Vector2 avgDelta = (t1.delta + t2.delta) * 0.5f;
            
            // Apply Orbit
            yaw   += avgDelta.x * orbitSpeed * Time.deltaTime * 0.05f; // reduced sensitivity for touch
            pitch -= avgDelta.y * orbitSpeed * Time.deltaTime * 0.05f;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            // --- Zoom (Pinch) ---
            Vector2 t1PrevPos = t1.screenPosition - t1.delta;
            Vector2 t2PrevPos = t2.screenPosition - t2.delta;

            float prevDist = Vector2.Distance(t1PrevPos, t2PrevPos);
            float currDist = Vector2.Distance(t1.screenPosition, t2.screenPosition);
            float deltaDist = prevDist - currDist; // + means pincing IN (zoom out? no distance increases)
            
            // Logic: Pinch In (fingers closer) -> distance increases (Zoom Out)
            // Logic: Pinch Out (fingers apart) -> distance decreases (Zoom In)
            // deltaDist > 0 (Prev > Curr) -> Pinch In -> Zoom Out (Increase distance)
            
            // Scale zoom speed for touch
            distance = Mathf.Clamp(distance + deltaDist * (zoomSpeed * 0.01f), minDistance, maxDistance);
        }
    }

    void HandleMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Pan (Left Mouse held)
        if (mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            
            if (_cam != null)
            {
                float vfovRad = _cam.fieldOfView * Mathf.Deg2Rad;
                float panDistance = Mathf.Max(distance, 15f);
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
}