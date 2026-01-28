using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Interfaces;
using UnityEngine;

namespace GRoll.Gameplay.Player.Collision
{
    /// <summary>
    /// Handles player collision detection with interactable objects.
    /// Routes collision events to IPlayerInteractable implementations.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerCollisionHandler : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool debugTriggers = false;

        [Header("References")]
        [SerializeField] private PlayerController player;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsCoinCollider(other)) return;
            if (TryFindInteractable(other, out var interactable))
            {
                if (debugTriggers)
                    Debug.Log($"[PlayerCollisionHandler] Enter -> {other.name}", other);
                interactable.OnPlayerEnter(player, other);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsCoinCollider(other)) return;
            if (TryFindInteractable(other, out var interactable))
            {
                if (debugTriggers)
                    Debug.Log($"[PlayerCollisionHandler] Stay -> {other.name}", other);
                interactable.OnPlayerStay(player, other, Time.deltaTime);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsCoinCollider(other)) return;
            if (TryFindInteractable(other, out var interactable))
            {
                if (debugTriggers)
                    Debug.Log($"[PlayerCollisionHandler] Exit -> {other.name}", other);
                interactable.OnPlayerExit(player, other);
            }
        }

        /// <summary>
        /// Find IPlayerInteractable on the collider, its parent, or children.
        /// </summary>
        private static bool TryFindInteractable(Collider other, out IPlayerInteractable interactable)
        {
            // Check same GameObject
            if (other.TryGetComponent<IPlayerInteractable>(out interactable))
                return true;

            // Check parent chain
            interactable = other.GetComponentInParent<IPlayerInteractable>();
            if (interactable != null)
                return true;

            // Check children
            interactable = other.GetComponentInChildren<IPlayerInteractable>();
            return interactable != null;
        }

        /// <summary>
        /// Check if collider is a coin (handled separately by magnet system).
        /// </summary>
        private static bool IsCoinCollider(Collider other)
        {
            return other.CompareTag("Coin");
        }
    }
}
