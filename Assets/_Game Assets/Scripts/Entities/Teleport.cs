using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Interfaces;
using GRoll.Gameplay.Player.Movement;
using UnityEngine;

public class Teleport : MonoBehaviour, IPlayerInteractable
{
    [Tooltip("Paired portal to exit from.")]
    public Teleport otherPortal;

    [Tooltip("Optional: the visual center point the player spirals into. If null, uses this transform.")]
    public Transform entryCenter;

    public void OnPlayerEnter(PlayerController player, Collider other)
    {
        if (otherPortal == null) return;

        // Teleport.cs only *orders* a teleport; PlayerMovement performs it.
        if (player != null && player.TryGetComponent(out PlayerMovement movement))
        {
            var center = entryCenter != null ? entryCenter : transform;
            // Preserve the player's current facing so exit jump honors it.
            Vector3 entryForward = player.transform.forward;
            movement.RequestTeleport(
                exitTransform: otherPortal.transform,
                entryCenter: center,
                preservedEntryForward: entryForward
            );
        }
    }

    public void OnPlayerStay(PlayerController p, Collider o, float dt) { }
    public void OnPlayerExit(PlayerController p, Collider o) { }
}