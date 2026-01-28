using GRoll.Gameplay.Player.Core;
using UnityEngine;

namespace GRoll.Gameplay.Player.Interfaces
{
    /// <summary>
    /// Interface for objects that can interact with the player.
    /// Implemented by entities like boosters, teleports, fans, etc.
    /// </summary>
    public interface IPlayerInteractable
    {
        void OnPlayerEnter(PlayerController player, Collider other);
        void OnPlayerStay(PlayerController player, Collider other, float dt);
        void OnPlayerExit(PlayerController player, Collider other);
    }
}
