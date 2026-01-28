using GRoll.Gameplay.Player.Core;
using UnityEngine;

namespace GRoll.Gameplay.Player.Visual
{
    /// <summary>
    /// Handles player visual rotation and ball rolling animation.
    /// Rotates the player to face movement direction and rolls the ball based on distance traveled.
    /// </summary>
    public class PlayerVisualAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The visual root of the rolling ball")]
        private Transform ballTransform;

        [Header("Turn / Roll Settings")]
        [SerializeField, Tooltip("Ball radius in meters")]
        private float ballRadius = 0.5f;
        [SerializeField, Tooltip("Turn speed in degrees per second")]
        private float turnSpeedDegPerSec = 720f;
        [SerializeField, Tooltip("Minimum movement threshold for rotation")]
        private float minDeltaToRotate = 0.0001f;

        private PlayerController _playerController;
        private bool _isInitialized = false;
        private bool _isFrozen = false;
        private Vector3 _lastPosition;

        public PlayerVisualAnimator Initialize(PlayerController playerController)
        {
            _playerController = playerController;
            _isInitialized = true;
            _lastPosition = transform.position;
            return this;
        }

        /// <summary>
        /// Freeze or unfreeze the visual animation.
        /// </summary>
        public void Freeze(bool freeze)
        {
            _isFrozen = freeze;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (!_isInitialized || _isFrozen) return;
            UpdateRotationAndRoll();
        }

        private void UpdateRotationAndRoll()
        {
            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - _lastPosition;
            delta.y = 0f;

            if (delta.sqrMagnitude < (minDeltaToRotate * minDeltaToRotate))
            {
                _lastPosition = currentPos;
                return;
            }

            // Rotate to face movement direction (always cardinal)
            Vector3 targetDirection = delta.normalized;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    turnSpeedDegPerSec * Time.deltaTime
                );
            }

            // Roll the ball based on distance traveled
            if (ballTransform != null)
            {
                float distance = delta.magnitude;
                float angleDeg = distance / Mathf.Max(0.0001f, ballRadius) * Mathf.Rad2Deg;
                Vector3 rollAxisWorld = transform.right;
                ballTransform.Rotate(rollAxisWorld, angleDeg, Space.World);
            }

            _lastPosition = currentPos;
        }
    }
}
