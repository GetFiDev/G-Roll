using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GRoll.Gameplay.Spawning
{
    /// <summary>
    /// Controls player spawning and despawning.
    /// </summary>
    public interface IPlayerSpawnController
    {
        bool IsPlayerSpawned { get; }
        Transform PlayerTransform { get; }

        UniTask SpawnPlayerAsync();
        UniTask SpawnPlayerAsync(Vector3 position, Quaternion rotation);
        void DespawnPlayer();
        void ResetPlayerPosition();

        /// <summary>
        /// Revives the player after death (e.g., after watching rewarded ad).
        /// Resets position and restores player state.
        /// </summary>
        void RevivePlayer();
    }
}
