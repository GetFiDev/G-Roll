using UnityEngine;

public class CameraSpeedBooster : BoosterBase
{
    [Header("Camera Speed Booster")]
    public float additiveSpeed = 3f; // m/sn ekle

    protected override void Apply(PlayerController player)
    {
        if (GameplayManager.Instance == null) return;
        GameplayManager.Instance.IncreaseSpeed(additiveSpeed);
    }
}