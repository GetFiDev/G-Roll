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
    private float _currentTurnSpeed;                            // anlık dönüş hızı
    private Coroutine _turnBoostRoutine;

    [Header("Freeze Options")]
    [SerializeField, Tooltip("Knockback sırasında rotasyonu kilitle ve her frame sabitle.")]
    private bool hardRotationLockOnFreeze = true;
    [SerializeField, Tooltip("Knockback sırasında Animator root motion'u kapat.")]
    private bool disableAnimatorRootMotionOnFreeze = true;

    // Knockback rotation permission (overrides hard lock while true)
    private bool _allowKnockbackRotation = false;

    [Header("Knockback Visual Tuning")]
    [SerializeField, Tooltip("Knockback sırasında oyuncu biraz da geri yöne dönsün mü?")]
    private bool rotateBackDuringKnockback = true;
    [SerializeField, Range(0f, 1f), Tooltip("Ne kadar geri yöne dönsün? 0=hiç, 1=tamamen geri (180°).")]
    private float backTurnAmount = 0.55f;        // daha net terse bakış
    [SerializeField, Tooltip("Knockback toplam süresinin şu oranında rotasyon tamamlansın.")]
    private float backTurnDurationRatio = 0.4f;  // daha hızlı dön

    private Quaternion _frozenRotation;
    private Animator _cachedAnimator;
    private bool _cachedAnimatorRootMotion;
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
        if (_cachedAnimator == null)
            _cachedAnimator = GetComponentInChildren<Animator>();
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
            if (_isFrozen && hardRotationLockOnFreeze)
            {
                if (!_allowKnockbackRotation)
                    transform.rotation = _frozenRotation;
            }
            Move();
            RotateTowardsMovement();
        }
    }

    private Vector3 _movementDirection = Vector3.zero;

    private void Move()
    {
        if (_isFrozen) return;
        transform.position += _movementDirection * (Time.deltaTime * Speed);
    }

    private void RotateTowardsMovement()
    {
        if (_isFrozen) return;
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

        while (true)
        {
            Vector3 fwd = new Vector3(_movementDirection.x, 0f, _movementDirection.z);
            if (fwd.sqrMagnitude < 0.0001f) break;

            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
            float ang = Quaternion.Angle(transform.rotation, targetRot);
            if (ang <= turnSnapAngle) break;

            yield return null;
        }

        _currentTurnSpeed = baseTurnSpeed;
        _turnBoostRoutine = null;
    }

    public void ChangeSpeed(float changeAmount) => Speed += changeAmount;

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
        totalAir *= Mathf.Max(0.05f, airtimeFactor); // global kısma

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
        _jumpTween.OnComplete(() => { _jumpTween = null; });
        _jumpTween.OnKill(() => { IsJumping = false; });
    }

    public void Boost(float boosterValue) => StartCoroutine(BoosterCoroutine(boosterValue));
    private IEnumerator BoosterCoroutine(float boosterValue) { yield return new WaitForEndOfFrame(); }

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

    [Header("Lateral (x) Movement")]
    [SerializeField] private float lateralVelocity = 0f;
    [SerializeField] private float lateralDampOnStop = 30f; // daha hızlı sönümle

    [Header("Wall Hit Feedback")]
    [Tooltip("İlk geri itiş mesafesi (metre).")]
    [SerializeField] private float knockbackDistance = 0.8f; // biraz daha mesafe

    [Tooltip("İlk geri itiş süresi (s).")]
    [SerializeField] private float knockbackDuration = 0.08f; // daha hızlı kopuş

    [Tooltip("Sekme sayısı (1–2 idealdir).")]
    [Range(0, 3)] [SerializeField] private int bounceCount = 2;

    [Tooltip("Her sekmede mesafenin çarpanı (azalma katsayısı).")]
    [Range(0.3f, 0.95f)] [SerializeField] private float restitution = 0.55f; // kuyruğu kısa tut

    [Tooltip("Her küçük geri itmenin süresi (s).")]
    [SerializeField] private float microBounceDuration = 0.07f; // daha hızlı sekmeler

    [Tooltip("Duvar normaline göre küçük bir ayrıştırma, overlap kaçınmak için.")]
    [SerializeField] private float immediateSeparation = 0.08f; // biraz daha ayrıştır

    private Coroutine _wallHitCo;

    /// <summary>Dışarıdan anlık durdurma.</summary>
    public void StopImmediately()
    {
        lateralVelocity = 0f;
    }

    /// <summary>Duvara çarpma hissi: sadece geriye (XZ) knockback + mikro sekmeler.</summary>
    public void WallHitFeedback(Vector3 hitNormal)
    {
        _frozenRotation = transform.rotation; // freeze boyunca baş yönü
        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator == null)
            _cachedAnimator = GetComponentInChildren<Animator>();
        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator != null)
        {
            _cachedAnimatorRootMotion = _cachedAnimator.applyRootMotion;
            _cachedAnimator.applyRootMotion = false;
        }

        _isFrozen = true; // input ve auto-rotation kilit
        _allowKnockbackRotation = rotateBackDuringKnockback;
        if (_turnBoostRoutine != null) { StopCoroutine(_turnBoostRoutine); _turnBoostRoutine = null; }
        if (_wallHitCo != null) StopCoroutine(_wallHitCo);
        _wallHitCo = StartCoroutine(WallHitSequence(hitNormal));
    }

    private IEnumerator WallHitSequence(Vector3 normal)
    {
        // Güvenli XZ geri yön
        Vector3 n = normal.sqrMagnitude > 1e-6f ? normal : -transform.forward;
        Vector3 back = new Vector3(n.x, 0f, n.z);
        if (back.sqrMagnitude < 1e-6f)
            back = new Vector3(-transform.forward.x, 0f, -transform.forward.z);
        back = back.normalized;

        // Hemen azıcık ayır (XZ)
        if (immediateSeparation > 0f)
        {
            Vector3 p = transform.position + back * immediateSeparation;
            transform.position = new Vector3(p.x, transform.position.y, p.z);
        }

        // Lateral sönüm
        lateralVelocity = Mathf.MoveTowards(lateralVelocity, 0f, lateralDampOnStop * Time.deltaTime);

        // --- Smooth knockback with DOTween (strictly horizontal) ---
        if (_knockbackTween != null && _knockbackTween.IsActive()) _knockbackTween.Kill(false);
        Sequence seq = DOTween.Sequence();
        float totalMoveTime = 0f;

        // Ana geri itiş
        Vector3 start = transform.position;
        Vector3 end = start + back * knockbackDistance;
        seq.Append(transform.DOMove(new Vector3(end.x, start.y, end.z), knockbackDuration).SetEase(Ease.OutExpo));
        totalMoveTime += knockbackDuration;

        // Mikro geri itmeler
        float dist = knockbackDistance * restitution;
        for (int i = 0; i < bounceCount; i++)
        {
            if (dist <= 0.001f) break;
            start = end;
            end = start + back * dist;
            seq.Append(transform.DOMove(new Vector3(end.x, start.y, end.z), microBounceDuration).SetEase(Ease.OutExpo));
            totalMoveTime += microBounceDuration;
            dist *= restitution;
        }

        // Opsiyonel: hafif geri yöne dön
        if (rotateBackDuringKnockback)
        {
            Quaternion currentRot = transform.rotation;
            Quaternion fullBackRot = Quaternion.LookRotation(back, Vector3.up);
            Quaternion targetRot = Quaternion.Slerp(currentRot, fullBackRot, Mathf.Clamp01(backTurnAmount));
            float rotDur = Mathf.Max(0.01f, totalMoveTime * Mathf.Clamp01(backTurnDurationRatio));
            seq.Join(transform.DORotateQuaternion(targetRot, rotDur).SetEase(Ease.OutCubic));
        }

        _knockbackTween = seq;
        yield return seq.WaitForCompletion();

        // Restore animator/root motion if changed
        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator != null)
            _cachedAnimator.applyRootMotion = _cachedAnimatorRootMotion;

        // Freeze biterken rotasyon kilidini normale al
        _allowKnockbackRotation = false;
        _isFrozen = false;
        _wallHitCo = null;
    }

    private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private IEnumerator MoveByHorizontal(Vector3 horizDelta, float duration, System.Func<float,float> ease)
    {
        // XZ düzleminde hareket; Y sabit kalsın
        Vector3 start = transform.position;
        Vector3 end   = start + new Vector3(horizDelta.x, 0f, horizDelta.z);

        if (duration <= 0f)
        {
            transform.position = end;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            Vector3 p = Vector3.LerpUnclamped(start, end, ease(k));
            transform.position = new Vector3(p.x, start.y, p.z);
            yield return null;
        }

        transform.position = new Vector3(end.x, start.y, end.z);
    }

    private static Vector3 SwipeToWorld(SwipeDirection dir)
    {
        switch (dir)
        {
            case SwipeDirection.Up: return Vector3.forward;
            case SwipeDirection.Down: return Vector3.back;
            case SwipeDirection.Left: return Vector3.left;
            case SwipeDirection.Right: return Vector3.right;
            default: return Vector3.zero;
        }
    }
}