using UnityEngine;

public class SpeedModifierZone : MonoBehaviour
{
    [Header("Speed Settings")]
    [Tooltip("Multiplier to apply when inside the zone. Start at 2 for x2 speed, 0.5 for half speed.")]
    public float speedMultiplier = 2f;

    private void Start()
    {
        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        // This allows putting the collider on any child object (visuals etc.)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<SpeedModifierZoneProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<SpeedModifierZoneProxy>();
            proxy.owner = this;
            proxy.isTrigger = col.isTrigger;
            
            // Ensure collider is a trigger, otherwise we can't walk through it!
            // The user implies "area" (alan), so it should likely be a trigger.
            // If they drag a solid collider, we force it to trigger?
            // Safer to assume user configures it as trigger, but for "Zone" logic,
            // we typically want non-blocking. I'll warn or force trigger?
            // Previous obstacles (Door/Button) rely on user setup.
            // But since this is a "Zone", blocking physics would be weird.
            // I'll force set isTrigger = true for convenience, as requested "dümdüz sürükleyeceğim".
            col.isTrigger = true; 
        }
    }

    public void OnProxyTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            ApplySpeed(true);
        }
    }

    public void OnProxyTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            ApplySpeed(false);
        }
    }

    private bool IsPlayer(Collider other)
    {
        // Check for player tag or component
        return other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null;
    }

    private void ApplySpeed(bool isEntering)
    {
        if (GameplayManager.Instance == null) return;

        float delta = 0f;

        if (isEntering)
        {
            // Apply Multiplier
            // Formula: old * (1 + delta) = old * mult
            // 1 + delta = mult -> delta = mult - 1
            delta = speedMultiplier - 1f;
        }
        else
        {
            // Revert (Apply Inverse Multiplier)
            // We want to divide by multiplier.
            // mult_inverse = 1 / mult
            // delta = (1/mult) - 1
            if (Mathf.Abs(speedMultiplier) > 0.001f)
            {
                float inverse = 1f / speedMultiplier;
                delta = inverse - 1f;
            }
        }

        GameplayManager.Instance.ApplyPlayerSpeedPercent(delta);
    }
}

// Helper component added automatically
public class SpeedModifierZoneProxy : MonoBehaviour
{
    public SpeedModifierZone owner;
    [HideInInspector] public bool isTrigger;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyTriggerExit(other);
    }
}
