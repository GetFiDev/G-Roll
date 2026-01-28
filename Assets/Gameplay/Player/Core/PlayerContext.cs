using System;
using UnityEngine;

namespace GRoll.Gameplay.Player.Core
{
    /// <summary>
    /// Shared context for all player components.
    /// Contains references and shared data accessible by all states and behaviors.
    /// </summary>
    public class PlayerContext
    {
        #region Components

        public Transform Transform { get; }
        public Rigidbody Rigidbody { get; }
        public CharacterController CharacterController { get; }
        public Animator Animator { get; }

        #endregion

        #region Movement Settings

        public float MoveSpeed { get; set; } = 8f;
        public float RotationSpeed { get; set; } = 10f;
        public float JumpHeight { get; set; } = 2f;
        public float JumpDuration { get; set; } = 0.5f;

        #endregion

        #region State

        public Vector3 CurrentDirection { get; private set; } = Vector3.forward;
        public Vector3 Velocity { get; set; }
        public bool IsGrounded { get; set; } = true;
        public bool IsIntroPlaying { get; set; }
        public bool FirstMoveNotified { get; private set; }

        #endregion

        #region Jump State (per-instance data for JumpingState)

        /// <summary>
        /// Current jump elapsed time. Reset on each jump.
        /// </summary>
        public float JumpTimer { get; set; }

        /// <summary>
        /// Y position when jump started.
        /// </summary>
        public float JumpStartY { get; set; }

        /// <summary>
        /// Whether player is in ascending phase of jump.
        /// </summary>
        public bool IsJumpAscending { get; set; }

        #endregion

        #region Events

        public event Action OnFirstMove;
        public event Action<Vector3> OnDirectionChanged;
        public event Action OnJump;
        public event Action OnLand;
        public event Action OnWallHit;

        #endregion

        public PlayerContext(
            Transform transform,
            Rigidbody rigidbody = null,
            CharacterController characterController = null,
            Animator animator = null)
        {
            Transform = transform;
            Rigidbody = rigidbody;
            CharacterController = characterController;
            Animator = animator;
        }

        /// <summary>
        /// Notify that the player has made their first move.
        /// Only fires once per session.
        /// </summary>
        public void NotifyFirstMove()
        {
            if (FirstMoveNotified) return;
            FirstMoveNotified = true;
            OnFirstMove?.Invoke();
        }

        /// <summary>
        /// Reset first move notification (for new session).
        /// </summary>
        public void ResetFirstMoveNotification()
        {
            FirstMoveNotified = false;
        }

        /// <summary>
        /// Set the current movement direction.
        /// Fires event if direction changed.
        /// </summary>
        public void SetDirection(Vector3 direction)
        {
            if (direction == Vector3.zero) return;

            var normalizedDirection = direction.normalized;
            if (CurrentDirection != normalizedDirection)
            {
                CurrentDirection = normalizedDirection;
                OnDirectionChanged?.Invoke(normalizedDirection);
            }
        }

        /// <summary>
        /// Fire jump event.
        /// </summary>
        public void TriggerJump()
        {
            OnJump?.Invoke();
        }

        /// <summary>
        /// Fire land event.
        /// </summary>
        public void TriggerLand()
        {
            OnLand?.Invoke();
        }

        /// <summary>
        /// Fire wall hit event.
        /// </summary>
        public void TriggerWallHit()
        {
            OnWallHit?.Invoke();
        }

        #region Animator Helpers

        public void SetAnimatorBool(string parameterName, bool value)
        {
            if (Animator != null)
            {
                Animator.SetBool(parameterName, value);
            }
        }

        public void SetAnimatorTrigger(string parameterName)
        {
            if (Animator != null)
            {
                Animator.SetTrigger(parameterName);
            }
        }

        public void SetAnimatorFloat(string parameterName, float value)
        {
            if (Animator != null)
            {
                Animator.SetFloat(parameterName, value);
            }
        }

        #endregion
    }
}
