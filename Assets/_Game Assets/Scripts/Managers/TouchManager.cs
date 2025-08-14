using System;
using Lean.Touch;
using Sirenix.OdinInspector;
using UnityEngine;

public class TouchManager : MonoBehaviour
{
    public bool IsTouching => _activeFinger != null;
    [SerializeField] private float doubleTapDuration = 0.2f;
    
    public Action<Vector2> OnTouchBegin;
    public Action<Vector2> OnTouchMoveScreen;
    public Action<Vector2> OnTouchEnd;
    
    public Action<SwipeDirection> OnSwipe;
    public Action OnDoubleTap;
    
    [ShowInInspector, ReadOnly] private LeanFinger _activeFinger;
    private float _doubleTapTimer;

    public TouchManager Initialize()
    {
        return this;
    }

    private void OnEnable()
    {
        LeanTouch.OnFingerDown += OnFingerDown;
        LeanTouch.OnFingerUpdate += OnFingerUpdate;
        LeanTouch.OnFingerUp += OnFingerUp;

        LeanTouch.OnFingerSwipe += OnFingerSwipe;
    }

    private void OnDisable()
    {
        LeanTouch.OnFingerDown -= OnFingerDown;
        LeanTouch.OnFingerUpdate -= OnFingerUpdate;
        LeanTouch.OnFingerUp -= OnFingerUp;
        
        LeanTouch.OnFingerSwipe -= OnFingerSwipe;
    }

    private void OnFingerDown(LeanFinger finger)
    {
        if (ValidateFinger(finger)) 
            return;

        _activeFinger = finger;
        
        OnTouchBegin?.Invoke(finger.ScreenPosition);
    }

    private void OnFingerUpdate(LeanFinger finger)
    {
        if (ValidateFinger(finger)) 
            return;

        OnTouchMoveScreen?.Invoke(finger.ScaledDelta);
    }

    private void OnFingerUp(LeanFinger finger)
    {
        if (ValidateFinger(finger)) 
            return;

        _activeFinger = null;

        if (_doubleTapTimer < Time.time)
        {
            _doubleTapTimer = Time.time + doubleTapDuration;
        }
        else
        {
            OnDoubleTap?.Invoke();
            _doubleTapTimer = 0f;
        }

        if (finger.Old)
            return;
        
        OnTouchEnd?.Invoke(finger.ScreenPosition);
    }
    
    private void OnFingerSwipe(LeanFinger finger)
    {
        var swipeDirection = SwipeDirectionHelper.CalculateSwipeDirection(finger.SwipeScaledDelta.normalized);
        
        OnSwipe?.Invoke(swipeDirection);
    }

    private bool ValidateFinger(LeanFinger finger)
    {
        if (_activeFinger != finger)
            return false;

        return finger.IsOverGui && GameSettingsData.Instance.ignoreUITouches;
    }
}