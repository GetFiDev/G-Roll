using UnityEngine;

public class PlayerSpeedBooster : Collectable
{
    [SerializeField] private float speedChangeAmount;
    
    public override void OnInteract(PlayerController player)
    {
        base.OnInteract(player);
        
        PlayerController.Instance.playerMovement.ChangeSpeed(speedChangeAmount);
    }
}
