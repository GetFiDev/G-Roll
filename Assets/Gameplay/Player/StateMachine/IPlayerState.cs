using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;

namespace GRoll.Gameplay.Player.StateMachine
{
    /// <summary>
    /// Interface for player state machine states.
    /// Each state handles its own behavior and transitions.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>
        /// Called when entering this state.
        /// </summary>
        void Enter(PlayerContext context);

        /// <summary>
        /// Called every frame while in this state.
        /// </summary>
        void Update(PlayerContext context);

        /// <summary>
        /// Called every fixed frame while in this state.
        /// </summary>
        void FixedUpdate(PlayerContext context);

        /// <summary>
        /// Called when exiting this state.
        /// </summary>
        void Exit(PlayerContext context);

        /// <summary>
        /// Handle input and return new state if transition needed.
        /// Return null to stay in current state.
        /// </summary>
        IPlayerState HandleInput(PlayerContext context, PlayerInputData input);
    }
}
