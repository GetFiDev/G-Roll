using UnityEngine;

public class PlayerSpeedBooster : BoosterBase
{
    public float speedBoost = 0.2f;

    protected override void Apply(PlayerController player)
    {
        if (player == null) { Destroy(gameObject); return; }
        // %20 hız artışı, süre sonunda geri alınır.
        player.ApplyRunSpeedBoostPercentInstant(speedBoost);
        // Pickup sahneden kaybolsun
    }
}