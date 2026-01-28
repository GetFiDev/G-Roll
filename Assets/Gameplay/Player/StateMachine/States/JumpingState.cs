using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;
using UnityEngine;

namespace GRoll.Gameplay.Player.StateMachine.States
{
    /// <summary>
    /// Player is in the air during a jump.
    /// Automatically transitions back to running when landing.
    /// Jump state data is stored in PlayerContext to support multiple players.
    /// </summary>
    public class JumpingState : PlayerStateBase
    {
        public static JumpingState Instance { get; } = new JumpingState();

        private JumpingState() { }

        public override void Enter(PlayerContext context)
        {
            context.IsGrounded = false;
            context.JumpTimer = 0f;
            context.JumpStartY = context.Transform.position.y;
            context.IsJumpAscending = true;

            context.SetAnimatorTrigger("Jump");
            context.SetAnimatorBool("IsJumping", true);
            context.TriggerJump();
        }

        public override void Update(PlayerContext context)
        {
            context.JumpTimer += Time.deltaTime;

            // Calculate jump arc (parabola)
            var t = context.JumpTimer / context.JumpDuration;

            if (t >= 1f)
            {
                // Jump complete, land
                return;
            }

            // Parabolic arc: h = 4 * maxHeight * t * (1 - t)
            var heightOffset = 4f * context.JumpHeight * t * (1f - t);
            var position = context.Transform.position;
            position.y = context.JumpStartY + heightOffset;
            context.Transform.position = position;

            // Check if descending
            if (context.IsJumpAscending && t > 0.5f)
            {
                context.IsJumpAscending = false;
            }

            // Rotate towards movement direction
            var targetRotation = Quaternion.LookRotation(context.CurrentDirection, Vector3.up);
            context.Transform.rotation = Quaternion.Slerp(
                context.Transform.rotation,
                targetRotation,
                context.RotationSpeed * Time.deltaTime
            );
        }

        public override void FixedUpdate(PlayerContext context)
        {
            // Apply horizontal movement during jump
            var movement = context.CurrentDirection * context.MoveSpeed * Time.fixedDeltaTime;
            movement.y = 0; // Vertical is handled in Update

            if (context.CharacterController != null)
            {
                context.CharacterController.Move(movement);
            }
            else if (context.Rigidbody != null)
            {
                var position = context.Transform.position + movement;
                position.y = context.Transform.position.y; // Preserve Y
                context.Rigidbody.MovePosition(position);
            }
            else
            {
                var position = context.Transform.position + movement;
                var currentY = context.Transform.position.y;
                context.Transform.position = new Vector3(position.x, currentY, position.z);
            }

            // Check if jump complete
            if (context.JumpTimer >= context.JumpDuration)
            {
                // Return to ground level
                var position = context.Transform.position;
                position.y = context.JumpStartY;
                context.Transform.position = position;

                context.IsGrounded = true;
                context.TriggerLand();
            }
        }

        public override void Exit(PlayerContext context)
        {
            context.SetAnimatorBool("IsJumping", false);

            // Ensure on ground
            var position = context.Transform.position;
            position.y = context.JumpStartY;
            context.Transform.position = position;
        }

        public override IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            // Check if jump is complete
            if (context.IsGrounded)
            {
                return RunningState.Instance;
            }

            switch (input.Type)
            {
                case PlayerInputType.Swipe:
                    // Allow direction change during jump
                    context.SetDirection(input.Direction);
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
