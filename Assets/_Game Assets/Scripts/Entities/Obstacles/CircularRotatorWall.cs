using UnityEngine;
using DG.Tweening;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class CircularRotatorWall : Wall, IMapConfigurable
{
    [Header("Rotator Settings")]
    [Tooltip("The actual object that will rotate.")]
    public Transform movingPart;
    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 90f;
    [Tooltip("If true, rotates clockwise. If false, rotates counter-clockwise. Can be changed at runtime.")]
    public bool clockwise = true;
    [Tooltip("Start time offset (0-10s) to create wave effects.")]
    public float startOffset = 0f;

    private Quaternion _initialRotation;

    private void Start()
    {
        if (movingPart == null)
        {
            Debug.LogError("CircularRotatorWall: Moving Part is missing!", this);
            return;
        }

        // Store initial rotation to rotate relative to it
        _initialRotation = movingPart.localRotation;

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<CircularRotatorCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<CircularRotatorCollisionProxy>();
            proxy.owner = this;
        }
    }

    private void Update()
    {
        if (movingPart == null || rotationSpeed <= 0f) return;

        // GLOBAL TIME SYNC logic
        // Angle = (Time + offset) * speed * direction
        float direction = clockwise ? 1f : -1f;
        float time = Time.time + startOffset;
        float angle = time * rotationSpeed * direction;

        // Apply absolute rotation relative to initial state
        // This ensures all instances with same speed/offset effectively face the same direction at the same time
        movingPart.localRotation = _initialRotation * Quaternion.Euler(0, angle, 0); 
    }

    // Public methods for the proxy to call
    public void OnProxyTriggerEnter(Collider other)
    {
        if (expectTrigger) NotifyPlayer(other);
    }

    public void OnProxyCollisionEnter(Collision collision)
    {
        if (!expectTrigger) NotifyPlayer(collision.collider);
    }

    // --- IMapConfigurable Implementation ---

    public List<ConfigDefinition> GetConfigDefinitions()
    {
        return new List<ConfigDefinition>
        {
            new ConfigDefinition
            {
                key = "speed",
                displayName = "Speed (deg/s)",
                type = ConfigType.Float,
                min = 10f,
                max = 720f,
                defaultValue = this.rotationSpeed
            },
            new ConfigDefinition
            {
                key = "clockwise",
                displayName = "Clockwise",
                type = ConfigType.Bool,
                defaultValue = 0f, 
                defaultBool = this.clockwise
            },
            new ConfigDefinition
            {
                key = "offset",
                displayName = "Start Offset (s)",
                type = ConfigType.Float,
                min = 0f,
                max = 10f,
                defaultValue = this.startOffset
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("speed", out var spdVal))
        {
            if (float.TryParse(spdVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                rotationSpeed = Mathf.Clamp(f, 0f, 2000f);
        }

        if (config.TryGetValue("clockwise", out var clockVal))
        {
            clockwise = bool.Parse(clockVal);
        }
        
        if (config.TryGetValue("offset", out var oVal))
        {
            if (float.TryParse(oVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                startOffset = Mathf.Clamp(f, 0f, 10f);
        }
    }
}

// Helper component added automatically to the moving part (unchanged)
public class CircularRotatorCollisionProxy : MonoBehaviour
{
    public CircularRotatorWall owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnProxyCollisionEnter(collision);
    }
}
// Force Recompile Fri Jan  9 23:15:37 +03 2026
