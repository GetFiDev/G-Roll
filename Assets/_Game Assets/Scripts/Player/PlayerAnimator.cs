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
    private const float TurnSpeed = 720f;

    private Vector3 lastPosition;

    private void Update()
    {
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        var delta = transform.position - lastPosition;
        delta.y = 0;

        if (delta.sqrMagnitude < 0.0001f)
            return;


        var targetDirection = delta.normalized;

        if (targetDirection != Vector3.zero)
        {
            var targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation =
                Quaternion.RotateTowards(transform.rotation, targetRotation, TurnSpeed * Time.deltaTime);
        }

        var distance = delta.magnitude;
        var angle = distance / BallRadius * Mathf.Rad2Deg;

        var rotationAxis = transform.right;

        ballTransform.Rotate(rotationAxis, angle, Space.World);

        lastPosition = transform.position;
    }

    private struct AnimatorParameterKey
    {
    }
}