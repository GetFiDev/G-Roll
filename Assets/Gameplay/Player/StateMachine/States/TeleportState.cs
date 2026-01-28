using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;
using UnityEngine;

namespace GRoll.Gameplay.Player.StateMachine.States
{
    /// <summary>
    /// Player is being teleported through a portal.
    /// Movement is locked during teleportation.
    /// </summary>
    public class TeleportState : PlayerStateBase
    {
        private Vector3 _targetPosition;
        private Vector3 _exitDirection;
        private float _teleportDuration = 0.5f;
        private float _timer;
        private Vector3 _startPosition;
        private bool _teleportComplete;
        private object _portalData;

        /// <summary>
        /// Create a new teleport state instance.
        /// </summary>
        public static TeleportState Create(object portalData)
        {
            return new TeleportState(portalData);
        }

        private TeleportState(object portalData)
        {
            _portalData = portalData;
        }

        public override void Enter(PlayerContext context)
        {
            _startPosition = context.Transform.position;
            _timer = 0f;
            _teleportComplete = false;

            // Extract target from portal data
            if (_portalData is PortalData data)
            {
                _targetPosition = data.ExitPosition;
                _exitDirection = data.ExitDirection;
                _teleportDuration = data.Duration;
            }
            else
            {
                // Default: teleport in place
                _targetPosition = _startPosition;
                _exitDirection = context.CurrentDirection;
            }

            context.SetAnimatorTrigger("Teleport");
            context.Velocity = Vector3.zero;
        }

        public override void Update(PlayerContext context)
        {
            if (_teleportComplete) return;

            _timer += Time.deltaTime;
            var t = Mathf.Clamp01(_timer / _teleportDuration);

            // Simple lerp with ease
            var easeT = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
            context.Transform.position = Vector3.Lerp(_startPosition, _targetPosition, easeT);

            if (t >= 1f)
            {
                _teleportComplete = true;
                context.Transform.position = _targetPosition;
                context.SetDirection(_exitDirection);
            }
        }

        public override void Exit(PlayerContext context)
        {
            // Ensure at target position
            context.Transform.position = _targetPosition;
        }

        public override IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            // Check if teleport complete
            if (_teleportComplete)
            {
                return RunningState.Instance;
            }

            // No input during teleport
            return null;
        }
    }

    /// <summary>
    /// Data for portal teleportation.
    /// </summary>
    public class PortalData
    {
        public Vector3 ExitPosition { get; set; }
        public Vector3 ExitDirection { get; set; }
        public float Duration { get; set; } = 0.5f;

        public PortalData(Vector3 exitPosition, Vector3 exitDirection, float duration = 0.5f)
        {
            ExitPosition = exitPosition;
            ExitDirection = exitDirection;
            Duration = duration;
        }
    }
}
