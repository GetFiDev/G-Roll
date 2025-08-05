using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Transform ballTransform;

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
        var delta = lastPosition - transform.position;

        delta.y = 0;

        if (delta.sqrMagnitude < 0.0001f)
            return;

        var distance = delta.magnitude;
        var angle = distance / BallRadius * Mathf.Rad2Deg;

        var rotationAxis = Vector3.Cross(delta.normalized, Vector3.up);

        ballTransform.Rotate(rotationAxis, angle, Space.World);

        lastPosition = transform.position;
    }

    private struct AnimatorParameterKey
    {
    }
}