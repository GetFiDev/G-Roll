using System;
using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Input;
using UnityEngine;

namespace GRoll.Gameplay.Player.StateMachine
{
    /// <summary>
    /// Player state machine implementation.
    /// Manages state transitions and delegates behavior to current state.
    /// </summary>
    public class PlayerStateMachine
    {
        private IPlayerState _currentState;
        private readonly PlayerContext _context;
        private bool _isLocked;

        public IPlayerState CurrentState => _currentState;
        public Type CurrentStateType => _currentState?.GetType();
        public bool IsLocked => _isLocked;

        public event Action<IPlayerState, IPlayerState> OnStateChanged;

        public PlayerStateMachine(PlayerContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Initialize with starting state.
        /// </summary>
        public void Initialize(IPlayerState initialState)
        {
            _currentState = initialState;
            _currentState.Enter(_context);
        }

        /// <summary>
        /// Process input and handle state transitions.
        /// </summary>
        public void ProcessInput(PlayerInputData input)
        {
            if (_isLocked || _currentState == null) return;

            var newState = _currentState.HandleInput(_context, input);

            if (newState != null && newState != _currentState)
            {
                TransitionTo(newState);
            }
        }

        /// <summary>
        /// Update current state (call from Update).
        /// </summary>
        public void Update()
        {
            if (_isLocked || _currentState == null) return;

            _currentState.Update(_context);
        }

        /// <summary>
        /// Fixed update current state (call from FixedUpdate).
        /// </summary>
        public void FixedUpdate()
        {
            if (_isLocked || _currentState == null) return;

            _currentState.FixedUpdate(_context);
        }

        /// <summary>
        /// Force transition to a new state.
        /// </summary>
        public void ForceTransition(IPlayerState newState)
        {
            if (_isLocked) return;
            TransitionTo(newState);
        }

        /// <summary>
        /// Lock the state machine (prevents transitions).
        /// </summary>
        public void Lock()
        {
            _isLocked = true;
        }

        /// <summary>
        /// Unlock the state machine.
        /// </summary>
        public void Unlock()
        {
            _isLocked = false;
        }

        private void TransitionTo(IPlayerState newState)
        {
            var previousState = _currentState;

            if (_currentState != null)
            {
                _currentState.Exit(_context);
            }

            _currentState = newState;
            _currentState.Enter(_context);

            OnStateChanged?.Invoke(previousState, newState);

#if UNITY_EDITOR
            Debug.Log($"[PlayerStateMachine] State changed: {previousState?.GetType().Name ?? "None"} -> {newState.GetType().Name}");
#endif
        }

        /// <summary>
        /// Check if current state is of type T.
        /// </summary>
        public bool IsInState<T>() where T : IPlayerState
        {
            return _currentState is T;
        }

        /// <summary>
        /// Get current state as type T.
        /// Returns null if not in that state.
        /// </summary>
        public T GetCurrentState<T>() where T : class, IPlayerState
        {
            return _currentState as T;
        }
    }
}
