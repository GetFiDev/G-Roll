using System;
using UnityEngine;
using UnityEngine.EventSystems;
using ISTouchPhase = UnityEngine.InputSystem.TouchPhase;
using ETouch = UnityEngine.InputSystem.EnhancedTouch;

public enum SwipeDirection { Up, Down, Left, Right }

public class TouchManager : MonoBehaviour
{
    [Header("General")] public bool ignoreUITouches = true;
    public bool singleFingerOnly = true;

    [Header("Double Tap")] [Range(0.05f, 0.5f)] public float doubleTapDuration = 0.25f;
    public float doubleTapMaxDistancePx = 40f;

    [Header("Swipe")] 
    public float swipeThresholdPx = 5f;
    [Tooltip("Continuous swipe için minimum hareket mesafesi")]
    public float continuousSwipeThresholdPx = 30f;

    public Action<Vector2> OnTouchBegin;
    public Action<Vector2> OnTouchMoveScreen;
    public Action<Vector2> OnTouchEnd;
    public Action<SwipeDirection> OnSwipe;
    public Action OnDoubleTap;

    public bool IsTouching => _activeFinger != null;

    ETouch.Finger _activeFinger;
    Vector2 _startPos;
    Vector2 _prevPos;
    Vector2 _lastSwipePos; // Son swipe pozisyonu (continuous swipe için)
    Vector2 _accumulatedDelta;
    SwipeDirection? _lastSwipeDirection; // Son swipe yönü

    float _lastTapTime = -999f;
    Vector2 _lastTapPos;

    void OnEnable()
    {
        if (!ETouch.EnhancedTouchSupport.enabled)
            ETouch.EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
        ETouch.TouchSimulation.Enable();
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        ETouch.TouchSimulation.Disable();
#endif
        if (ETouch.EnhancedTouchSupport.enabled)
            ETouch.EnhancedTouchSupport.Disable();
    }

    [SerializeField] private PlayerMovement playerMovement;

    public void BindPlayer(PlayerMovement pm)  => playerMovement = pm;
    public void UnbindPlayer()                 => playerMovement = null;

    void Update()
    {
        // Meta/Requesting fazında input işleme
        if (GameManager.Instance == null || GameManager.Instance.current != GamePhase.Gameplay)
            return;

#if !DISABLE_SRDEBUGGER
        // SRDebugger açıkken input'u blokla
        if (SRDebug.Instance != null && SRDebug.Instance.IsDebugPanelVisible)
            return;
#endif

        // Destroy edilmiş Unity objesi "== null" döner (fake null)
        if (playerMovement == null || !playerMovement || !playerMovement.isActiveAndEnabled)
            return;

#if UNITY_EDITOR
        // WASD keyboard support for editor testing
        HandleKeyboardInput();
#endif

        HandleTouchInput();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor'de WASD tuşları ile swipe emülasyonu
    /// </summary>
    void HandleKeyboardInput()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
        {
            TriggerSwipe(SwipeDirection.Up);
        }
        else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
        {
            TriggerSwipe(SwipeDirection.Down);
        }
        else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            TriggerSwipe(SwipeDirection.Left);
        }
        else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
        {
            TriggerSwipe(SwipeDirection.Right);
        }
    }
    
    void TriggerSwipe(SwipeDirection dir)
    {
        // Pause-resume check
        var logicApplier = FindObjectOfType<GameplayLogicApplier>();
        if (logicApplier != null && logicApplier.IsPausedWaitingForInput)
        {
            logicApplier.OnFirstInputAfterPauseResume();
        }
        
        _lastSwipeDirection = dir;
        OnSwipe?.Invoke(dir);
    }
#endif

    void HandleTouchInput()
    {
        var touches = ETouch.Touch.activeTouches;
        if (touches.Count == 0 && _activeFinger == null) return;

        if (_activeFinger == null)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (ignoreUITouches && IsOverUI(t)) continue;

                if (t.phase == ISTouchPhase.Began)
                {
                    if (singleFingerOnly && _activeFinger != null) break;

                    _activeFinger = t.finger;
                    _startPos = t.screenPosition;
                    _prevPos = _startPos;
                    _lastSwipePos = _startPos;
                    _accumulatedDelta = Vector2.zero;
                    _lastSwipeDirection = null;
                    OnTouchBegin?.Invoke(_startPos);
                    break;
                }
            }
            return;
        }

        ETouch.Touch? maybe = null;
        for (int i = 0; i < touches.Count; i++)
        {
            if (touches[i].finger == _activeFinger)
            {
                maybe = touches[i];
                break;
            }
        }

        if (maybe.HasValue)
        {
            var t = maybe.Value;
            if (ignoreUITouches && IsOverUI(t)) return;

            var delta = t.screenPosition - _prevPos;
            if (delta.sqrMagnitude > 0.001f)
            {
                _accumulatedDelta += delta;
                OnTouchMoveScreen?.Invoke(delta);
                _prevPos = t.screenPosition;
                
                // Continuous swipe detection - parmağı kaldırmadan yön değiştirme
                TryDetectContinuousSwipe(t.screenPosition);
            }

            if (t.phase == ISTouchPhase.Canceled || t.phase == ISTouchPhase.Ended)
            {
                HandleDoubleTap(t.screenPosition);
                OnTouchEnd?.Invoke(t.screenPosition);

                _activeFinger = null;
                _accumulatedDelta = Vector2.zero;
                _lastSwipeDirection = null;
            }
        }
        else
        {
            HandleDoubleTap(_prevPos);
            OnTouchEnd?.Invoke(_prevPos);
            _activeFinger = null;
            _accumulatedDelta = Vector2.zero;
            _lastSwipeDirection = null;
        }
    }

    void HandleDoubleTap(Vector2 upPos)
    {
        if (!playerMovement) return;
        
        var now = Time.unscaledTime;
        float dt = now - _lastTapTime;
        bool closeInTime = dt <= doubleTapDuration;
        bool closeInSpace = (_lastTapPos - upPos).magnitude <= doubleTapMaxDistancePx;

        if (closeInTime && closeInSpace)
        {
            OnDoubleTap?.Invoke();
            _lastTapTime = -999f;
        }
        else
        {
            _lastTapTime = now;
            _lastTapPos = upPos;
        }
    }

    /// <summary>
    /// Continuous swipe: Parmağı kaldırmadan hareket ederken yön değişikliği algıla
    /// - Son swipe pozisyonundan yeterince uzaklaştıysa
    /// - Ve yön farklıysa yeni swipe tetikle
    /// </summary>
    void TryDetectContinuousSwipe(Vector2 currentPos)
    {
        if (!playerMovement) return;
        
        // Pause-resume check
        var logicApplier = FindObjectOfType<GameplayLogicApplier>();
        if (logicApplier != null && logicApplier.IsPausedWaitingForInput)
        {
            var totalFromStart = currentPos - _startPos;
            if (totalFromStart.magnitude >= swipeThresholdPx)
            {
                logicApplier.OnFirstInputAfterPauseResume();
            }
        }
        
        // Son swipe pozisyonundan mesafe
        Vector2 fromLastSwipe = currentPos - _lastSwipePos;
        float distanceFromLast = fromLastSwipe.magnitude;
        
        // İlk swipe henüz yapılmadıysa, daha düşük threshold kullan
        float threshold = _lastSwipeDirection.HasValue ? continuousSwipeThresholdPx : swipeThresholdPx;
        
        if (distanceFromLast < threshold) return;
        
        // Yön hesapla
        SwipeDirection newDir = DetectDirection(fromLastSwipe);
        
        // FARKLI yön ise VEYA ilk swipe ise tetikle
        if (!_lastSwipeDirection.HasValue || newDir != _lastSwipeDirection.Value)
        {
            _lastSwipeDirection = newDir;
            _lastSwipePos = currentPos;
            OnSwipe?.Invoke(newDir);
        }
        else
        {
            // Aynı yönde devam ediyoruz, pozisyonu güncelle
            _lastSwipePos = currentPos;
        }
    }

    bool IsOverUI(ETouch.Touch t)
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(t.finger.index);
    }

    static SwipeDirection DetectDirection(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return v.x > 0f ? SwipeDirection.Right : SwipeDirection.Left;
        else
            return v.y > 0f ? SwipeDirection.Up : SwipeDirection.Down;
    }
}