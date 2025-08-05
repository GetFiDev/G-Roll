using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public bool IsMoving => _movementDirection.magnitude > 0.1f;
    
    [SerializeField] private float movementSpeed = 5f;

    private PlayerController _playerController;

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
        }
    }

    private Vector3 _movementDirection = Vector3.zero;

    private void Move()
    {
        transform.position += _movementDirection * (Time.deltaTime * movementSpeed);
    }

    private void ChangeDirection(SwipeDirection swipeDirection)
    {
        _movementDirection = SwipeDirectionHelper.SwipeDirectionToWorld(swipeDirection);
    }
}