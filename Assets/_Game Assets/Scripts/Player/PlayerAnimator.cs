using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private PlayerController _playerController;
    
    public PlayerAnimator Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        
        return this;
    }

    private const float BallRadius = 0.5f; 
    private Vector3 lastPosition;
    private void Update()
    {
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        var delta = transform.position - lastPosition;
        
        if (delta == Vector3.zero)
            return;

        var distance = delta.magnitude;
        var angle = distance / BallRadius * Mathf.Rad2Deg;

        var rotationAxis = Vector3.Cross(delta.normalized, Vector3.forward);

        transform.Rotate(rotationAxis, angle, Space.World);
        lastPosition = transform.position;
    }
    
    private struct AnimatorParameterKey
    {
        
    }
}
