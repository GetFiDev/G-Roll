using UnityEngine;

public interface IPlayerInteractable
{
    void OnPlayerEnter(PlayerController player, Collider other);
    void OnPlayerStay(PlayerController player, Collider other, float dt);
    void OnPlayerExit(PlayerController player, Collider other);
}