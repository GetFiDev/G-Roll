using UnityEngine;

public class InstantFillBooster : BoosterBase
{
    protected override void Apply(PlayerController player)
    {
        if (GameplayManager.Instance != null)
        {
            GameplayManager.Instance.BoosterFillToMaxInstant();
        }
    }
}