using UnityEngine;
using MapDesignerTool; // for IMapConfigurable, PlacedItemData
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

    private MapGridCellUtility _grid;
    private PlacedItemData _placedData;
    private GridPlacer _placer;
    
    // Runtime Visualization
    private LineRenderer _previewLine;

    private void Start()
    {
        _grid = FindFirstObjectByType<MapGridCellUtility>();
        _placer = FindFirstObjectByType<GridPlacer>();
        _placedData = GetComponentInParent<PlacedItemData>();
    }

    private void Update()
    {
        // Lazy initialization retry
        if (_placedData == null)
        {
            _placedData = GetComponentInParent<PlacedItemData>();
            if (_placedData == null) 
            {
                 UpdateVisualization(false);
                 return;
            }
        }
        
        if (_grid == null) _grid = FindFirstObjectByType<MapGridCellUtility>();
        if (_placer == null) _placer = FindFirstObjectByType<GridPlacer>();

            // 1. Calculate dynamic center
        Vector3 centerPos = Vector3.zero;
        if (_grid != null) centerPos = _grid.GetCellCenterWorld(_placedData.gridX, _placedData.gridY);

        // Movement Logic
        if (_grid != null)
        {
            float cycleDuration = 0f;
            float t = 0f;

            // Use RAW local range for distance to ensure stability (ignore rotation FP errors)
            float localMag = new Vector3(rangeX, 0, rangeZ).magnitude;
            float dist = 2 * localMag;
            
            // Still need WorldOffset for TargetA/B positions
            Vector3 worldOffset = CalculateWorldOffset();
            Vector3 targetA = centerPos + worldOffset;
            Vector3 targetB = centerPos - worldOffset;

            // If range is zero, stay at center
            if (dist < 0.0001f)
            {
                if (Vector3.Distance(transform.position, centerPos) > 0.01f)
                    transform.position = Vector3.MoveTowards(transform.position, centerPos, speed * Time.deltaTime);
            }
            else
            {
                // GLOBAL TIME SYNC
                if (speed > 0.001f)
                {
                    // Duration for one full crossing (A -> B or B -> A)
                    // time = dist / speed
                    float oneWayTime = dist / speed; // Time to go from one end to the other
                    cycleDuration = oneWayTime * 2f; // Return Trip
                    
                    // use Time.time + offset
                    t = (Time.time + startOffset) % cycleDuration;
                    
                    // PingPong logic simulation manually to get position
                    // 0 -> oneWayTime: A -> B
                    // oneWayTime -> cycleDuration: B -> A
                    
                    Vector3 currentPos;
                    if (t < oneWayTime)
                    {
                        // Moving A -> B
                        float progress = t / oneWayTime; // 0..1
                        // Linear interpolation
                        currentPos = Vector3.Lerp(targetA, targetB, progress);
                    }
                    else
                    {
                        // Moving B -> A
                        float progress = (t - oneWayTime) / oneWayTime; // 0..1
                        currentPos = Vector3.Lerp(targetB, targetA, progress);
                    }
                    
                    transform.position = currentPos;
                }
            }

            // Visualization Logic
            bool isSelected = false;
            if (_placer != null && _placer.currentMode == BuildMode.Modify)
            {
                if (_placer.SelectedObject == _placedData.gameObject) isSelected = true;
            }
            
            UpdateVisualization(isSelected, targetA, targetB);

            // DEBUG SYNC (Unconditional for troubleshooting)
            /*
            if (speed > 0)
            {
               Debug.Log($"[{gameObject.name}] T:{t:F4} Cyc:{cycleDuration:F4} Off:{startOffset:F4} Spd:{speed:F4} Dist:{dist:F4} RX:{rangeX:F4} RZ:{rangeZ:F4}");
            }
            */
        }
    }

    private Vector3 CalculateWorldOffset()
    {
        Vector3 localOffset = new Vector3(rangeX, 0, rangeZ);
        // Use current rotation
        return transform.rotation * localOffset;
    }

    private void UpdateVisualization(bool show, Vector3 p1 = default, Vector3 p2 = default)
    {
        if (!show)
        {
            if (_previewLine != null) _previewLine.enabled = false;
            return;
        }

        // Create Line if missing
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
        
        // Elevate
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
}// Force Recompile Fri Jan  9 23:15:37 +03 2026
