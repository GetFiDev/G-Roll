using UnityEngine;

public class PlayerSpeedBooster : BoosterBase
{
    [Header("Player Lateral Speed Booster")]
    [Tooltip("Yanal (sol/sağ) hız çarpanı")]
    public float lateralMultiplier = 1.5f;

    [Tooltip("Buff süresi (sn)")]
    public float duration = 2f;

    [Tooltip("Eğer PlayerLateralSpeedBuff bulunamazsa, ileri (run) hızı şu kadar arttır.")]
    public float fallbackRunSpeedBoost = 2f;

    protected override void Apply(PlayerController player)
    {
        
    }
}