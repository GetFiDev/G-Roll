using UnityEngine;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class SpeedModifierZone : MonoBehaviour, IMapConfigurable
{
    [Header("Speed Settings")]
    [Tooltip("Multiplier to apply. Calculated from Percentage config (e.g. 100% = 2x, -50% = 0.5x).")]
    public float speedMultiplier = 2f;

    // Helper for inspector to see the percent value easily
    [SerializeField, HideInInspector]
    private float _modifierPercent = 100f; 

    private void Start()
    {
        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<SpeedModifierZoneProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<SpeedModifierZoneProxy>();
            proxy.owner = this;
            proxy.isTrigger = col.isTrigger;
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
            if (Mathf.Abs(speedMultiplier) > 0.001f)
            {
                float inverse = 1f / speedMultiplier;
                delta = inverse - 1f;
            }
        }

        GameplayManager.Instance.ApplyPlayerSpeedPercent(delta);
    }

    // --- IMapConfigurable Implementation ---

    public List<ConfigDefinition> GetConfigDefinitions()
    {
        return new List<ConfigDefinition>
        {
            new ConfigDefinition
            {
                key = "modifier",
                displayName = "Modifier (%)",
                type = ConfigType.Float,
                min = -300f,
                max = 300f,
                defaultValue = 100f // Default x2 speed
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("modifier", out var val))
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                _modifierPercent = Mathf.Clamp(f, -300f, 300f);
                
                // Convert Percent to Multiplier
                // 0% -> 1x (No Change)
                // 100% -> 2x
                // -50% -> 0.5x
                // mult = 1 + (percent / 100)
                speedMultiplier = 1f + (_modifierPercent / 100f);
                
                // Safety clamp to prevent div by zero logic if someone inputs exactly -100%
                if (Mathf.Abs(speedMultiplier) < 0.01f)
                {
                    speedMultiplier = 0.01f;
                }
            }
        }
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
