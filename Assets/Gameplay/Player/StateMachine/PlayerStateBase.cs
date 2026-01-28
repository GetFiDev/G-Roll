using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;

namespace GRoll.Gameplay.Player.StateMachine
{
    /// <summary>
    /// Base class for player states with default implementations.
    /// </summary>
    public abstract class PlayerStateBase : IPlayerState
    {
        public virtual void Enter(PlayerContext context) { }

        public virtual void Update(PlayerContext context) { }

        public virtual void FixedUpdate(PlayerContext context) { }

        public virtual void Exit(PlayerContext context) { }

        public virtual IPlayerState HandleInput(PlayerContext context, PlayerInputData input)
        {
            return null;
        }
    }
}
