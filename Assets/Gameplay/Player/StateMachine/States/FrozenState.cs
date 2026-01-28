using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;
using UnityEngine;

namespace GRoll.Gameplay.Player.StateMachine.States
{
    /// <summary>
    /// Player is frozen (paused, cutscene, etc).
    /// No movement or input processing.
    /// </summary>
    public class FrozenState : PlayerStateBase
    {
        public static FrozenState Instance { get; } = new FrozenState();

        private FrozenState() { }

        public override void Enter(PlayerContext context)
        {
            context.Velocity = Vector3.zero;
            context.SetAnimatorBool("IsFrozen", true);

            // Optionally stop animator
            if (context.Animator != null)
            {
                context.Animator.speed = 0f;
            }
        }

        public override void Exit(PlayerContext context)
        {
            context.SetAnimatorBool("IsFrozen", false);

            // Resume animator
            if (context.Animator != null)
            {
                context.Animator.speed = 1f;
            }
        }

        public override IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            // Only unfreeze input is accepted
            if (input.Type == PlayerInputType.Unfreeze)
            {
                return RunningState.Instance;
            }

            return null;
        }
    }
}
