using UnityEngine;

public class Fan : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private float jumpForce;
    
    private const float SuspendDuration = 1f;
    private float _suspendedTime;

    public void OnInteract()
    {
        if (_suspendedTime > Time.time)
            return;
        
        Suspend();

        PlayerController.Instance.playerMovement.Jump(jumpForce);
    }

    private void Suspend()
    {
        _suspendedTime = Time.time + SuspendDuration;
    }
}