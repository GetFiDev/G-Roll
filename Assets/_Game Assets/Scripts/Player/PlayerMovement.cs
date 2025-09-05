using System.Collections;
using System.Linq;
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
    [SerializeField] private float boosterCollectRange = 2f;

    [Header("Jump Options")]
    [SerializeField] private float doubleTapJumpForce = 3f;

    private PlayerController _playerController;

    public PlayerMovement Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        Speed = movementSpeed;

        return this;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.25f);

        GameManager.Instance.touchManager.OnSwipe += ChangeDirection;
        GameManager.Instance.touchManager.OnDoubleTap += () => Jump(doubleTapJumpForce);
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

    public void Teleport(Vector3 enterPosition, Vector3 teleportPosition)
    {
        StartCoroutine(TeleportCoroutine(enterPosition, teleportPosition));
    }

    private float _lastSpeed = 0;

    private IEnumerator TeleportCoroutine(Vector3 enterPosition, Vector3 teleportPosition)
    {
        _lastSpeed = Speed;
        Speed = 0f;

        yield return transform.DOJump(enterPosition + Vector3.down, 2f, 1, .35f).WaitForCompletion();

        transform.position = teleportPosition + Vector3.down;

        yield return transform.DOJump(teleportPosition + _movementDirection, 2f, 1, .35f).WaitForCompletion();

        Speed = _lastSpeed;
    }

    private Coroutine _jumpCoroutine;

    public void Jump(float jumpForce)
    {
        if (_jumpCoroutine != null || GameManager.Instance.GameState != GameState.Gameplay)
            return;

        _jumpCoroutine = StartCoroutine(JumpCoroutine(jumpForce));
    }

    private IEnumerator JumpCoroutine(float jumpForce)
    {
        yield return transform.DOMoveY(jumpForce, .35f).WaitForCompletion();

        yield return transform.DOMoveY(0f, 1f).WaitForCompletion();

        _jumpCoroutine = null;
    }

    public void Boost(float boosterValue)
    {
        StartCoroutine(BoosterCoroutine(boosterValue));
    }

    private IEnumerator BoosterCoroutine(float boosterValue)
    {
        Speed += boosterSpeed;
        _lastSpeed += boosterSpeed;

        var timeLeft = boosterDuration;
        var coinList = GameManager.Instance.levelManager.currentLevel.Coins;

        while (timeLeft > 0)
        {
            timeLeft -= Time.deltaTime;

            yield return null;

            foreach (var coin in coinList.Where(coin =>
                         Vector3.Distance(transform.position, coin.transform.position) < boosterCollectRange))
            {
                coin.CollectByMagnet(transform);
                coinList.Remove(coin);

                break;
            }
        }

        Speed -= boosterSpeed;
        _lastSpeed -= boosterSpeed;
    }
}