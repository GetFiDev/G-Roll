using System;
using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.StateMachine;
using GRoll.Gameplay.Player.StateMachine.States;
using UnityEngine;

namespace GRoll.Gameplay.Player.Input
{
    /// <summary>
    /// Handles touch/mouse input and converts to PlayerInputData.
    /// Detects swipes, taps, and double taps.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float swipeThreshold = 50f;
        [SerializeField] private float tapMaxDuration = 0.3f;
        [SerializeField] private float doubleTapMaxInterval = 0.3f;

        [Header("References")]
        [SerializeField] private PlayerController playerController;

        private Vector2 _touchStartPosition;
        private float _touchStartTime;
        private float _lastTapTime;
        private bool _isTouching;

        public event Action<PlayerInputData> OnInputDetected;

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        private void HandleMouseInput()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _touchStartPosition = UnityEngine.Input.mousePosition;
                _touchStartTime = Time.time;
                _isTouching = true;
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0) && _isTouching)
            {
                _isTouching = false;
                ProcessTouchEnd(UnityEngine.Input.mousePosition);
            }
            else if (_isTouching)
            {
                // Check for swipe during drag
                var currentPosition = (Vector2)UnityEngine.Input.mousePosition;
                var delta = currentPosition - _touchStartPosition;

                if (delta.magnitude >= swipeThreshold)
                {
                    _isTouching = false;
                    ProcessSwipe(delta);
                }
            }
        }

        private void HandleTouchInput()
        {
            if (UnityEngine.Input.touchCount == 0) return;

            var touch = UnityEngine.Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _touchStartPosition = touch.position;
                    _touchStartTime = Time.time;
                    _isTouching = true;
                    break;

                case TouchPhase.Moved:
                    if (_isTouching)
                    {
                        var delta = touch.position - _touchStartPosition;
                        if (delta.magnitude >= swipeThreshold)
                        {
                            _isTouching = false;
                            ProcessSwipe(delta);
                        }
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (_isTouching)
                    {
                        _isTouching = false;
                        ProcessTouchEnd(touch.position);
                    }
                    break;
            }
        }

        private void ProcessTouchEnd(Vector2 endPosition)
        {
            var delta = endPosition - _touchStartPosition;
            var duration = Time.time - _touchStartTime;

            // Swipe
            if (delta.magnitude >= swipeThreshold)
            {
                ProcessSwipe(delta);
                return;
            }

            // Tap (short touch without much movement)
            if (duration <= tapMaxDuration)
            {
                var timeSinceLastTap = Time.time - _lastTapTime;

                if (timeSinceLastTap <= doubleTapMaxInterval)
                {
                    // Double tap
                    _lastTapTime = 0f;
                    EmitInput(PlayerInputData.DoubleTap());
                }
                else
                {
                    // Single tap
                    _lastTapTime = Time.time;
                    EmitInput(PlayerInputData.Tap());
                }
            }
        }

        private void ProcessSwipe(Vector2 delta)
        {
            // Convert screen delta to world direction
            // For a top-down or isometric view, this maps differently
            var direction = GetWorldDirection(delta);
            EmitInput(PlayerInputData.Swipe(direction));
        }

        /// <summary>
        /// Convert screen-space swipe to world-space direction.
        /// Assumes camera is looking down at an angle.
        /// </summary>
        private Vector3 GetWorldDirection(Vector2 screenDelta)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                // Fallback: simple mapping
                return new Vector3(screenDelta.x, 0, screenDelta.y).normalized;
            }

            // Get camera forward/right on XZ plane
            var cameraForward = camera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            var cameraRight = camera.transform.right;
            cameraRight.y = 0;
            cameraRight.Normalize();

            // Map screen delta to world direction
            var worldDirection = cameraRight * screenDelta.x + cameraForward * screenDelta.y;
            return worldDirection.normalized;
        }

        private void EmitInput(PlayerInputData input)
        {
            OnInputDetected?.Invoke(input);

            if (playerController != null)
            {
                playerController.ProcessInput(input);
            }
        }

        /// <summary>
        /// Manually inject input (for triggers, AI, etc).
        /// </summary>
        public void InjectInput(PlayerInputData input)
        {
            EmitInput(input);
        }
    }
}
