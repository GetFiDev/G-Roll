using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    private PlayerController _playerController;

    public PlayerCollision Initialize(PlayerController playerController)
    {
        _playerController = playerController;

        return this;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Interactable"))
            return;

        if (other.TryGetComponent(out IPlayerInteractable playerInteractable))
        {
            playerInteractable.OnInteract();
        }
    }
}