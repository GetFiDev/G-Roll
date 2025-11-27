using UnityEngine;
using UnityEngine.InputSystem;

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

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (!pivot) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Pan (Left Mouse held)
        if (mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            // Convert screen delta to world-space at current distance using vertical FOV

            if (_cam != null)
            {
                float vfovRad = _cam.fieldOfView * Mathf.Deg2Rad;
                // world units per pixel at current distance along vertical axis
                float worldPerPixelY = 2f * distance * Mathf.Tan(vfovRad * 0.5f) / Mathf.Max(1, Screen.height);
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
}