using UnityEngine;

/// <summary>
/// Helper script to relay trigger events from a child collider to the parent LevelFinishZone.
/// Attach this to any child GameObject that has the trigger collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelFinishZoneTrigger : MonoBehaviour
{
    private LevelFinishZone _parentZone;

    private void Awake()
    {
        // Find the LevelFinishZone in parent hierarchy
        _parentZone = GetComponentInParent<LevelFinishZone>();
        
        if (_parentZone == null)
        {
            Debug.LogError("[LevelFinishZoneTrigger] No LevelFinishZone found in parent hierarchy!");
        }
        
        // Ensure collider is set as trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("[LevelFinishZoneTrigger] Collider is not a trigger. Setting isTrigger = true.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_parentZone != null)
        {
            _parentZone.HandleTriggerEnter(other);
        }
    }
}
