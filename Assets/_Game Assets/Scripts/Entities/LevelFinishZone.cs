using UnityEngine;

/// <summary>
/// Trigger zone that marks the level as successfully completed.
/// Attach this to a GameObject with a Trigger Collider at the end of Chapter maps.
/// If the collider is on a child object, add LevelFinishZoneTrigger to that child.
/// </summary>
public class LevelFinishZone : MonoBehaviour
{
    [SerializeField] private bool active = true;

    private void Awake()
    {
        // Auto-setup: If we don't have a collider on this object, 
        // check children and add LevelFinishZoneTrigger if needed
        if (GetComponent<Collider>() == null)
        {
            var childColliders = GetComponentsInChildren<Collider>();
            foreach (var col in childColliders)
            {
                if (col.isTrigger && col.GetComponent<LevelFinishZoneTrigger>() == null)
                {
                    col.gameObject.AddComponent<LevelFinishZoneTrigger>();
                    Debug.Log($"[LevelFinishZone] Auto-added LevelFinishZoneTrigger to child: {col.gameObject.name}");
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>
    /// Called when a collider enters the finish zone.
    /// Can be called from child LevelFinishZoneTrigger scripts.
    /// </summary>
    public void HandleTriggerEnter(Collider other)
    {
        if (!active) return;

        // Check for player tag or component
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            Debug.Log("[LevelFinishZone] Player reached the finish line!");
            
            // Trigger Logic
            if (GameplayManager.Instance != null)
            {
                // Ensure we only trigger once
                active = false;
                
                // FIX #4: Chapter modda coasting ile success flow
                if (GameplayManager.Instance.CurrentMode == GameMode.Chapter)
                {
                    GameplayManager.Instance.StartSuccessFlowWithCoasting();
                }
                else
                {
                    // Endless mode'da normal success flow
                    GameplayManager.Instance.StartSuccessFlow();
                }
            }
            else
            {
                Debug.LogError("[LevelFinishZone] GameplayManager instance is null!");
            }
        }
    }
}
