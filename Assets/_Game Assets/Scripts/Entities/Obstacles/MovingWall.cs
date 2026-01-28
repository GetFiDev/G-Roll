using UnityEngine;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class MovingWall : Wall, IMapConfigurable
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed in units per second.")]
    public float speed = 5f;
    [Tooltip("Start time offset (0-10s) to create wave effects.")]
    public float startOffset = 0f;

    [Header("Oscillation Ranges (Local)")]
    [Tooltip("Oscillation range on X axis (relative to center).")]
    public float rangeX = 0f;
    [Tooltip("Oscillation range on Z axis (relative to center).")]
    public float rangeZ = 0f;

    // Runtime state
    private Vector3 _centerPosition;
    private bool _initialized = false;

    // Editor-only references (optional)
    private MapGridCellUtility _grid;
    private PlacedItemData _placedData;

    // Runtime Visualization
    private LineRenderer _previewLine;

    private void Start()
    {
        _centerPosition = transform.position;
        _initialized = true;

        _grid = FindFirstObjectByType<MapGridCellUtility>();
        _placedData = GetComponentInParent<PlacedItemData>();
    }

    private void Update()
    {
        if (!_initialized)
        {
            _centerPosition = transform.position;
            _initialized = true;
        }

        Vector3 centerPos = _centerPosition;

        // In Editor with PlacedItemData, update center dynamically for editing
        if (_placedData != null && _grid != null)
        {
            centerPos = _grid.GetCellCenterWorld(_placedData.gridX, _placedData.gridY);
            _centerPosition = centerPos;
        }

        // Movement Logic
        float localMag = new Vector3(rangeX, 0, rangeZ).magnitude;
        float dist = 2 * localMag;

        Vector3 worldOffset = CalculateWorldOffset();
        Vector3 targetA = centerPos + worldOffset;
        Vector3 targetB = centerPos - worldOffset;

        if (dist < 0.0001f)
        {
            if (Vector3.Distance(transform.position, centerPos) > 0.01f)
                transform.position = Vector3.MoveTowards(transform.position, centerPos, speed * Time.deltaTime);
        }
        else
        {
            if (speed > 0.001f)
            {
                float oneWayTime = dist / speed;
                float cycleDuration = oneWayTime * 2f;
                float t = (Time.time + startOffset) % cycleDuration;

                Vector3 currentPos;
                if (t < oneWayTime)
                {
                    float progress = t / oneWayTime;
                    currentPos = Vector3.Lerp(targetA, targetB, progress);
                }
                else
                {
                    float progress = (t - oneWayTime) / oneWayTime;
                    currentPos = Vector3.Lerp(targetB, targetA, progress);
                }

                transform.position = currentPos;
            }
        }

        // Editor-only selection visualization
        bool isSelected = false;
#if UNITY_EDITOR
        isSelected = CheckEditorSelection();
#endif

        UpdateVisualization(isSelected, targetA, targetB);
    }

#if UNITY_EDITOR
    private bool CheckEditorSelection()
    {
        if (_placedData == null) _placedData = GetComponentInParent<PlacedItemData>();
        if (_placedData == null) return false;

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

    private Vector3 CalculateWorldOffset()
    {
        Vector3 localOffset = new Vector3(rangeX, 0, rangeZ);
        return transform.rotation * localOffset;
    }

    private void UpdateVisualization(bool show, Vector3 p1 = default, Vector3 p2 = default)
    {
        if (!show)
        {
            if (_previewLine != null) _previewLine.enabled = false;
            return;
        }

        if (_previewLine == null)
        {
            var lineObj = new GameObject("_MovePathPreview");
            lineObj.transform.SetParent(this.transform);

            _previewLine = lineObj.AddComponent<LineRenderer>();
            _previewLine.useWorldSpace = true;

            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.renderQueue = 3500;

            _previewLine.material = mat;
            _previewLine.startColor = Color.yellow;
            _previewLine.endColor = Color.yellow;
            _previewLine.startWidth = 0.15f;
            _previewLine.endWidth = 0.15f;
            _previewLine.positionCount = 2;
        }

        _previewLine.enabled = true;

        p1.y += 0.2f;
        p2.y += 0.2f;

        _previewLine.SetPosition(0, p1);
        _previewLine.SetPosition(1, p2);
    }

    // --- IMapConfigurable Implementation ---

    public List<ConfigDefinition> GetConfigDefinitions()
    {
        return new List<ConfigDefinition>
        {
            new ConfigDefinition
            {
                key = "speed",
                displayName = "Speed",
                type = ConfigType.Float,
                min = 1f,
                max = 20f,
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
                key = "rangeX",
                displayName = "Range X (Local)",
                type = ConfigType.Float,
                min = 0f,
                max = 10f,
                defaultValue = this.rangeX
            },
            new ConfigDefinition
            {
                key = "rangeZ",
                displayName = "Range Z (Local)",
                type = ConfigType.Float,
                min = 0f,
                max = 10f,
                defaultValue = this.rangeZ
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("speed", out var sVal))
        {
            if (float.TryParse(sVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                speed = Mathf.Clamp(f, 0.5f, 50f);
        }

        if (config.TryGetValue("offset", out var oVal))
        {
            if (float.TryParse(oVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                startOffset = Mathf.Clamp(f, 0f, 10f);
        }

        if (config.TryGetValue("rangeX", out var rxVal))
        {
            if (float.TryParse(rxVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                rangeX = Mathf.Clamp(f, 0f, 20f);
        }

        if (config.TryGetValue("rangeZ", out var rzVal))
        {
            if (float.TryParse(rzVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                rangeZ = Mathf.Clamp(f, 0f, 20f);
        }
    }
}
