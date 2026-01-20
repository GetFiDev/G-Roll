using UnityEngine;
using DG.Tweening;
using MapDesignerTool; // for IMapConfigurable, PlacedItemData
using System.Collections.Generic;
using System.Globalization;

public class RotatorHammer : Wall, IMapConfigurable
{
    [Header("Hammer Settings")]
    [Tooltip("The actual object that will rotate.")]
    public Transform movingPart;
    
    [Tooltip("Rotation speed in degrees per second.")]
    public float speed = 45f;
    [Tooltip("Start time offset (0-10s) to create wave effects.")]
    public float startOffset = 0f;

    [Header("Configuration")]
    [Range(0f, 360f)] public float angle1 = 0f;
    [Range(0f, 360f)] public float angle2 = 90f;
    public bool clockwise = true;
    public bool powerfulAnimation = true;

    [Tooltip("Animation curve for the rotation movement.")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Particle Settings")]
    public bool enableFirstParticle;
    public ParticleSystem firstParticle;
    public bool enableSecondParticle;
    public ParticleSystem secondParticle;

    // Visualization
    private LineRenderer _previewLine;
    private GridPlacer _placer;
    private PlacedItemData _placedData;

    private void Start()
    {
        if (movingPart == null)
        {
            Debug.LogError("RotatorHammer: Missing 'movingPart' reference!", this);
            return;
        }

        _placer = FindFirstObjectByType<GridPlacer>();
        _placedData = GetComponent<PlacedItemData>();

        // Setup Collision Proxy on ALL child colliders
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<RotatorHammerCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<RotatorHammerCollisionProxy>();
            proxy.owner = this;
        }
    }

    private void Update()
    {
        // Lazy find
        if (_placer == null) _placer = FindFirstObjectByType<GridPlacer>();
        if (_placedData == null) _placedData = GetComponent<PlacedItemData>();

        bool isSelected = false;
        if (_placer != null && _placedData != null && _placer.currentMode == BuildMode.Modify)
        {
            if (_placer.SelectedObject == _placedData.gameObject) isSelected = true;
        }

        UpdateVisualization(isSelected);

        // --- GLOBAL TIME SYNC MOVEMENT ---
        if (movingPart != null)
        {
            // Calculate the shortest angular distance between the two angles
            float angleDiff = Mathf.DeltaAngle(angle1, angle2);
            float absAngleDiff = Mathf.Abs(angleDiff);

            if (absAngleDiff < 0.01f) absAngleDiff = 360f; // full spin if angles are same

            float totalDist = absAngleDiff;

            if (totalDist > 0.01f && speed > 0.1f)
            {
               float oneWayTime = totalDist / speed;
               float cycleDuration = oneWayTime * 2f;

               if (cycleDuration > 0.001f)
               {
                   float t = (Time.time + startOffset) % cycleDuration;

                   float startAngle = clockwise ? angle1 : angle2;
                   float endAngle   = clockwise ? angle2 : angle1;

                   // Calculate the delta for proper interpolation (handles 0/360 wrap)
                   float delta = Mathf.DeltaAngle(startAngle, endAngle);

                   float progress = 0f;
                   bool goingForward = (t < oneWayTime);

                   if (goingForward)
                   {
                       progress = t / oneWayTime; // 0..1
                   }
                   else
                   {
                       progress = (t - oneWayTime) / oneWayTime; // 0..1
                   }

                   // Apply Curve
                   float easedProgress = progress;
                   if (powerfulAnimation && movementCurve != null)
                   {
                       easedProgress = movementCurve.Evaluate(progress);
                   }

                   float currentAngle;
                   if (goingForward)
                   {
                       // Use delta to go the shortest path
                       currentAngle = startAngle + delta * easedProgress;
                   }
                   else
                   {
                       // Reverse: from end back to start
                       currentAngle = endAngle - delta * easedProgress;
                   }

                   movingPart.localRotation = Quaternion.Euler(0, 0, currentAngle);
               }
            }
        }
    }
    
    private void UpdateVisualization(bool show)
    {
        if (!show)
        {
            if (_previewLine != null) _previewLine.enabled = false;
            return;
        }

        if (_previewLine == null)
        {
            var lineObj = new GameObject("_HammerAnglePreview");
            lineObj.transform.SetParent(this.transform);
            
            _previewLine = lineObj.AddComponent<LineRenderer>();
            _previewLine.useWorldSpace = true;
            
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
            var mat = new Material(shader);
            mat.renderQueue = 3500; 
            
            _previewLine.material = mat;
            _previewLine.startColor = Color.magenta;
            _previewLine.endColor = Color.magenta;
            _previewLine.startWidth = 0.15f;
            _previewLine.endWidth = 0.15f;
            _previewLine.positionCount = 3;
        }

        _previewLine.enabled = true;

        Vector3 center = transform.position;
        center.y += 0.5f;

        Vector3 dir1 = Quaternion.Euler(0, 0, angle1) * Vector3.up;
        Vector3 dir2 = Quaternion.Euler(0, 0, angle2) * Vector3.up;

        Vector3 worldDir1 = transform.rotation * dir1;
        Vector3 worldDir2 = transform.rotation * dir2;
        
        Vector3 p1 = center + worldDir1 * 3f;
        Vector3 p2 = center + worldDir2 * 3f;

        _previewLine.SetPosition(0, p1);
        _previewLine.SetPosition(1, center);
        _previewLine.SetPosition(2, p2);
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
                defaultValue = this.speed
            },
            new ConfigDefinition
            {
                key = "offset",
                displayName = "Start Offset (s)",
                type = ConfigType.Float,
                min = 0f,
                max = 10f,
                defaultValue = this.startOffset
            },
            new ConfigDefinition
            {
                key = "angle1",
                displayName = "Angle 1",
                type = ConfigType.Float,
                min = 0f,
                max = 360f,
                defaultValue = this.angle1
            },
            new ConfigDefinition
            {
                key = "angle2",
                displayName = "Angle 2",
                type = ConfigType.Float,
                min = 0f,
                max = 360f,
                defaultValue = this.angle2
            },
            new ConfigDefinition
            {
                key = "clockwise",
                displayName = "Clockwise",
                type = ConfigType.Bool,
                defaultBool = this.clockwise
            },
            new ConfigDefinition
            {
                key = "powerfulAnimation",
                displayName = "Powerful Anim",
                type = ConfigType.Bool,
                defaultBool = this.powerfulAnimation
            },
            new ConfigDefinition
            {
                key = "particle1",
                displayName = "Particle 1",
                type = ConfigType.Bool,
                defaultBool = this.enableFirstParticle
            },
            new ConfigDefinition
            {
                key = "particle2",
                displayName = "Particle 2",
                type = ConfigType.Bool,
                defaultBool = this.enableSecondParticle
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("speed", out var val))
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                speed = Mathf.Clamp(f, 1f, 720f);
        }
        
        if (config.TryGetValue("offset", out var oVal))
        {
            if (float.TryParse(oVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                startOffset = Mathf.Clamp(f, 0f, 10f);
        }
        
        if (config.TryGetValue("angle1", out var a1))
        {
            if (float.TryParse(a1, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                angle1 = Mathf.Clamp(f, 0f, 360f);
        }

        if (config.TryGetValue("angle2", out var a2))
        {
            if (float.TryParse(a2, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                angle2 = Mathf.Clamp(f, 0f, 360f);
        }
        
        if (config.TryGetValue("clockwise", out var cw)) clockwise = bool.Parse(cw);
        if (config.TryGetValue("powerfulAnimation", out var pa)) powerfulAnimation = bool.Parse(pa);

        if (config.TryGetValue("particle1", out var p1Val)) enableFirstParticle = bool.Parse(p1Val);
        if (config.TryGetValue("particle2", out var p2Val)) enableSecondParticle = bool.Parse(p2Val);
    }
}

// Helper component added automatically
public class RotatorHammerCollisionProxy : MonoBehaviour
{
    public RotatorHammer owner;

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
