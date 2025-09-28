using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public bool IsMoving => GameManager.Instance.GameState == GameState.GameplayRun && _movementDirection.magnitude > 0.1f;
    public float Speed { get; private set; }
    public bool IsJumping { get; private set; } = false;

    [SerializeField] private float movementSpeed = 5f;
    [Header("Jump Options")]
    [SerializeField] private float doubleTapJumpForce = 3f;
    [SerializeField] private float baseAirTime = 0.9f;
    [SerializeField] private float referenceSpeed = 5f;               // bu hızda baseAirTime geçerli sayılır
    [SerializeField] private float minAirTime = 0.45f;                // taban
    [SerializeField] private float maxAirTime = 1.2f;                 // tavan
    [SerializeField, Range(0.1f, 0.9f)] private float ascentFraction = 0.35f; // sürenin ne kadarı çıkış
    [SerializeField] private Ease ascentEase = Ease.OutQuad;          // çıkış eğrisi
    [SerializeField] private Ease descentEase = Ease.InQuad;          // iniş eğrisi
    [SerializeField] private bool landingSquash = true;               // inişte squash/strech efekti
    [SerializeField] private Vector3 squashScale = new Vector3(1.05f, 0.9f, 1.05f);
    [SerializeField] private float squashDuration = 0.08f;
    [SerializeField, Tooltip("Toplam hava süresi çarpanı (0.65 = %35 daha az)")]
    private float airtimeFactor = 0.65f;

    [Header("Jump Arc Scale")]
    [SerializeField] private bool enableJumpArcScale = true;
    [SerializeField, Tooltip("Apex'te ölçek (1.25 = %25 büyük)")] private float jumpArcScalePeak = 1.25f;
    [SerializeField] private Ease jumpArcEaseUp = Ease.OutQuad;
    [SerializeField] private Ease jumpArcEaseDown = Ease.InQuad;

    [Header("Visual (Jump/Scale Target)")]
    [SerializeField, Tooltip("Jump/scale tweenleri için görsel kök. Collider'ı tutan root'u DEĞİL, model/mesh child'ını atayın.")]
    private Transform visualRoot;

    [Header("Rotation Options")]
    [SerializeField] private float baseTurnSpeed = 360f;        // deg/sec – normal dönüş hızı
    [SerializeField] private float turnBoostMultiplier = 4f;    // yön değiştiğinde hız çarpanı
    [SerializeField] private float turnSnapAngle = 2f;          // hedef yöne bu açıdan daha yakınsa boost biter
    private float _currentTurnSpeed;                            // anlık dönüş hızı (speed'den bağımsız)
    private Coroutine _turnBoostRoutine;

    [Header("Crash / Knockback")]
    [SerializeField] private float defaultKnockbackDistance = 1.25f;
    [SerializeField] private float defaultKnockbackDuration = 0.25f;
    [SerializeField] private Ease knockbackEase = Ease.OutCubic;

    private bool _isFrozen = false;
    private Tween _knockbackTween;
    private Tween _jumpTween;
    private PlayerController _playerController;
    public ParticleSystem landingParticlePrefab;
    public Vector3 landingParticleOffset = Vector3.zero;

    public PlayerMovement Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        Speed = movementSpeed;
        _currentTurnSpeed = baseTurnSpeed;

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
        if (GameManager.Instance.GameState == GameState.GameplayRun)
        {
            Move();
            RotateTowardsMovement();
        }
    }

    private Vector3 _movementDirection = Vector3.zero;

    private void Move()
    {
        if (_isFrozen) return; // ⬅️ eklendi
        transform.position += _movementDirection * (Time.deltaTime * Speed);
    }

    private void RotateTowardsMovement()
    {
        // Hareket yoksa dönme yapma
        if (_movementDirection.sqrMagnitude < 0.0001f)
            return;

        // Sadece Y ekseninde hedef yönü hesapla
        Vector3 fwd = new Vector3(_movementDirection.x, 0f, _movementDirection.z);
        if (fwd.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            _currentTurnSpeed * Time.deltaTime
        );
    }

    private void SetDirection(Vector3 dir)
    {
        if (_isFrozen) return;
        if (dir.sqrMagnitude < 0.0001f) return;
        _movementDirection = new Vector3(dir.x, 0f, dir.z).normalized;
        StartTurnBoost();
    }

    private void ChangeDirection(SwipeDirection swipeDirection)
    {
        if (_isFrozen) return;
        _movementDirection = SwipeToWorld(swipeDirection);
        StartTurnBoost();
    }

    private void StartTurnBoost()
    {
        if (_turnBoostRoutine != null)
            StopCoroutine(_turnBoostRoutine);
        _turnBoostRoutine = StartCoroutine(TurnBoostUntilAligned());
    }

    private IEnumerator TurnBoostUntilAligned()
    {
        _currentTurnSpeed = baseTurnSpeed * turnBoostMultiplier; // 4x default

        // Hedef yöne yeterince yaklaşana kadar bekle
        while (true)
        {
            // Hedef rotasyon
            Vector3 fwd = new Vector3(_movementDirection.x, 0f, _movementDirection.z);
            if (fwd.sqrMagnitude < 0.0001f)
                break; // hareket yoksa

            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
            float ang = Quaternion.Angle(transform.rotation, targetRot);
            if (ang <= turnSnapAngle)
                break; // yeterince hizalandık

            yield return null; // bir sonraki frame'e kadar bekle
        }

        _currentTurnSpeed = baseTurnSpeed; // normale dön
        _turnBoostRoutine = null;
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

    public void Jump(float jumpHeight)
    {
        if (_isFrozen) return;
        if (_jumpTween != null && _jumpTween.IsActive()) return;
        if (GameManager.Instance.GameState != GameState.GameplayRun) return;

        IsJumping = true;

        // Tween hedefi: görsel root varsa onu, yoksa fallback olarak kendi transform
        Transform t = visualRoot != null ? visualRoot : transform;
        bool useLocal = visualRoot != null; // görselle sınırlı tutmak için local Y

        float refSpd = Mathf.Max(0.01f, referenceSpeed);
        float curSpd = Mathf.Max(0.01f, Speed);
        float totalAir = Mathf.Clamp(baseAirTime * (refSpd / curSpd), minAirTime, maxAirTime);
        totalAir *= Mathf.Max(0.05f, airtimeFactor); // global kısma (%35 default)

        float upDur = totalAir * ascentFraction;
        float downDur = totalAir - upDur;

        float startY = useLocal ? t.localPosition.y : t.position.y;
        float apexY = startY + jumpHeight;

        var seq = DOTween.Sequence();
        // çıkış
        if (useLocal)
        {
            seq.Append(t.DOLocalMoveY(apexY, upDur).SetEase(ascentEase));
            if (enableJumpArcScale)
                seq.Join(t.DOScale(Vector3.one * Mathf.Max(0.01f, jumpArcScalePeak), upDur).SetEase(jumpArcEaseUp));
        }
        else
        {
            seq.Append(t.DOMoveY(apexY, upDur).SetEase(ascentEase));
            if (enableJumpArcScale)
                seq.Join(t.DOScale(Vector3.one * Mathf.Max(0.01f, jumpArcScalePeak), upDur).SetEase(jumpArcEaseUp));
        }

        // iniş
        if (useLocal)
        {
            seq.Append(t.DOLocalMoveY(startY, downDur).SetEase(descentEase));
            if (enableJumpArcScale)
                seq.Join(t.DOScale(Vector3.one, downDur).SetEase(jumpArcEaseDown));
            seq.AppendCallback(() => { PlayLandingParticle(); IsJumping = false; });
        }
        else
        {
            seq.Append(t.DOMoveY(startY, downDur).SetEase(descentEase));
            if (enableJumpArcScale)
                seq.Join(t.DOScale(Vector3.one, downDur).SetEase(jumpArcEaseDown));
            seq.AppendCallback(() => { PlayLandingParticle(); IsJumping = false; });
        }

        // squash/strech görselde
        if (landingSquash)
        {
            seq.Append(t.DOScale(squashScale, squashDuration));
            seq.Append(t.DOScale(Vector3.one, squashDuration));
        }

        _jumpTween = seq;
        _jumpTween.OnComplete(() =>
        {
            _jumpTween = null;
        });
        _jumpTween.OnKill(() =>
        {
            IsJumping = false;
        });
    }

    public void Boost(float boosterValue)
    {
        StartCoroutine(BoosterCoroutine(boosterValue));
    }

    private IEnumerator BoosterCoroutine(float boosterValue)
    {
        yield return new WaitForEndOfFrame();
    }
    public void BeginCrashKnockback(
        Vector3 hitNormal,
        float? distance = null,
        float? duration = null,
        Ease? ease = null,
        System.Action onCompleted = null)
    {
        _isFrozen = true; // Move / Rotate guard’ları varsa otomatik durur

        Vector3 velDir = _movementDirection.sqrMagnitude > 0.0001f ? _movementDirection.normalized : Vector3.zero;
        Vector3 pushDir = velDir != Vector3.zero ? -velDir :
                        (hitNormal.sqrMagnitude > 0.0001f ? -hitNormal.normalized : -transform.forward);

        float dist = distance ?? defaultKnockbackDistance;
        float dur = duration ?? defaultKnockbackDuration;
        Ease ez = ease ?? knockbackEase;

        IsJumping = false;        // ⬅️ çarpışmada jump state sıfırla
        _jumpTween?.Kill();       // aktif bir zıplama varsa iptal et
        _knockbackTween?.Kill();
        Vector3 target = transform.position + pushDir * dist;

        _knockbackTween = transform.DOMove(target, dur)
                                .SetEase(ez)
                                .OnComplete(() => { onCompleted?.Invoke(); });
    }

    private void PlayLandingParticle()
    {
        if (landingParticlePrefab == null) return;
        try
        {
            var pos = transform.position + landingParticleOffset;
            var fx = Instantiate(landingParticlePrefab);
            fx.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90f, 0f, 0f));
            fx.Play(true);

            var main = fx.main;
            float life = main.duration + (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                ? Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax)
                : main.startLifetime.constant);
            Destroy(fx.gameObject, Mathf.Max(0.1f, life + 0.1f));
        }
        catch (Exception) { /* sessizce geç */ }
    }
    
    private static Vector3 SwipeToWorld(SwipeDirection dir)
    {
        switch (dir)
        {
            case SwipeDirection.Up:    return Vector3.forward; // +Z
            case SwipeDirection.Down:  return Vector3.back;    // -Z
            case SwipeDirection.Left:  return Vector3.left;    // -X
            case SwipeDirection.Right: return Vector3.right;   // +X
            default:                   return Vector3.zero;
        }
    }
}