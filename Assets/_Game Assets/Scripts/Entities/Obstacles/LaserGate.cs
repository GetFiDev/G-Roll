using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class LaserGate : Wall
{
    [Header("Laser Gate Settings")]
    [Tooltip("The root parent object containing all lasers.")]
    public GameObject rootObject;
    [Tooltip("List of laser objects to toggle (children of root).")]
    public List<GameObject> laserObjects;
    [Tooltip("Delay between activating each laser in the list.")]
    public float sequenceDelay = 0.05f;
    [Tooltip("Time in seconds to wait before activating (Idle state).")]
    public float idleDuration = 3f;
    [Tooltip("Time in seconds to stay active (Laser ON).")]
    public float activeDuration = 3f;

    private float _timer;
    private bool _isLaserActive;
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

        // Initial state: Root OFF, Children OFF
        _isLaserActive = false;
        SetAllLasersImmediate(false); // Determine local state
        rootObject.SetActive(false); // Determine global visibility
        _timer = 0f;
    }

    private void Update()
    {
        if (rootObject == null) return;

        _timer += Time.deltaTime;

        // Check which duration to use based on current state
        float currentThreshold = _isLaserActive ? activeDuration : idleDuration;

        if (_timer >= currentThreshold)
        {
            // Switch state
            _timer = 0f; // Reset timer
            _isLaserActive = !_isLaserActive;
            
            if (_isLaserActive)
            {
                // Turn ON Sequence
                // 1. Ensure all children are OFF locally first
                SetAllLasersImmediate(false);
                // 2. Turn Root ON
                rootObject.SetActive(true);
                // 3. Start Sequence to turn children ON
                if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = StartCoroutine(OpenLaserSequence());
            }
            else
            {
                // Turn OFF Immediately
                // Just close the root.
                if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
                rootObject.SetActive(false);
                SetAllLasersImmediate(false); // Reset locals for cleanliness
            }
        }
    }

    private IEnumerator OpenLaserSequence()
    {
        foreach (var laser in laserObjects)
        {
            if (laser != null) laser.SetActive(true);
            if (sequenceDelay > 0f) yield return new WaitForSeconds(sequenceDelay);
        }
    }

    private void SetAllLasersImmediate(bool state)
    {
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


}

// Helper component added automatically to the moving part
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
