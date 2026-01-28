using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;
using UnityEngine;

namespace GRoll.Gameplay.Player.StateMachine.States
{
    /// <summary>
    /// Player is coasting (moving without active input, e.g., after a jump or power-up).
    /// Gradually slows down and transitions back to running or idle.
    /// </summary>
    public class CoastingState : PlayerStateBase
    {
        public static CoastingState Instance { get; } = new CoastingState();

        private float _deceleration = 5f;
        private float _minSpeedToStop = 0.5f;
        private float _currentSpeed;

        private CoastingState() { }

        public override void Enter(PlayerContext context)
        {
            _currentSpeed = context.MoveSpeed;
            context.SetAnimatorBool("IsCoasting", true);
        }

        public override void Update(PlayerContext context)
        {
            // Decelerate
            _currentSpeed = Mathf.Max(0, _currentSpeed - _deceleration * Time.deltaTime);

            // Update velocity
            context.Velocity = context.CurrentDirection * _currentSpeed;

            // Rotate towards movement direction
            if (_currentSpeed > _minSpeedToStop)
            {
                var targetRotation = Quaternion.LookRotation(context.CurrentDirection, Vector3.up);
                context.Transform.rotation = Quaternion.Slerp(
                    context.Transform.rotation,
                    targetRotation,
                    context.RotationSpeed * Time.deltaTime
                );
            }
        }

        public override void FixedUpdate(PlayerContext context)
        {
            if (_currentSpeed <= _minSpeedToStop) return;

            // Apply movement
            var movement = context.Velocity * Time.fixedDeltaTime;

            if (context.CharacterController != null)
            {
                context.CharacterController.Move(movement);
            }
            else if (context.Rigidbody != null)
            {
                context.Rigidbody.MovePosition(context.Transform.position + movement);
            }
            else
            {
                context.Transform.position += movement;
            }
        }

        public override void Exit(PlayerContext context)
        {
            context.SetAnimatorBool("IsCoasting", false);
        }

        public override IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            // Check if stopped
            if (_currentSpeed <= _minSpeedToStop)
            {
                return IdleState.Instance;
            }

            switch (input.Type)
            {
                case PlayerInputType.Swipe:
                    context.SetDirection(input.Direction);
                    return RunningState.Instance;

                case PlayerInputType.Tap:
                    return RunningState.Instance;

                case PlayerInputType.TeleportTrigger:
                    return TeleportState.Create(input.Data);

                case PlayerInputType.Freeze:
                    return FrozenState.Instance;

                default:
                    return null;
            }
        }
    }
}
