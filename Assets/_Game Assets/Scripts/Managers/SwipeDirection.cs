using System;
using UnityEngine;

public enum SwipeDirection
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3
}

public static class SwipeDirectionHelper
{
    public static SwipeDirection CalculateSwipeDirection(Vector2 normalized)
    {
        if (Mathf.Abs(normalized.x) > Mathf.Abs(normalized.y))
        {
            return normalized.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        }

        return normalized.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
    }

    public static Vector3 SwipeDirectionToWorld(SwipeDirection swipeDirection)
    {
        return swipeDirection switch
        {
            SwipeDirection.Up => Vector3.forward,
            SwipeDirection.Down => Vector3.back,
            SwipeDirection.Left => Vector3.left,
            SwipeDirection.Right => Vector3.right,
        
            _ => throw new ArgumentOutOfRangeException(nameof(swipeDirection), swipeDirection, null)
        };
    }
}