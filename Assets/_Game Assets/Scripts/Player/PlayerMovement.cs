using System.Collections;
using DG.Tweening;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public bool IsMoving => GameManager.Instance.GameState == GameState.Gameplay && _movementDirection.magnitude > 0.1f;
    public float Speed { get; private set; }

    [SerializeField] private float movementSpeed = 5f;

    [Header("Booster Options")]
    [SerializeField] private float boosterDuration = 10f;

    [SerializeField] private float boosterSpeed = 1f;

    private PlayerController _playerController;

    public PlayerMovement Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        Speed = movementSpeed;

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
        transform.position += _movementDirection * (Time.deltaTime * Speed);
    }

    private void ChangeDirection(SwipeDirection swipeDirection)
    {
        _movementDirection = SwipeDirectionHelper.SwipeDirectionToWorld(swipeDirection);
    }

    public void ChangeSpeed(float changeAmount)
    {
        Speed += changeAmount;
    }

    public void Teleport(Vector3 teleportPosition)
    {
        StartCoroutine(TeleportCoroutine(teleportPosition));
    }

    private float _lastSpeed = 0;

    private IEnumerator TeleportCoroutine(Vector3 teleportPosition)
    {
        _lastSpeed = Speed;
        Speed = 0f;

        yield return transform.DOScale(Vector3.zero, .2f).SetEase(Ease.InBack).WaitForCompletion();

        transform.position = teleportPosition;

        yield return transform.DOScale(Vector3.one, .2f).SetEase(Ease.OutBack).WaitForCompletion();

        Speed = _lastSpeed;
    }

    public void Jump(float jumpForce)
    {
        StartCoroutine(JumpCoroutine(jumpForce));
    }

    private IEnumerator JumpCoroutine(float jumpForce)
    {
        yield return transform.DOMoveY(jumpForce, .1f).WaitForCompletion();

        yield return transform.DOMoveY(0f, 1f).WaitForCompletion();
    }

    public void Boost(float boosterValue)
    {
        StartCoroutine(BoosterCoroutine(boosterValue));
    }

    private IEnumerator BoosterCoroutine(float boosterValue)
    {
        Speed += boosterSpeed;
        _lastSpeed += boosterSpeed;

        yield return new WaitForSeconds(boosterDuration);
        
        Speed -= boosterSpeed;
        _lastSpeed -= boosterSpeed;
    }
}