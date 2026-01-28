using System;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Gameplay.Player.Input;
using GRoll.Gameplay.Player.StateMachine;
using GRoll.Gameplay.Player.StateMachine.States;
using UnityEngine;
using VContainer;

namespace GRoll.Gameplay.Player.Core
{
    /// <summary>
    /// Main player controller component.
    /// Coordinates state machine, input, and visual components.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Animator animator;

        // NOTE: playerMovement field kaldırıldı (assembly boundary sorunu)
        // Entity'ler player.GetComponent<PlayerMovement>() kullanmalı

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float jumpDuration = 0.5f;

        [Inject] private IMessageBus _messageBus;

        private PlayerContext _context;
        private PlayerStateMachine _stateMachine;
        private bool _isInitialized;

        public PlayerContext Context => _context;
        public PlayerStateMachine StateMachine => _stateMachine;

        public event Action OnPlayerInitialized;
        public event Action OnFirstMove;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            // Create context
            _context = new PlayerContext(
                transform,
                playerRigidbody,
                characterController,
                animator
            );

            // Apply settings
            _context.MoveSpeed = moveSpeed;
            _context.RotationSpeed = rotationSpeed;
            _context.JumpHeight = jumpHeight;
            _context.JumpDuration = jumpDuration;

            // Subscribe to context events
            _context.OnFirstMove += HandleFirstMove;
            _context.OnDirectionChanged += HandleDirectionChanged;
            _context.OnJump += HandleJump;
            _context.OnLand += HandleLand;
            _context.OnWallHit += HandleWallHit;

            // Create state machine
            _stateMachine = new PlayerStateMachine(_context);
            _stateMachine.Initialize(IdleState.Instance);

            _isInitialized = true;
            OnPlayerInitialized?.Invoke();
        }

        private void Update()
        {
            if (!_isInitialized) return;
            _stateMachine.Update();
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;
            _stateMachine.FixedUpdate();
        }

        /// <summary>
        /// Process input from input handler.
        /// </summary>
        public void ProcessInput(PlayerInputData input)
        {
            if (!_isInitialized) return;
            _stateMachine.ProcessInput(input);
        }

        /// <summary>
        /// Force state transition.
        /// </summary>
        public void ForceState(IPlayerState state)
        {
            if (!_isInitialized) return;
            _stateMachine.ForceTransition(state);
        }

        /// <summary>
        /// Freeze the player.
        /// </summary>
        public void Freeze()
        {
            ProcessInput(PlayerInputData.Freeze());
        }

        /// <summary>
        /// Unfreeze the player.
        /// </summary>
        public void Unfreeze()
        {
            ProcessInput(PlayerInputData.Unfreeze());
        }

        /// <summary>
        /// Lock the state machine.
        /// </summary>
        public void LockStateMachine()
        {
            _stateMachine?.Lock();
        }

        /// <summary>
        /// Unlock the state machine.
        /// </summary>
        public void UnlockStateMachine()
        {
            _stateMachine?.Unlock();
        }

        /// <summary>
        /// Reset player to initial state for new session.
        /// </summary>
        public void ResetForNewSession(Vector3 position, Vector3 direction)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            _context.SetDirection(direction);
            _context.ResetFirstMoveNotification();
            _context.IsIntroPlaying = false;

            _stateMachine.ForceTransition(IdleState.Instance);
            _stateMachine.Unlock();
        }

        /// <summary>
        /// Start intro sequence.
        /// </summary>
        public void PlayIntro()
        {
            _context.IsIntroPlaying = true;
            _context.SetAnimatorTrigger("Intro");
        }

        /// <summary>
        /// End intro sequence.
        /// </summary>
        public void EndIntro()
        {
            _context.IsIntroPlaying = false;
        }

        /// <summary>
        /// Called when player hits a wall. Signals death/game over.
        /// Wall.cs calls this after handling PlayerMovement feedback directly.
        /// </summary>
        public void HitTheWall()
        {
            _context.TriggerWallHit();
            _messageBus?.Publish(new PlayerWallHitMessage());
        }

        #region Event Handlers

        private void HandleFirstMove()
        {
            OnFirstMove?.Invoke();
            _messageBus?.Publish(new PlayerFirstMoveMessage());
        }

        private void HandleDirectionChanged(Vector3 direction)
        {
            _messageBus?.Publish(new PlayerDirectionChangedMessage(direction));
        }

        private void HandleJump()
        {
            _messageBus?.Publish(new PlayerJumpMessage());
        }

        private void HandleLand()
        {
            _messageBus?.Publish(new PlayerLandMessage());
        }

        private void HandleWallHit()
        {
            _messageBus?.Publish(new PlayerWallHitMessage());
        }

        #endregion

        private void OnDestroy()
        {
            if (_context != null)
            {
                _context.OnFirstMove -= HandleFirstMove;
                _context.OnDirectionChanged -= HandleDirectionChanged;
                _context.OnJump -= HandleJump;
                _context.OnLand -= HandleLand;
                _context.OnWallHit -= HandleWallHit;
            }
        }
    }
}
