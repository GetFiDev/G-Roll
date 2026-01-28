using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Gameplay.Player.Core;
using UnityEngine;
using VContainer;

namespace GRoll.Gameplay.Spawning
{
    /// <summary>
    /// Controls player spawning and despawning.
    /// </summary>
    public class PlayerSpawnController : IPlayerSpawnController
    {
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private PlayerController _playerController;
        private Transform _spawnPoint;
        private Vector3 _defaultSpawnPosition = new Vector3(0, 0.5f, 0);
        private Vector3 _defaultSpawnDirection = Vector3.forward;

        [Inject]
        public PlayerSpawnController(IMessageBus messageBus, IGRollLogger logger)
        {
            _messageBus = messageBus;
            _logger = logger;
        }

        public bool IsPlayerSpawned => _playerController != null && _playerController.gameObject.activeInHierarchy;
        public Transform PlayerTransform => _playerController?.transform;

        /// <summary>
        /// Set the player controller reference.
        /// Called during scene initialization.
        /// </summary>
        public void SetPlayerController(PlayerController controller)
        {
            _playerController = controller;
        }

        /// <summary>
        /// Set the spawn point transform.
        /// </summary>
        public void SetSpawnPoint(Transform spawnPoint)
        {
            _spawnPoint = spawnPoint;
        }

        public async UniTask SpawnPlayerAsync()
        {
            var position = _spawnPoint != null ? _spawnPoint.position : _defaultSpawnPosition;
            var rotation = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.LookRotation(_defaultSpawnDirection);

            await SpawnPlayerAsync(position, rotation);
        }

        public async UniTask SpawnPlayerAsync(Vector3 position, Quaternion rotation)
        {
            if (_playerController == null)
            {
                _logger.LogError("[PlayerSpawn] Player controller not set");
                return;
            }

            // Activate player
            _playerController.gameObject.SetActive(true);

            // Reset player state
            var direction = rotation * Vector3.forward;
            _playerController.ResetForNewSession(position, direction);

            // Play intro
            _playerController.PlayIntro();

            _messageBus.Publish(new PlayerSpawnedMessage(position));
            _logger.Log($"[PlayerSpawn] Player spawned at {position}");

            // Wait for intro
            await UniTask.Delay(500);

            _playerController.EndIntro();
        }

        public void DespawnPlayer()
        {
            if (_playerController == null) return;

            _playerController.Freeze();
            _playerController.gameObject.SetActive(false);

            _messageBus.Publish(new PlayerDespawnedMessage());
            _logger.Log("[PlayerSpawn] Player despawned");
        }

        public void ResetPlayerPosition()
        {
            if (_playerController == null) return;

            var position = _spawnPoint != null ? _spawnPoint.position : _defaultSpawnPosition;
            var direction = _spawnPoint != null ? _spawnPoint.forward : _defaultSpawnDirection;

            _playerController.ResetForNewSession(position, direction);
            _logger.Log("[PlayerSpawn] Player position reset");
        }

        public void RevivePlayer()
        {
            if (_playerController == null)
            {
                _logger.LogError("[PlayerSpawn] Cannot revive - player controller not set");
                return;
            }

            // Ensure player is active
            _playerController.gameObject.SetActive(true);

            // Reset to spawn position
            var position = _spawnPoint != null ? _spawnPoint.position : _defaultSpawnPosition;
            var direction = _spawnPoint != null ? _spawnPoint.forward : _defaultSpawnDirection;

            _playerController.ResetForNewSession(position, direction);
            _playerController.EndIntro(); // Skip intro on revive

            _messageBus.Publish(new PlayerRevivedMessage(position));
            _logger.Log("[PlayerSpawn] Player revived");
        }
    }

    /// <summary>
    /// Message published when player is revived.
    /// </summary>
    public readonly struct PlayerRevivedMessage : IMessage
    {
        public Vector3 Position { get; }

        public PlayerRevivedMessage(Vector3 position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Message published when player is spawned.
    /// </summary>
    public readonly struct PlayerSpawnedMessage : IMessage
    {
        public Vector3 Position { get; }

        public PlayerSpawnedMessage(Vector3 position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Message published when player is despawned.
    /// </summary>
    public readonly struct PlayerDespawnedMessage : IMessage { }
}
