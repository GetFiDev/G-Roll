using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public bool IsMoving => _isActive && _movementDirection.magnitude > 0.1f;
    public float Speed { get; private set; }
    public bool IsJumping { get; private set; } = false;

    // New orchestration bindings
    private GameplayLogicApplier _logic;     // GameplayManager tarafından bind edilir
    private TouchManager _touch;             // Gameplay domain touch (GameManager üzerinden değil)
    private bool _isActive = false;          // Gameplay session açık mı?
    private float _playerSpeedMultiplier = 1f; // logic'ten gelen çarpan
    private Action<SwipeDirection> _onSwipeHandler;   // stored delegates for unsubscribe
    private Action _onDoubleTapHandler;
    private bool _firstMoveNotified = false;          // camera start gate
    private float _externalAccelPer60 = 0f; // dk başına dış hızlanma
    private Vector3 _spawnScale;            // başlangıç ölçeği (SetPlayerSize için referans)
    private float _spawnWorldScaleY = 1f;   // scale=1 iken yer teması 0.225 referansı için dünya ölçeği (Y)

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

    [Header("Teleport Settings")]
    [SerializeField, Tooltip("Spiralın toplam süresi (s)")] private float teleportSpiralDuration = 0.35f;
    [SerializeField, Tooltip("Spiral başlangıç yarıçapı (m)")] private float teleportSpiralStartRadius = 0.6f;
    [SerializeField, Tooltip("Spiral tur sayısı")] private int teleportSpiralTurns = 2;
    [SerializeField, Tooltip("Vortex hissi için yukarı kaldırma (m)")] private float teleportVortexLift = 0.25f;
    [SerializeField, Tooltip("Spiral sonunda zemine gömülme miktarı (m, pozitif)")] private float teleportSinkDepth = 0.5f;
    [SerializeField, Tooltip("Merkeze geldikten SONRA ekstra aşağı dalma miktarı (m)")] private float centerExtraSinkDepth = 0.2f;
    [SerializeField, Tooltip("Merkez sonrası ekstra dalma süresi (s)")] private float centerExtraSinkDuration = 0.08f;
    [SerializeField, Tooltip("Çıkışta yer altından başlama derinliği (m, pozitif)")] private float exitBurrowDepth = 0.5f;
    [SerializeField, Tooltip("Çıkışta uygulanacak sıçrama yüksekliği (m)")] private float exitJumpHeight = 2f;
    [SerializeField, Tooltip("Çıkış sıçramasının süresi (s)")] private float exitJumpDuration = 0.35f;

    // Teleport state
    private bool teleportInProgress = false;
    private Vector3 _preservedEntryForward = Vector3.forward;
    private Vector3 _savedMoveDir = Vector3.zero;

    [Header("Freeze Options")]
    [SerializeField, Tooltip("Knockback sırasında rotasyonu kilitle ve her frame sabitle.")]
    private bool hardRotationLockOnFreeze = true;
    [SerializeField, Tooltip("Knockback sırasında Animator root motion'u kapat.")]
    private bool disableAnimatorRootMotionOnFreeze = true;


    private Quaternion _frozenRotation;
    private Animator _cachedAnimator;
    private bool _cachedAnimatorRootMotion;
    public bool _isFrozen = false;

    private Tween _knockbackTween;
    private Tween _jumpTween;
    private Tween _teleportSeq;
    private PlayerController _playerController;

    public ParticleSystem landingParticlePrefab;
    public Vector3 landingParticleOffset = Vector3.zero;

    public PlayerMovement Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        // Speed başlangıçta base speed; efektif hız logic çarpanı ile Update'te hesaplanır
        Speed = movementSpeed;
        _currentTurnSpeed = baseTurnSpeed;
        _spawnScale = transform.localScale;
        _spawnWorldScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        if (_cachedAnimator == null)
            _cachedAnimator = GetComponentInChildren<Animator>();
        return this;
    }

    /// <summary>GameplayManager, session başında çağırır.</summary>
    public void BindToGameplay(GameplayLogicApplier logic, TouchManager touch = null)
    {
        _logic = logic;
        _touch = touch;
        _isActive = true;
        _firstMoveNotified = false;

        // anlık çarpanı al ve hız hesaplamasını güncel tut
        _playerSpeedMultiplier = (_logic != null) ? Mathf.Max(0f, _logic.PlayerSpeedMultiplier) : 1f;
        if (_logic != null)
        {
            _logic.OnPlayerSpeedMultiplierChanged += HandlePlayerSpeedMultiplierChanged;
        }

        // Touch bağla (opsiyonel dıştan yönetilir)
        if (_touch != null)
        {
            _onSwipeHandler = HandleSwipe;
            _onDoubleTapHandler = HandleDoubleTap;
            _touch.OnSwipe += _onSwipeHandler;
            _touch.OnDoubleTap += _onDoubleTapHandler;
        }
    }

    /// <summary>GameplayManager, session sonunda çağırır.</summary>
    public void UnbindFromGameplay()
    {
        _isActive = false;

        if (_logic != null)
        {
            _logic.OnPlayerSpeedMultiplierChanged -= HandlePlayerSpeedMultiplierChanged;
        }
        _logic = null;

        if (_touch != null)
        {
            if (_onSwipeHandler != null) _touch.OnSwipe -= _onSwipeHandler;
            if (_onDoubleTapHandler != null) _touch.OnDoubleTap -= _onDoubleTapHandler;
        }
        _onSwipeHandler = null;
        _onDoubleTapHandler = null;
        _touch = null;

        _movementDirection = Vector3.zero;
        Speed = 0f;
    }

    private void HandlePlayerSpeedMultiplierChanged(float mult)
    {
        _playerSpeedMultiplier = Mathf.Max(0f, mult);
    }


    private void Update()
    {
        // Teleport sırasında normal hareket/rotasyon akışını durdur
        if (teleportInProgress) return;

        if (_isActive)
        {
            if (_isFrozen && hardRotationLockOnFreeze)
            {
                transform.rotation = _frozenRotation;
            }
            // Dış hızlanma: dakika başına verilen değeri saniyeye çevirip base hıza ekle
            if (!Mathf.Approximately(_externalAccelPer60, 0f))
            {
                float perSecond = _externalAccelPer60 / 60f;
                movementSpeed += perSecond * Time.deltaTime; // basit ve direkt; run boyunca birikir
            }
            Move();
            RotateTowardsMovement();
        }
    }

    private Vector3 _movementDirection = Vector3.zero;

    private void Move()
    {
        if (_isFrozen) return;
        // Efektif hız = base movementSpeed * logic'ten gelen playerSpeedMultiplier
        Speed = movementSpeed * Mathf.Max(0f, _playerSpeedMultiplier);
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
        EnsureFirstMoveNotified();
        if (_isFrozen) return;
        if (dir.sqrMagnitude < 0.0001f) return;
        _movementDirection = new Vector3(dir.x, 0f, dir.z).normalized;
        StartTurnBoost();
    }

    private void ChangeDirection(SwipeDirection swipeDirection)
    {
        EnsureFirstMoveNotified();
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

    /// <summary>
    /// Teleport emri: girişte kısa spiral/vortex, çıkışta preserved forward yönüne doğru zıplayarak çıkış.
    /// </summary>
    public void RequestTeleport(Transform exitTransform, Transform entryCenter, Vector3 preservedEntryForward)
    {
        if (exitTransform == null || entryCenter == null) return;
        if (teleportInProgress) return;
        _preservedEntryForward = preservedEntryForward;
        StartCoroutine(TeleportRoutine(exitTransform, entryCenter));
    }

    private IEnumerator TeleportRoutine(Transform exitTransform, Transform entryCenter)
    {
        teleportInProgress = true;

        // ---- HARD FREEZE: tüm hareket/tween/input etkilerini durdur ----
        float prevSpeed = Speed;
        Vector3 prevMoveDir = _movementDirection;

        // Mevcut tweeenleri sonlandır
        if (_jumpTween != null && _jumpTween.IsActive()) { _jumpTween.Kill(false); _jumpTween = null; }
        if (_knockbackTween != null && _knockbackTween.IsActive()) { _knockbackTween.Kill(false); _knockbackTween = null; }
        if (_teleportSeq != null && _teleportSeq.IsActive()) { _teleportSeq.Kill(false); _teleportSeq = null; }

        // Tam durdur
        Speed = 0f;
        _movementDirection = Vector3.zero;

        // Opsiyonel: animasyon root motion devre dışı
        if (disableAnimatorRootMotionOnFreeze)
        {
            if (_cachedAnimator == null) _cachedAnimator = GetComponentInChildren<Animator>();
            if (_cachedAnimator != null)
            {
                _cachedAnimatorRootMotion = _cachedAnimator.applyRootMotion;
                _cachedAnimator.applyRootMotion = false;
            }
        }

        // ---- SPIRAL (DOTween Path) ----
        // Vortex: 0.5s, tam 2 tur, yarım genişlik
        float duration = 0.5f;
        float turns = 2f;
        float startRadius = Mathf.Max(0.01f, teleportSpiralStartRadius * 0.5f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(turns * 18f), 12, 128); // her tur için ~18 nokta

        // Spiral düzlemi: dünya Y
        Vector3 up = Vector3.up;

        // Başlangıç radyal
        Vector3 radial = transform.position - entryCenter.position;
        if (radial.sqrMagnitude < 1e-4f) radial = transform.right;
        radial = Vector3.ProjectOnPlane(radial, up).normalized;

        var points = new Vector3[steps];
        for (int i = 0; i < steps; i++)
        {
            float a = (i + 1) / (float)steps;                 // 0..1
            float eased = 1f - Mathf.Pow(1f - a, 3f);         // easeOutCubic
            float radius = Mathf.Lerp(startRadius, 0f, eased);
            float angle = eased * turns * Mathf.PI * 2f;

            Vector3 offset = Quaternion.AngleAxis(Mathf.Rad2Deg * angle, up) * radial * radius;
            // Vortex boyunca toplam 1.0m aşağı in
            Vector3 pos = entryCenter.position + offset + up * Mathf.Lerp(0f, -1f, eased);
            points[i] = pos;
        }

        // Sequence: Spiral path -> teleport snap -> exit jump -> landing
        var seq = DOTween.Sequence();

        // Spiral path
        seq.Append(transform.DOPath(points, duration, PathType.CatmullRom, PathMode.Full3D, 10, Color.white)
                            .SetEase(Ease.OutCubic)
                            .OnUpdate(() =>
                            {
                                // Vortex bakışı
                                Vector3 toCenter = (entryCenter.position - transform.position);
                                if (toCenter.sqrMagnitude > 1e-4f)
                                {
                                    Quaternion look = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 0.25f);
                                }
                            })
                            .SetUpdate(UpdateType.Normal, false)
                            .SetLink(gameObject));


        // Teleport snap (callback ile)
        seq.AppendCallback(() =>
        {
            // doğrudan çıkışı yer altından başlat
            transform.position = exitTransform.position - Vector3.up * Mathf.Abs(exitBurrowDepth);
            // giriş forward'ını koru
            Vector3 fwdFlat = Vector3.ProjectOnPlane(_preservedEntryForward, Vector3.up).normalized;
            if (fwdFlat.sqrMagnitude < 1e-4f) fwdFlat = transform.forward;
            transform.rotation = Quaternion.LookRotation(fwdFlat, Vector3.up);
        });

        // Exit jump (preserved forward yönüne 1m)
        Vector3 preservedFlat = Vector3.ProjectOnPlane(_preservedEntryForward, Vector3.up).normalized;
        if (preservedFlat.sqrMagnitude < 1e-4f) preservedFlat = transform.forward;
        Vector3 jumpEnd = exitTransform.position + preservedFlat; // 1m ileri
        float outDuration = 0.5f;
        seq.Append(transform.DOJump(jumpEnd, exitJumpHeight, 1, outDuration)
                            .SetEase(Ease.OutQuad)
                            .SetUpdate(UpdateType.Normal, false)
                            .SetLink(gameObject));
        // Çıkış sırasında normal zıplamadaki gibi büyüyüp küçülme efekti
        Transform scaleTarget = visualRoot != null ? visualRoot : transform;
        var scaleSeq = DOTween.Sequence()
            .Append(scaleTarget.DOScale(Vector3.one * Mathf.Max(0.01f, jumpArcScalePeak), outDuration * 0.5f).SetEase(jumpArcEaseUp))
            .Append(scaleTarget.DOScale(Vector3.one, outDuration * 0.5f).SetEase(jumpArcEaseDown))
            .SetUpdate(UpdateType.Normal, false)
            .SetLink(gameObject);
        seq.Join(scaleSeq);

        // Landing FX
        seq.AppendCallback(() => PlayLandingParticle());

        // Kaydet referans, çalıştır
        _teleportSeq = seq;

        // Tamamlanmasını bekle
        yield return _teleportSeq.WaitForCompletion();

        // ---- UNFREEZE: hareketi geri aç ve yönü preserved forward yap ----
        Speed = prevSpeed;
        _movementDirection = preservedFlat;

        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator != null)
            _cachedAnimator.applyRootMotion = _cachedAnimatorRootMotion;

        teleportInProgress = false;
        _teleportSeq = null;
    }

    public void Jump(float jumpHeight)
    {
        EnsureFirstMoveNotified();
        if (_isFrozen) return;
        if (_jumpTween != null && _jumpTween.IsActive()) return;

        IsJumping = true;

        // Pozisyon hedefi: HER ZAMAN ROOT (collider ile birlikte yükselsin)
        Transform posTarget = transform;
        // Ölçek/squash hedefi: görsel kök varsa onu kullan, yoksa root
        Transform scaleTarget = visualRoot != null ? visualRoot : transform;

        // Hava süresini koşu hızına göre ayarla
        float refSpd = Mathf.Max(0.01f, referenceSpeed);
        float curSpd = Mathf.Max(0.01f, Speed);
        float totalAir = Mathf.Clamp(baseAirTime * (refSpd / curSpd), minAirTime, maxAirTime);
        totalAir *= Mathf.Max(0.05f, airtimeFactor); // global kısma

        float upDur = totalAir * ascentFraction;
        float downDur = totalAir - upDur;

        float startY = posTarget.position.y;
        float apexY = startY + jumpHeight;

        var seq = DOTween.Sequence();

        // Çıkış (root'un dünya Y'si)
        seq.Append(posTarget.DOMoveY(apexY, upDur).SetEase(ascentEase));
        if (enableJumpArcScale)
            seq.Join(scaleTarget.DOScale(Vector3.one * Mathf.Max(0.01f, jumpArcScalePeak), upDur).SetEase(jumpArcEaseUp));

        // İniş (root'un dünya Y'si)
        seq.Append(posTarget.DOMoveY(startY, downDur).SetEase(descentEase));
        if (enableJumpArcScale)
            seq.Join(scaleTarget.DOScale(Vector3.one, downDur).SetEase(jumpArcEaseDown));

        seq.AppendCallback(() => { PlayLandingParticle(); IsJumping = false; });

        // Squash/strech sadece görselde
        if (landingSquash)
        {
            seq.Append(scaleTarget.DOScale(squashScale, squashDuration));
            seq.Append(scaleTarget.DOScale(Vector3.one, squashDuration));
        }

        _jumpTween = seq;
        _jumpTween.OnComplete(() => { _jumpTween = null; });
        _jumpTween.OnKill(() => { IsJumping = false; });
    }

    /// <summary>
    /// Normal Jump'a çok benzer; ortografik kamerada daha yüksek hissi vermek için
    /// hem zıplama yüksekliğini hem de görsel ölçeği artırır.
    /// airTimeMultiplier: toplam havada kalma süresine çarpan (1.0 = aynı),
    /// scaleMultiplier: apex'teki görsel büyütmeye çarpan (1.0 = aynı).
    /// </summary>
    public void JumpCustom(float jumpHeight, float airTimeMultiplier = 1.15f, float scaleMultiplier = 1.35f)
    {
        if (_isFrozen) return;

        // Custom jump her zaman mevcut zıplamayı iptal edip yeni tween başlatır
        if (_jumpTween != null && _jumpTween.IsActive()) { _jumpTween.Kill(false); _jumpTween = null; }

        IsJumping = true;

        Transform posTarget = transform;                                // pozisyon root
        Transform scaleTarget = visualRoot != null ? visualRoot : transform; // ölçek görsel

        // Normal Jump ile aynı taban; yalnızca çarpan uygularız
        float refSpd = Mathf.Max(0.01f, referenceSpeed);
        float curSpd = Mathf.Max(0.01f, Speed);
        float totalAir = Mathf.Clamp(baseAirTime * (refSpd / curSpd), minAirTime, maxAirTime);

        // Global kısma + custom multiplier
        totalAir *= Mathf.Max(0.05f, airtimeFactor) * Mathf.Max(0.1f, airTimeMultiplier);

        float upDur = totalAir * ascentFraction;
        float downDur = totalAir - upDur;

        float startY = posTarget.position.y;
        float apexY = startY + jumpHeight;

        // Scale tepesini büyüt (ortografik kamerada yüksekliği hissettirmek için)
        float peakScale = Mathf.Max(0.01f, jumpArcScalePeak * Mathf.Max(1f, scaleMultiplier));

        var seq = DG.Tweening.DOTween.Sequence();

        // Çıkış (root'un dünya Y'si)
        seq.Append(posTarget.DOMoveY(apexY, upDur).SetEase(ascentEase));
        if (enableJumpArcScale)
            seq.Join(scaleTarget.DOScale(Vector3.one * peakScale, upDur).SetEase(jumpArcEaseUp));

        // İniş (root'un dünya Y'si)
        seq.Append(posTarget.DOMoveY(startY, downDur).SetEase(descentEase));
        if (enableJumpArcScale)
            seq.Join(scaleTarget.DOScale(Vector3.one, downDur).SetEase(jumpArcEaseDown));

        seq.AppendCallback(() => { PlayLandingParticle(); IsJumping = false; });

        // Squash/strech sadece görselde (normal Jump ile aynı davranış)
        if (landingSquash)
        {
            seq.Append(scaleTarget.DOScale(squashScale, squashDuration));
            seq.Append(scaleTarget.DOScale(Vector3.one, squashDuration));
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

    [Tooltip("Duvar normaline göre küçük bir ayrıştırma, overlap kaçınmak için.")]
    [SerializeField] private float immediateSeparation = 0.08f; // biraz daha ayrıştır

    private Coroutine _wallHitCo;

    /// <summary>Dışarıdan anlık durdurma.</summary>
    public void StopImmediately()
    {
        lateralVelocity = 0f;
    }

    /// <summary>Duvara çarpma hissi: sadece geriye (XZ) knockback.</summary>
    public void WallHitFeedback(Vector3 hitNormal)
    {
        _frozenRotation = transform.rotation; // freeze boyunca baş yönü
        // Oyunu bitiriyoruz: yön ve hız sıfırlansın, ileri hareket tamamen dursun
        _movementDirection = Vector3.zero;
        Speed = 0f;
        _playerController?.HitTheWall();

        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator == null)
            _cachedAnimator = GetComponentInChildren<Animator>();
        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator != null)
        {
            _cachedAnimatorRootMotion = _cachedAnimator.applyRootMotion;
            _cachedAnimator.applyRootMotion = false;
        }

        _isFrozen = true; // input ve auto-rotation kilit
        if (_turnBoostRoutine != null) { StopCoroutine(_turnBoostRoutine); _turnBoostRoutine = null; }
        if (_wallHitCo != null) StopCoroutine(_wallHitCo);
        _wallHitCo = StartCoroutine(WallHitSequenceSimple(hitNormal));
    }

    private IEnumerator WallHitSequenceSimple(Vector3 normal)
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

        // Var olan tweeni durdur
        if (_knockbackTween != null && _knockbackTween.IsActive()) _knockbackTween.Kill(false);

        // Tek adımlık geri itiş (Y sabit)
        Vector3 start = transform.position;
        Vector3 end = start + back * knockbackDistance;
        _knockbackTween = transform
            .DOMove(new Vector3(end.x, start.y, end.z), knockbackDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject);

        yield return _knockbackTween.WaitForCompletion();

        // Oyun bitti durumunda donuk kalsın; coroutine biter ama freeze korunur
        _wallHitCo = null;
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
    public void InjectSwipe(SwipeDirection swipe)
    {
        EnsureFirstMoveNotified();
        ChangeDirection(swipe);
    }

    private void HandleSwipe(SwipeDirection dir)
    {
        EnsureFirstMoveNotified();
        ChangeDirection(dir);
    }

    private void HandleDoubleTap()
    {
        EnsureFirstMoveNotified();
        Jump(doubleTapJumpForce);
    }

    private void EnsureFirstMoveNotified()
    {
        if (_firstMoveNotified) return;
        _firstMoveNotified = true;
        _logic?.NotifyFirstPlayerMove();
    }
    // PlayerStatHandler run başında çağırır: dakika başına hızlanma miktarı
    public void SetExternalAccelerationPer60Sec(float value)
    {
        _externalAccelPer60 = value; // negatif olabilir (yavaşlama)
    }

    // PlayerStatHandler run başında çağırır: başlangıç hızı üzerine ek (additive)
    public void AddPlayerSpeed(float delta)
    {
        movementSpeed += delta;
    }

    public void SetPlayerSize(int percent)
    {
        float k = 1f + (percent / 100f);
        transform.localScale = _spawnScale * k;

        const float baseContactYAtScale1 = 0.225f;
        float currentWorldScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        float ratio = currentWorldScaleY / _spawnWorldScaleY; // 1.0 => 0.225, 1.5 => 0.3375, 0.5 => 0.1125
        float worldContactY = baseContactYAtScale1 * ratio;

        Vector3 pos = transform.position;
        pos.y = worldContactY;
        transform.position = pos;
    }
}