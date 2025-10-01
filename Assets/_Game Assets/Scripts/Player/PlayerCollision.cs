using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerCollision : MonoBehaviour
{
    public PlayerController player; // sahneden atarsın; yoksa GetComponent ile buluruz

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponentInParent<IPlayerInteractable>();
        if (interactable != null)
        {
            interactable.OnPlayerEnter(player, other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        var interactable = other.GetComponentInParent<IPlayerInteractable>();
        if (interactable != null)
        {
            interactable.OnPlayerStay(player, other, Time.deltaTime);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var interactable = other.GetComponentInParent<IPlayerInteractable>();
        if (interactable != null)
        {
            interactable.OnPlayerExit(player, other);
        }
    }
}