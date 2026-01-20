using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;

public class GameplayCameraManager : MonoBehaviour
{
    public static GameplayCameraManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 1f;
    [SerializeField] private Ease transitionEase = Ease.OutCubic;

    [Header("Start Parameters")]
    [SerializeField] private Vector3 startOffset = new Vector3(0, 5, -10);
    [SerializeField] private Vector3 startRotation = new Vector3(20, 0, 0);
    [SerializeField] private float startFOV = 60f;

    [Header("Gameplay Parameters (End)")]
    [SerializeField] private Vector3 gameplayOffset = new Vector3(0, 10, -10);
    [SerializeField] private Vector3 gameplayRotation = new Vector3(45, 0, 0);
    [SerializeField] private float gameplayFOV = 60f;

    [Header("Follow Settings")]
    [SerializeField] private float followSmoothSpeed = 5f;
    [SerializeField] private bool useSmoothFollow = true;
    [SerializeField] private float horizontalDeadZone = 2.2f; // Camera X stays static if player X is within +/- this range

    // Public accessors for runtime adjustment (SRDebugger)
    public Vector3 GameplayOffset
    {
        get => gameplayOffset;
        set
        {
            gameplayOffset = value;
            _currentOffset = value; // Always update current offset
        }
    }
    
    public Vector3 GameplayRotation
    {
        get => gameplayRotation;
        set
        {
            gameplayRotation = value;
            if (targetCamera != null) 
                targetCamera.transform.rotation = Quaternion.Euler(value);
        }
    }
    
    public float GameplayFOV
    {
        get => gameplayFOV;
        set
        {
            gameplayFOV = value;
            if (targetCamera != null) 
                targetCamera.fieldOfView = value;
        }
    }


    [Header("Dependencies")]
    [SerializeField] private GameplayLogicApplier logicApplier;

    private Transform _playerTransform;
    private bool _isFollowing;
    private Tween _transitionTween;

    // Runtime state
    private Vector3 _currentOffset;
    // We track Z independently in gameplay
    private bool _useIndependentZ = false;

    private void Awake()
    {
        Instance = this;
        
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    public void Initialize(Transform player, GameplayLogicApplier logic)
    {
        _playerTransform = player;
        logicApplier = logic;
        _isFollowing = false;
        _useIndependentZ = false;
        
        // Initial State Setup
        if (targetCamera != null && _playerTransform != null)
        {
            // Set to Start configuration relative to player
            ApplyCameraState(startOffset, Quaternion.Euler(startRotation), startFOV);
            _currentOffset = startOffset; // FIX: CurrentOffset default is (0,0,0). Must init to startOffset to avoid camera snapping to inside player.
        }

        // Subscribe to events
        if (logicApplier != null)
        {
            // Transition immediately when game is ready (waiting for input)
            logicApplier.OnGameReady -= PlayOpeningTransition;
            logicApplier.OnGameReady += PlayOpeningTransition;
            
            // Start moving forward (Z) only when input is received
            logicApplier.OnRunStarted -= StartZTracking;
            logicApplier.OnRunStarted += StartZTracking;
        }
    }

    private void OnDestroy()
    {
        if (logicApplier != null)
        {
            logicApplier.OnGameReady -= PlayOpeningTransition;
            logicApplier.OnRunStarted -= StartZTracking;
        }
    }

    // Triggered by OnGameReady (immediate)
    private void PlayOpeningTransition() => PlayOpeningTransition(null);

    // Triggered by OnRunStarted (input)
    private void StartZTracking(bool isReviveResume)
    {
        // Camera starts Z tracking regardless of whether it's initial start or revive
        _useIndependentZ = true;
    }

    public void PlayOpeningTransition(Action onComplete = null)
    {
        if (targetCamera == null || _playerTransform == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Kill any existing
        _transitionTween?.Kill();

        float t = 0f;
        float delay = 0.25f; // Initial wait
        
        _currentOffset = startOffset;
        Quaternion startRotQ = Quaternion.Euler(startRotation);
        Quaternion endRotQ = Quaternion.Euler(gameplayRotation);
        
        _transitionTween = DOTween.To(() => 0f, x => t = x, 1f, transitionDuration)
            .SetDelay(delay) // Wait 0.25s before starting the lerp
            .SetEase(transitionEase)
            .OnUpdate(() =>
            {
                // Lerp values
                _currentOffset = Vector3.Lerp(startOffset, gameplayOffset, t);
                Quaternion curRot = Quaternion.Slerp(startRotQ, endRotQ, t);
                float curFOV = Mathf.Lerp(startFOV, gameplayFOV, t);

                if (targetCamera != null)
                {
                    targetCamera.transform.rotation = curRot;
                    targetCamera.fieldOfView = curFOV;
                }
            })
            .OnComplete(() =>
            {
                _isFollowing = true;
                _currentOffset = gameplayOffset;
                if (targetCamera != null)
                {
                    targetCamera.transform.rotation = Quaternion.Euler(gameplayRotation);
                    targetCamera.fieldOfView = gameplayFOV;
                }
                onComplete?.Invoke();
            });
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("[GameplayCameraManager] Test Input: Restarting Transition");
            // Stop following to reset nicely
            _isFollowing = false;
            // Snap to start temporarily
            if(_playerTransform != null) ApplyCameraState(startOffset, Quaternion.Euler(startRotation), startFOV);
            
            PlayOpeningTransition();
        }
#endif
    }

    private void LateUpdate()
    {
        // Must have targets
        if (targetCamera == null || _playerTransform == null) return;

        // Calculate Target Position
        Vector3 finalPos = targetCamera.transform.position;

        if (!_useIndependentZ)
        {
            // Fully relative to player (Pre-transition / Waiting)
             finalPos = _playerTransform.position + _currentOffset;
             
             // Fixed X during wait (usually 0)
             finalPos.x = _currentOffset.x;
        }
        else
        {
            // Gameplay Mode: Z independent, Y/X Follows Player with constraints
            
            // X Movement Logic with Deadzone
            float centerOffset = _currentOffset.x; // Usually 0 relative to path
            float playerX = _playerTransform.position.x;
            float targetX = centerOffset;

            // If player exceeds deadzone, camera drags along the excess amount
            if (playerX > horizontalDeadZone)
            {
                targetX = centerOffset + (playerX - horizontalDeadZone);
            }
            else if (playerX < -horizontalDeadZone)
            {
                targetX = centerOffset + (playerX + horizontalDeadZone);
            }
            
            // Y follows player + offset
            float targetY = _playerTransform.position.y + _currentOffset.y;
            
            // Smoothly follow
            if (useSmoothFollow)
            {
                float smoothDt = Time.deltaTime * followSmoothSpeed;
                finalPos.x = Mathf.Lerp(finalPos.x, targetX, smoothDt);
                finalPos.y = Mathf.Lerp(finalPos.y, targetY, smoothDt);
            }
            else
            {
                finalPos.x = targetX;
                finalPos.y = targetY;
            }

            // Z Movement: Driven by Gameplay Speed
            if (logicApplier != null)
            {
                finalPos.z += logicApplier.CurrentSpeed * Time.deltaTime;
            }
            else 
            {
                finalPos.z = _playerTransform.position.z + _currentOffset.z; 
            }
        }

        targetCamera.transform.position = finalPos;
    }

    private void ApplyCameraState(Vector3 offset, Quaternion rotation, float fov)
    {
        if (targetCamera != null && _playerTransform != null)
        {
            targetCamera.transform.position = _playerTransform.position + offset;
            targetCamera.transform.rotation = rotation;
            targetCamera.fieldOfView = fov;
        }
    }
}
