using System.Collections;
using DG.Tweening;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public bool IsMoving => GameManager.Instance.GameState == GameState.Gameplay && _movementDirection.magnitude > 0.1f;

    [SerializeField] private float drag = 5f;
    [SerializeField] private float forceMultiplier = 0.000002f;

    private PlayerController _playerController;
    private float _activeSpeed = 1f;

    public PlayerMovement Initialize(PlayerController playerController)
    {
        _playerController = playerController;

        return this;
    }

    private void Start()
    {
        GameManager.Instance.touchManager.OnSwipe += ChangeDirection;
    }

    private void Update()
    {
        if (GameManager.Instance.GameState == GameState.Gameplay)
        {
            Move();

            Debug.Log($"Movement Direction: {_movementDirection}");
        }
    }

    private Vector3 _movementDirection = Vector3.zero;

    private void Move()
    {
        _movementDirection = Vector3.Lerp(_movementDirection, Vector3.zero, Time.deltaTime * drag);

        transform.position += _movementDirection * (Time.deltaTime * _activeSpeed);
    }

    private void ChangeDirection(Vector2 swipeDirection)
    {
        var newForce = swipeDirection.ToDirectionVector().normalized;
        newForce *= swipeDirection.sqrMagnitude * forceMultiplier;

        _movementDirection += newForce;
    }

    public void ChangeSpeed(float changeAmount)
    {
        _activeSpeed += changeAmount;
    }

    public void Teleport(Vector3 teleportPosition)
    {
        StartCoroutine(TeleportCoroutine(teleportPosition));
    }

    private IEnumerator TeleportCoroutine(Vector3 teleportPosition)
    {
        var lastSpeed = _activeSpeed;
        _activeSpeed = 0f;

        yield return transform.DOScale(Vector3.zero, .2f).SetEase(Ease.InBack).WaitForCompletion();

        transform.position = teleportPosition;

        yield return transform.DOScale(Vector3.one, .2f).SetEase(Ease.OutBack).WaitForCompletion();

        _activeSpeed = lastSpeed;
    }
}