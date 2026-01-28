using System;
using DG.Tweening;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Services;
using UnityEngine;
using VContainer;

namespace GRoll.Gameplay.Camera
{
    /// <summary>
    /// Gameplay kamera yonetimi servisi implementasyonu.
    /// Eski GameplayCameraManager'in yerini alir.
    /// </summary>
    public class CameraService : ICameraService
    {
        #region Dependencies

        private readonly IGameplaySpeedService _speedService;
        private readonly IMessageBus _messageBus;

        #endregion

        #region Configuration

        // Start parameters
        private Vector3 _startOffset = new Vector3(0, 5, -10);
        private Vector3 _startRotation = new Vector3(20, 0, 0);
        private float _startFOV = 60f;

        // Gameplay parameters
        private Vector3 _gameplayOffset = new Vector3(0, 10, -10);
        private Vector3 _gameplayRotation = new Vector3(45, 0, 0);
        private float _gameplayFOV = 60f;

        // Follow settings
        private float _followSmoothSpeed = 5f;
        private float _horizontalDeadZone = 2.2f;
        private bool _useSmoothFollow = true;

        // Transition settings
        private float _transitionDuration = 1f;
        private float _transitionDelay = 0.25f;
        private Ease _transitionEase = Ease.OutCubic;

        #endregion

        #region State

        private UnityEngine.Camera _camera;
        private Transform _playerTransform;
        private bool _isFollowing;
        private bool _isTransitioning;
        private bool _useIndependentZ;
        private Vector3 _currentOffset;
        private Tween _transitionTween;
        private IDisposable _firstMoveSubscription;

        #endregion

        #region Properties

        public Vector3 GameplayOffset
        {
            get => _gameplayOffset;
            set
            {
                _gameplayOffset = value;
                _currentOffset = value;
            }
        }

        public Vector3 GameplayRotation
        {
            get => _gameplayRotation;
            set
            {
                _gameplayRotation = value;
                if (_camera != null)
                    _camera.transform.rotation = Quaternion.Euler(value);
            }
        }

        public float GameplayFOV
        {
            get => _gameplayFOV;
            set
            {
                _gameplayFOV = value;
                if (_camera != null)
                    _camera.fieldOfView = value;
            }
        }

        public float HorizontalDeadZone
        {
            get => _horizontalDeadZone;
            set => _horizontalDeadZone = value;
        }

        public float FollowSmoothSpeed
        {
            get => _followSmoothSpeed;
            set => _followSmoothSpeed = value;
        }

        public bool IsFollowing => _isFollowing;
        public bool IsTransitioning => _isTransitioning;
        public bool IsZTrackingActive => _useIndependentZ;

        #endregion

        #region Events

        public event Action OnTransitionCompleted;
        public event Action OnZTrackingStarted;

        #endregion

        #region Constructor

        [Inject]
        public CameraService(IGameplaySpeedService speedService, IMessageBus messageBus)
        {
            _speedService = speedService;
            _messageBus = messageBus;
        }

        #endregion

        #region Initialization

        public void Initialize(Transform playerTransform)
        {
            Initialize(playerTransform, UnityEngine.Camera.main);
        }

        public void Initialize(Transform playerTransform, UnityEngine.Camera camera)
        {
            _playerTransform = playerTransform;
            _camera = camera;
            _isFollowing = false;
            _useIndependentZ = false;
            _isTransitioning = false;

            // Initial state setup
            if (_camera != null && _playerTransform != null)
            {
                ApplyCameraState(_startOffset, Quaternion.Euler(_startRotation), _startFOV);
                _currentOffset = _startOffset;
            }

            // Subscribe to first player move
            _firstMoveSubscription?.Dispose();
            _firstMoveSubscription = _messageBus?.Subscribe<PlayerFirstMoveMessage>(OnPlayerFirstMove);

            // Also subscribe to speed service event for compatibility
            if (_speedService != null)
            {
                _speedService.OnFirstPlayerMove -= OnFirstPlayerMoveFromService;
                _speedService.OnFirstPlayerMove += OnFirstPlayerMoveFromService;
            }
        }

        private void OnPlayerFirstMove(PlayerFirstMoveMessage message)
        {
            StartZTracking();
        }

        private void OnFirstPlayerMoveFromService()
        {
            StartZTracking();
        }

        #endregion

        #region Transition

        public void PlayOpeningTransition(Action onComplete = null)
        {
            if (_camera == null || _playerTransform == null)
            {
                onComplete?.Invoke();
                return;
            }

            _transitionTween?.Kill();
            _isTransitioning = true;

            float t = 0f;
            _currentOffset = _startOffset;
            Quaternion startRotQ = Quaternion.Euler(_startRotation);
            Quaternion endRotQ = Quaternion.Euler(_gameplayRotation);

            _messageBus?.Publish(new CameraTransitionStartedMessage("Opening"));

            _transitionTween = DOTween.To(() => 0f, x => t = x, 1f, _transitionDuration)
                .SetDelay(_transitionDelay)
                .SetEase(_transitionEase)
                .OnUpdate(() =>
                {
                    _currentOffset = Vector3.Lerp(_startOffset, _gameplayOffset, t);
                    Quaternion curRot = Quaternion.Slerp(startRotQ, endRotQ, t);
                    float curFOV = Mathf.Lerp(_startFOV, _gameplayFOV, t);

                    if (_camera != null)
                    {
                        _camera.transform.rotation = curRot;
                        _camera.fieldOfView = curFOV;
                    }
                })
                .OnComplete(() =>
                {
                    _isFollowing = true;
                    _isTransitioning = false;
                    _currentOffset = _gameplayOffset;

                    if (_camera != null)
                    {
                        _camera.transform.rotation = Quaternion.Euler(_gameplayRotation);
                        _camera.fieldOfView = _gameplayFOV;
                    }

                    OnTransitionCompleted?.Invoke();
                    _messageBus?.Publish(new CameraTransitionCompletedMessage());
                    onComplete?.Invoke();
                });
        }

        #endregion

        #region Z Tracking

        public void StartZTracking()
        {
            if (_useIndependentZ) return;

            _useIndependentZ = true;
            OnZTrackingStarted?.Invoke();
            _messageBus?.Publish(new CameraZTrackingStartedMessage());
        }

        public void StopZTracking()
        {
            _useIndependentZ = false;
            _messageBus?.Publish(new CameraZTrackingStoppedMessage());
        }

        #endregion

        #region Update

        public void Tick(float deltaTime)
        {
            if (_camera == null || _playerTransform == null) return;

            Vector3 finalPos = _camera.transform.position;

            if (!_useIndependentZ)
            {
                // Fully relative to player (pre-transition / waiting)
                finalPos = _playerTransform.position + _currentOffset;
                finalPos.x = _currentOffset.x;
            }
            else
            {
                // Gameplay mode: Z independent, Y/X follows player with constraints
                float centerOffset = _currentOffset.x;
                float playerX = _playerTransform.position.x;
                float targetX = centerOffset;

                // Horizontal deadzone logic
                if (playerX > _horizontalDeadZone)
                {
                    targetX = centerOffset + (playerX - _horizontalDeadZone);
                }
                else if (playerX < -_horizontalDeadZone)
                {
                    targetX = centerOffset + (playerX + _horizontalDeadZone);
                }

                // Y follows player + offset
                float targetY = _playerTransform.position.y + _currentOffset.y;

                // Smooth follow
                if (_useSmoothFollow)
                {
                    float smoothDt = deltaTime * _followSmoothSpeed;
                    finalPos.x = Mathf.Lerp(finalPos.x, targetX, smoothDt);
                    finalPos.y = Mathf.Lerp(finalPos.y, targetY, smoothDt);
                }
                else
                {
                    finalPos.x = targetX;
                    finalPos.y = targetY;
                }

                // Z movement driven by gameplay speed
                if (_speedService != null)
                {
                    finalPos.z += _speedService.CurrentSpeed * deltaTime;
                }
                else
                {
                    finalPos.z = _playerTransform.position.z + _currentOffset.z;
                }
            }

            _camera.transform.position = finalPos;
        }

        #endregion

        #region Reset

        public void Reset()
        {
            _transitionTween?.Kill();
            _isFollowing = false;
            _isTransitioning = false;
            _useIndependentZ = false;
            _currentOffset = _startOffset;

            if (_camera != null && _playerTransform != null)
            {
                ApplyCameraState(_startOffset, Quaternion.Euler(_startRotation), _startFOV);
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Start konfigurasyonunu ayarla
        /// </summary>
        public void ConfigureStart(Vector3 offset, Vector3 rotation, float fov)
        {
            _startOffset = offset;
            _startRotation = rotation;
            _startFOV = fov;
        }

        /// <summary>
        /// Gameplay konfigurasyonunu ayarla
        /// </summary>
        public void ConfigureGameplay(Vector3 offset, Vector3 rotation, float fov)
        {
            _gameplayOffset = offset;
            _gameplayRotation = rotation;
            _gameplayFOV = fov;
        }

        /// <summary>
        /// Transition ayarlarini yapilandir
        /// </summary>
        public void ConfigureTransition(float duration, float delay, Ease ease)
        {
            _transitionDuration = duration;
            _transitionDelay = delay;
            _transitionEase = ease;
        }

        /// <summary>
        /// Follow ayarlarini yapilandir
        /// </summary>
        public void ConfigureFollow(float smoothSpeed, float horizontalDeadZone, bool useSmoothFollow)
        {
            _followSmoothSpeed = smoothSpeed;
            _horizontalDeadZone = horizontalDeadZone;
            _useSmoothFollow = useSmoothFollow;
        }

        #endregion

        #region Helpers

        private void ApplyCameraState(Vector3 offset, Quaternion rotation, float fov)
        {
            if (_camera != null && _playerTransform != null)
            {
                _camera.transform.position = _playerTransform.position + offset;
                _camera.transform.rotation = rotation;
                _camera.fieldOfView = fov;
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _transitionTween?.Kill();
            _firstMoveSubscription?.Dispose();

            if (_speedService != null)
            {
                _speedService.OnFirstPlayerMove -= OnFirstPlayerMoveFromService;
            }
        }

        #endregion
    }
}
