using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using MapDesignerTool; // for IMapConfigurable
using System.Globalization;

public class LaserGate : Wall, IMapConfigurable
{
    [Header("Laser Gate Settings")]
    [Tooltip("The root parent object containing all lasers.")]
    public GameObject rootObject;
    [Tooltip("List of laser objects to toggle (children of root).")]
    public List<GameObject> laserObjects;
    
    // Sequence delay is tricky in Global Sync. 
    // If strict sync is needed, usually all turn on at once. 
    // If we want "wave" opening, that's fine inside the Active window.
    // For now, let's keep sequenceDelay relative to the START of the Active Phase.
    [Tooltip("Delay between activating each laser in the list.")]
    public float sequenceDelay = 0.05f;

    [Tooltip("Time in seconds to wait before activating (Idle state).")]
    public float idleDuration = 3f;
    [Tooltip("Time in seconds to stay active (Laser ON).")]
    public float activeDuration = 3f;
    [Tooltip("Start time offset (0-10s) to create wave effects.")]
    public float startOffset = 0f;

    private bool _wasActive = false;
    private Coroutine _sequenceCoroutine;

    private void Start()
    {
        if (rootObject == null || laserObjects == null || laserObjects.Count == 0)
        {
            Debug.LogError("LaserGate: Root Object or Laser List missing!", this);
            return;
        }

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<LaserGateCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<LaserGateCollisionProxy>();
            proxy.owner = this;
        }

        // Initial set
        SetAllLasersImmediate(false);
        rootObject.SetActive(false);
    }

    private void Update()
    {
        if (rootObject == null) return;

        // GLOBAL TIME SYNC
        // Cycle: ActiveDuration -> IdleDuration
        // Note: Previous logic was Idle -> Active -> Idle?
        // Let's standardize: 0..Active is ON, Active..Total is OFF.
        
        float totalDuration = activeDuration + idleDuration;
        if (totalDuration < 0.1f) return; // safety

        float t = (Time.time + startOffset) % totalDuration;
        
        bool shouldBeActive = (t < activeDuration);

        if (shouldBeActive != _wasActive)
        {
            // State Change detected
            _wasActive = shouldBeActive;
            
            if (shouldBeActive)
            {
                // TURN ON
                rootObject.SetActive(true);
                // Start sequence visual
                if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = StartCoroutine(OpenLaserSequence());
            }
            else
            {
                // TURN OFF
                if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
                rootObject.SetActive(false);
                SetAllLasersImmediate(false);
            }
        }
        
        // Safety: If active, ensure root is Active (in case of mishaps)
        if (shouldBeActive && !rootObject.activeSelf) 
        {
            rootObject.SetActive(true);
            // If sequence failed, maybe force all on?
            // Usually fine.
        }
    }

    private IEnumerator OpenLaserSequence()
    {
        // Turn them on one by one
        foreach (var laser in laserObjects)
        {
            if (laser == null) continue;
            laser.SetActive(true);
            if (sequenceDelay > 0.01f) yield return new WaitForSeconds(sequenceDelay);
        }
    }

    private void SetAllLasersImmediate(bool state)
    {
        if (laserObjects == null) return;
        foreach (var laser in laserObjects)
        {
            if (laser != null) laser.SetActive(state);
        }
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
                key = "activeTime",
                displayName = "Active Time (ON)",
                type = ConfigType.Float,
                min = 0.5f,
                max = 10.0f,
                defaultValue = this.activeDuration
            },
            new ConfigDefinition
            {
                key = "idleTime",
                displayName = "Idle Time (OFF)",
                type = ConfigType.Float,
                min = 0.5f,
                max = 10.0f,
                defaultValue = this.idleDuration
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
        if (config.TryGetValue("activeTime", out var activeVal))
        {
            if (float.TryParse(activeVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                 activeDuration = Mathf.Clamp(f, 0.1f, 60f);
            }
        }

        if (config.TryGetValue("idleTime", out var idleVal))
        {
            if (float.TryParse(idleVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                 idleDuration = Mathf.Clamp(f, 0.1f, 60f);
            }
        }
        
        if (config.TryGetValue("offset", out var oVal))
        {
            if (float.TryParse(oVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                startOffset = Mathf.Clamp(f, 0f, 10f);
        }
    }

}

// Helper component added automatically to the moving part (unchanged)
public class LaserGateCollisionProxy : MonoBehaviour
{
    public LaserGate owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyCollisionEnter(collision);
    }
}
// Force Recompile Fri Jan  9 23:15:37 +03 2026
