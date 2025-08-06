using UnityEngine;

public class Booster : Collectable
{
    [SerializeField] private float speedChangeAmount;
    
    public override void OnInteract()
    {
        base.OnInteract();
        
        PlayerController.Instance.playerMovement.ChangeSpeed(speedChangeAmount);
    }
}
