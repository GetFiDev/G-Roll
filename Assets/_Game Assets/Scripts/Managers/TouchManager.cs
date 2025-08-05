using System;
using Lean.Touch;
using UnityEngine;

public class TouchManager : MonoBehaviour
{
    public bool IsTouching = false;
    public bool IsTouchingOverUI = false;
    public float TouchDistance = 1f;

    public Action<Vector2> OnTouchBegin;
    public Action<Vector2, Vector3> OnTouchMoveWorld;
    public Action<Vector2, Vector2> OnTouchMoveScreen;
    public Action<Vector2> OnTouchEnd;
    public Action<Vector2> OnOverUITouchBegin;
    public Action<Vector2, Vector3> OnOverUITouchMoveWorld;
    public Action<Vector2, Vector2> OnOverUITouchMoveScreen;
    public Action<Vector2> OnOverUITouchEnd;
    
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
    }
    
    private void OnDisable()
    {
        LeanTouch.OnFingerDown -= OnFingerDown;
        LeanTouch.OnFingerUpdate -= OnFingerUpdate;
        LeanTouch.OnFingerUp -= OnFingerUp;
    }
    
    private void OnFingerDown(LeanFinger finger)
    {
        if (_activeFinger is not null)
            return;
        
        _activeFinger = finger;
        IsTouchingOverUI = true;
        
        OnOverUITouchBegin?.Invoke(finger.ScreenPosition);
        
        if (GameSettingsData.Instance.ignoreUITouches && finger.IsOverGui)
            return;

        IsTouching = true;
        OnTouchBegin?.Invoke(finger.ScreenPosition);
    }
    
    private void OnFingerUpdate(LeanFinger finger)
    {
        if (finger != _activeFinger)
            return;
        
        OnOverUITouchMoveWorld?.Invoke(finger.ScreenPosition, finger.GetWorldDelta(TouchDistance));
        OnOverUITouchMoveScreen?.Invoke(finger.ScreenPosition, finger.ScaledDelta);
        
        if (GameSettingsData.Instance.ignoreUITouches && finger.IsOverGui)
            return;

        OnTouchMoveWorld?.Invoke(finger.ScreenPosition, finger.GetWorldDelta(TouchDistance));
        OnTouchMoveScreen?.Invoke(finger.ScreenPosition, finger.ScaledDelta);
    }

    private void OnFingerUp(LeanFinger finger)
    {
        if (_activeFinger != finger)
            return;
        
        _activeFinger = null;
        IsTouchingOverUI = false;
        
        OnOverUITouchEnd?.Invoke(finger.ScreenPosition);
        
        if (GameSettingsData.Instance.ignoreUITouches && finger.IsOverGui)
            return;

        IsTouching = false;
        OnTouchEnd?.Invoke(finger.ScreenPosition);
    }
}