using UnityEngine;

public class PlayerSpeedBooster : BoosterBase
{
    public float speedBoost = 0.2f;

    protected override void Apply(PlayerController player)
    {
        if (player == null) { Destroy(gameObject); return; }
        GameplayManager.Instance.ApplyPlayerSpeedPercent(speedBoost);
    }
}