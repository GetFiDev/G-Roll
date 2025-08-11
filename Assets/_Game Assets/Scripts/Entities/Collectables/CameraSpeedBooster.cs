using UnityEngine;

public class CameraSpeedBooster : Collectable
{
    [SerializeField] private float speedChangeAmount;
    
    public override void OnInteract()
    {
        base.OnInteract();
        
        GameManager.Instance.levelManager.currentLevel.CameraController.ChangeSpeed(speedChangeAmount);
    }
}
