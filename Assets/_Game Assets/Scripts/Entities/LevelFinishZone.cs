using UnityEngine;

/// <summary>
/// Trigger zone that marks the level as successfully completed.
/// Attach this to a GameObject with a Trigger Collider at the end of Chapter maps.
/// </summary>
public class LevelFinishZone : MonoBehaviour
{
    [SerializeField] private bool active = true;

    private void OnTriggerEnter(Collider other)
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
                
                // Start Success Flow (Show UI then End Session)
                GameplayManager.Instance.StartSuccessFlow();
            }
            else
            {
                Debug.LogError("[LevelFinishZone] GameplayManager instance is null!");
            }
        }
    }
}
