using UnityEngine;

public class Teleport : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private Teleport otherPortal;

    private const float SuspendDuration = 1f;
    private float _suspendedTime;

    public void OnInteract()
    {
        if (_suspendedTime > Time.time)
            return;
        
        Suspend();
        otherPortal.Suspend();

        PlayerController.Instance.playerMovement.Teleport(transform.position, otherPortal.transform.position);
    }

    private void Suspend()
    {
        _suspendedTime = Time.time + SuspendDuration;
    }
}