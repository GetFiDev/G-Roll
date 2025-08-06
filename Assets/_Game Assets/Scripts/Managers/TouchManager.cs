using System;
using Lean.Touch;
using UnityEngine;

public class TouchManager : MonoBehaviour
{
    public bool IsTouching => _activeFinger != null;

    public Action<Vector2> OnTouchBegin;
    public Action<Vector2> OnTouchMoveScreen;
    public Action<Vector2> OnTouchEnd;
    
    public Action<Vector2> OnSwipe;

    private LeanFinger _activeFinger;

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

        if (finger.Old)
            return;

        OnTouchEnd?.Invoke(finger.ScreenPosition);
    }
    
    private void OnFingerSwipe(LeanFinger finger)
    {
        Debug.Log($"Swipe Scaled Delta : {finger.SwipeScaledDelta}");
        
        OnSwipe?.Invoke(finger.SwipeScaledDelta);
    }

    private bool ValidateFinger(LeanFinger finger)
    {
        if (_activeFinger != finger)
            return true;

        return finger.IsOverGui && GameSettingsData.Instance.ignoreUITouches;
    }
}