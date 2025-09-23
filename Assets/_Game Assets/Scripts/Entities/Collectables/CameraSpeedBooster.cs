using UnityEngine;

public class CameraSpeedBooster : Collectable
{
    [SerializeField] private float speedChangeAmount;
    
    public override void OnInteract(PlayerController player)
    {
        base.OnInteract(player);
        
        //GameManager.Instance.levelManager.currentLevel.CameraController.ChangeSpeed(speedChangeAmount);
    }
}
