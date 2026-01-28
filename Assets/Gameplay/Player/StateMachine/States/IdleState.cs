using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;

namespace GRoll.Gameplay.Player.StateMachine.States
{
    /// <summary>
    /// Player is stationary, waiting for input.
    /// This is the initial state before game starts.
    /// </summary>
    public class IdleState : PlayerStateBase
    {
        public static IdleState Instance { get; } = new IdleState();

        private IdleState() { }

        public override void Enter(PlayerContext context)
        {
            context.Velocity = UnityEngine.Vector3.zero;
            context.SetAnimatorBool("IsRunning", false);
            context.SetAnimatorBool("IsIdle", true);
        }

        public override void Exit(PlayerContext context)
        {
            context.SetAnimatorBool("IsIdle", false);
        }

        public override IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            if (context.IsIntroPlaying) return null;

            switch (input.Type)
            {
                case PlayerInputType.Swipe:
                    context.NotifyFirstMove();
                    context.SetDirection(input.Direction);
                    return RunningState.Instance;

                case PlayerInputType.Tap:
                    context.NotifyFirstMove();
                    return RunningState.Instance;

                case PlayerInputType.Freeze:
                    return FrozenState.Instance;

                default:
                    return null;
            }
        }
    }
}
