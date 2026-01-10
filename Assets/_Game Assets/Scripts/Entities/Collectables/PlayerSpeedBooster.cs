using UnityEngine;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class PlayerSpeedBooster : BoosterBase, IMapConfigurable
{
    [Tooltip("The boost amount logic (0.2 = +20%)")]
    public float speedBoost = 0.2f;

    protected override void Apply(PlayerController player)
    {
        if (player == null) { Destroy(gameObject); return; }
        GameplayManager.Instance.ApplyPlayerSpeedPercent(speedBoost);
    }

    // --- IMapConfigurable Implementation ---

    public List<ConfigDefinition> GetConfigDefinitions()
    {
        return new List<ConfigDefinition>
        {
            new ConfigDefinition
            {
                key = "boostPercent",
                displayName = "Boost Amount (%)",
                type = ConfigType.Float,
                min = -100f,
                max = 200f,
                defaultValue = 20f
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("boostPercent", out var val))
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                // Convert percent to float (20% -> 0.2)
                speedBoost = f / 100f;
            }
        }
    }
}