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

    [Header("Swipe")] public float swipeThresholdPx = 5f;

    public Action<Vector2> OnTouchBegin;
    public Action<Vector2> OnTouchMoveScreen;
    public Action<Vector2> OnTouchEnd;
    public Action<SwipeDirection> OnSwipe;
    public Action OnDoubleTap;

    public bool IsTouching => _activeFinger != null;

    ETouch.Finger _activeFinger;
    Vector2 _startPos;
    Vector2 _prevPos;
    Vector2 _accumulatedDelta;

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

        // Destroy edilmiş Unity objesi "== null" döner (fake null)
        if (playerMovement == null || !playerMovement || !playerMovement.isActiveAndEnabled)
            return;

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
                    _accumulatedDelta = Vector2.zero;
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
            }

            if (t.phase == ISTouchPhase.Canceled || t.phase == ISTouchPhase.Ended)
            {
                HandleDoubleTap(t.screenPosition);
                HandleSwipe(t.screenPosition);
                OnTouchEnd?.Invoke(t.screenPosition);

                _activeFinger = null;
                _accumulatedDelta = Vector2.zero;
            }
        }
        else
        {
            HandleDoubleTap(_prevPos);
            HandleSwipe(_prevPos);
            OnTouchEnd?.Invoke(_prevPos);
            _activeFinger = null;
            _accumulatedDelta = Vector2.zero;
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

    void HandleSwipe(Vector2 currentPos)
    {
        if (!playerMovement) return;

        var total = currentPos - _startPos;
        if (total.magnitude < swipeThresholdPx) return;

        var dir = DetectDirection(total);
        OnSwipe?.Invoke(dir);
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