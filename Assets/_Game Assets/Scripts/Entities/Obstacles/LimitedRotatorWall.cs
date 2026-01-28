using UnityEngine;
using DG.Tweening;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class LimitedRotatorWall : Wall, IMapConfigurable
{
    [Header("Rotator Settings")]
    [Tooltip("The actual object that will rotate.")]
    public Transform movingPart;

    [Tooltip("Rotation speed in degrees per second.")]
    public float speed = 45f;
    [Tooltip("Start time offset (0-10s) to create wave effects.")]
    public float startOffset = 0f;

    [Header("Runtime Config")]
    [Range(0f, 360f)] public float angle1 = 0f;
    [Range(0f, 360f)] public float angle2 = 90f;
    public bool clockwise = true;

    // Visualization (Editor only)
    private LineRenderer _previewLine;
    private PlacedItemData _placedData;

    private void Start()
    {
        if (movingPart == null)
        {
            Debug.LogError("LimitedRotatorWall: Missing 'movingPart' reference!", this);
            return;
        }

        _placedData = GetComponent<PlacedItemData>();

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<LimitedRotatorCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<LimitedRotatorCollisionProxy>();
            proxy.owner = this;
        }
    }

    private void Update()
    {
        // Editor-only selection visualization
        bool isSelected = false;
#if UNITY_EDITOR
        isSelected = CheckEditorSelection();
#endif

        UpdateVisualization(isSelected);

        // --- GLOBAL TIME SYNC MOVEMENT ---
        if (movingPart != null)
        {
            float angleDiff = Mathf.DeltaAngle(angle1, angle2);
            float totalDist = Mathf.Abs(angleDiff);

            if (totalDist > 0.01f && speed > 0.1f)
            {
                float oneWayTime = totalDist / speed;
                float cycleDuration = oneWayTime * 2f;

                if (cycleDuration > 0.001f)
                {
                    float t = (Time.time + startOffset) % cycleDuration;

                    float tNorm;
                    if (t < oneWayTime)
                    {
                        tNorm = t / oneWayTime;
                        tNorm = Mathf.SmoothStep(0f, 1f, tNorm);
                    }
                    else
                    {
                        tNorm = (t - oneWayTime) / oneWayTime;
                        tNorm = 1f - Mathf.SmoothStep(0f, 1f, tNorm);
                    }

                    float currentAngle = Mathf.LerpAngle(angle1, angle2, tNorm);
                    movingPart.localRotation = Quaternion.Euler(0, currentAngle, 0);
                }
            }
        }
    }

#if UNITY_EDITOR
    private bool CheckEditorSelection()
    {
        if (_placedData == null) _placedData = GetComponent<PlacedItemData>();
        if (_placedData == null) return false;

        // Use reflection to check GridPlacer selection (avoid compile-time dependency)
        var placerType = System.Type.GetType("GridPlacer, GRoll.MapDesignerTools");
        if (placerType == null) return false;

        var placer = FindFirstObjectByType(placerType);
        if (placer == null) return false;

        var modeField = placerType.GetField("currentMode");
        var selectedField = placerType.GetProperty("SelectedObject");
        if (modeField == null || selectedField == null) return false;

        var mode = modeField.GetValue(placer);
        const int BuildMode_Modify = 3; // BuildMode.Modify value
        if (mode == null || (int)mode != BuildMode_Modify) return false;

        var selected = selectedField.GetValue(placer) as GameObject;
        return selected == _placedData.gameObject;
    }
#endif

    private void UpdateVisualization(bool show)
    {
        if (!show)
        {
            if (_previewLine != null) _previewLine.enabled = false;
            return;
        }

        if (_previewLine == null)
        {
            var lineObj = new GameObject("_AnglePreview");
            lineObj.transform.SetParent(this.transform);

            _previewLine = lineObj.AddComponent<LineRenderer>();
            _previewLine.useWorldSpace = true;

            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.renderQueue = 3500;

            _previewLine.material = mat;
            _previewLine.startColor = Color.cyan;
            _previewLine.endColor = Color.cyan;
            _previewLine.startWidth = 0.15f;
            _previewLine.endWidth = 0.15f;
            _previewLine.positionCount = 3;
        }

        _previewLine.enabled = true;

        Vector3 center = transform.position;
        center.y += 0.5f;

        Vector3 dir1 = Quaternion.Euler(0, angle1, 0) * Vector3.forward;
        Vector3 dir2 = Quaternion.Euler(0, angle2, 0) * Vector3.forward;

        Vector3 worldDir1 = transform.rotation * dir1;
        Vector3 worldDir2 = transform.rotation * dir2;

        Vector3 p1 = center + worldDir1 * 3f;
        Vector3 p2 = center + worldDir2 * 3f;

        _previewLine.SetPosition(0, p1);
        _previewLine.SetPosition(1, center);
        _previewLine.SetPosition(2, p2);
    }

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
                max = 360f,
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
                displayName = "Clockwise (Start Phase)",
                type = ConfigType.Bool,
                defaultBool = this.clockwise
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("speed", out var spdVal))
        {
            if (float.TryParse(spdVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                speed = Mathf.Clamp(f, 1f, 720f);
            }
        }

        if (config.TryGetValue("offset", out var oVal))
        {
            if (float.TryParse(oVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                startOffset = Mathf.Clamp(f, 0f, 10f);
        }

        if (config.TryGetValue("angle1", out var a1Val))
        {
            if (float.TryParse(a1Val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                angle1 = Mathf.Clamp(f, 0f, 360f);
            }
        }

        if (config.TryGetValue("angle2", out var a2Val))
        {
            if (float.TryParse(a2Val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                angle2 = Mathf.Clamp(f, 0f, 360f);
            }
        }

        if (config.TryGetValue("clockwise", out var clockVal))
        {
            clockwise = bool.Parse(clockVal);
        }
    }
}

public class LimitedRotatorCollisionProxy : MonoBehaviour
{
    public LimitedRotatorWall owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnProxyCollisionEnter(collision);
    }
}
