using UnityEngine;

public abstract class Obstacle : MonoBehaviour, IPlayerInteractable
{
    public virtual void OnInteract(PlayerController player)
    {
    }
}
