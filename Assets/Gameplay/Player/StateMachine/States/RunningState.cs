using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;
using UnityEngine;

namespace GRoll.Gameplay.Player.StateMachine.States
{
    /// <summary>
    /// Player is running in current direction.
    /// The primary gameplay state.
    /// </summary>
    public class RunningState : PlayerStateBase
    {
        public static RunningState Instance { get; } = new RunningState();

        private RunningState() { }

        public override void Enter(PlayerContext context)
        {
            context.SetAnimatorBool("IsRunning", true);
        }

        public override void Update(PlayerContext context)
        {
            // Rotate towards movement direction
            var targetRotation = Quaternion.LookRotation(context.CurrentDirection, Vector3.up);
            context.Transform.rotation = Quaternion.Slerp(
                context.Transform.rotation,
                targetRotation,
                context.RotationSpeed * Time.deltaTime
            );

            // Update velocity
            context.Velocity = context.CurrentDirection * context.MoveSpeed;
        }

        public override void FixedUpdate(PlayerContext context)
        {
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
            context.SetAnimatorBool("IsRunning", false);
        }

        public override IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            switch (input.Type)
            {
                case PlayerInputType.Swipe:
                    context.SetDirection(input.Direction);
                    return null;

                case PlayerInputType.Tap:
                    if (context.IsGrounded)
                    {
                        return JumpingState.Instance;
                    }
                    return null;

                case PlayerInputType.DoubleTap:
                    // Double jump or special action
                    return null;

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
