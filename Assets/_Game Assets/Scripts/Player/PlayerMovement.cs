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
    public float SpeedDisplayDivider { get; set; } = 1f; // UI display compensation
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

    [SerializeField] private float movementSpeed = 2f;

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
    [SerializeField, Tooltip("Spiral başlangıç yarıçapı (m)")] private float teleportSpiralStartRadius = 0.6f;
    [SerializeField, Tooltip("Çıkışta yer altından başlama derinliği (m, pozitif)")] private float exitBurrowDepth = 0.5f;
    [SerializeField, Tooltip("Çıkışta uygulanacak sıçrama yüksekliği (m)")] private float exitJumpHeight = 2f;
    
    [Header("Wall Hit Feedback")]
    [SerializeField, Tooltip("Duvara çarpınca geri zıplama mesafesi")] private float bounceDistance = 1.5f;
    [SerializeField, Tooltip("Bounce sırasında hava yüksekliği")] private float bounceHeight = 0.8f;
    [SerializeField, Tooltip("Bounce animasyon süresi")] private float bounceDuration = 0.35f;
    [SerializeField, Tooltip("Duvara çarpınca squash scale")] private Vector3 wallSquashScale = new Vector3(0.7f, 1.3f, 0.7f);
    [SerializeField, Tooltip("Wall hit particle prefab")] private ParticleSystem wallHitParticlePrefab;

    // Teleport state
    private bool teleportInProgress = false;
    private SwipeDirection? _queuedPortalExitDirection = null; // Portal içindeyken gelen direction queue
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


    private void Start()
    {
        // UI Speed Element'e kendini tanıt (Inactive olsa bile bul)
        var speedElement = FindFirstObjectByType<UISpeedElement>(FindObjectsInactive.Include);
        if (speedElement != null)
        {
            Debug.Log("[PlayerMovement] Found UISpeedElement, registering...");
            speedElement.RegisterPlayer(this);
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] Could not find UISpeedElement!");
        }
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
        
        // The ball always faces where it's moving (cardinal direction)
        if (_movementDirection.sqrMagnitude < 0.0001f)
            return;

        Vector3 fwd = new Vector3(_movementDirection.x, 0f, _movementDirection.z);
        if (fwd.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
        transform.rotation = targetRot;
    }

    private bool IsAlive() => this != null && isActiveAndEnabled;

    private void SetDirection(Vector3 dir)
    {
        EnsureFirstMoveNotified();
        if (!IsAlive() || _isFrozen) return;
        if (dir.sqrMagnitude < 0.0001f) return;
        
        // Enforce cardinal-only movement
        _movementDirection = SnapToCardinal(dir);
        StartTurnBoost();
    }

    private float _lastSwipeTime;
    private SwipeDirection _lastSwipeDirection = SwipeDirection.Up;

    private void ChangeDirection(SwipeDirection swipeDirection)
    {
        EnsureFirstMoveNotified();
        if (!IsAlive()) return;
        
        // Portal içindeyse direction'ı queue'ya al ve return
        if (teleportInProgress)
        {
            _queuedPortalExitDirection = swipeDirection;
            Debug.Log($"[PlayerMovement] Queued portal exit direction: {swipeDirection}");
            return;
        }
        
        if (_isFrozen) return;

        // Filter accidental "Up" swipes after side swipes
        if (swipeDirection == SwipeDirection.Up && 
           (_lastSwipeDirection == SwipeDirection.Left || _lastSwipeDirection == SwipeDirection.Right))
        {
            if (Time.time - _lastSwipeTime < 0.35f)
            {
                Debug.Log($"[PlayerMovement] Ignored rapid UP swipe (dt={Time.time - _lastSwipeTime:F3}s) after SIDE swipe.");
                return;
            }
        }

        _lastSwipeTime = Time.time;
        _lastSwipeDirection = swipeDirection;

        // Pure cardinal direction - no diagonal movement ever
        Vector3 targetDir = SwipeToWorld(swipeDirection);
        
        // ARCHITECTURAL ENFORCEMENT: Only cardinal directions allowed
        _movementDirection = SnapToCardinal(targetDir);
        
        Debug.Log($"[PlayerMovement] ChangeDirection: Swipe={swipeDirection}, Direction set to {_movementDirection} (Cardinal-only)");
        
        StartTurnBoost();
    }

    /// <summary>
    /// Snaps any vector to the nearest cardinal direction.
    /// This is architectural enforcement - the character can ONLY move in 4 directions.
    /// </summary>
    private Vector3 SnapToCardinal(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        // Normalize for comparison
        Vector3 normalized = dir.normalized;
        
        // Choose dominant axis
        if (Mathf.Abs(normalized.x) > Mathf.Abs(normalized.z))
        {
            // Left or Right
            return new Vector3(Mathf.Sign(normalized.x), 0, 0);
        }
        else
        {
            // Forward or Back
            return new Vector3(0, 0, Mathf.Sign(normalized.z));
        }
    }

    private void StartTurnBoost()
    {
        // Instant turn, no boost needed
        if (_turnBoostRoutine != null)
        {
            StopCoroutine(_turnBoostRoutine);
            _turnBoostRoutine = null;
        }
    }

    private IEnumerator TurnBoostUntilAligned()
    {
        _currentTurnSpeed = baseTurnSpeed * turnBoostMultiplier; // 4x default

        while (true)
        {
            // Align with movement direction
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
        if (!IsAlive() || _isFrozen) return;
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

        // Exit spiral rise (giriş spiral'inin tersi - collision-safe)
        Vector3 preservedFlat = Vector3.ProjectOnPlane(_preservedEntryForward, Vector3.up).normalized;
        if (preservedFlat.sqrMagnitude < 1e-4f) preservedFlat = transform.forward;
        float outDuration = 0.5f;
        
        // Spiral çıkış path'i - PORTAL MERKEZİNDE, ileri hareket yok
        Vector3 exitUp = Vector3.up;
        int spiralSteps = Mathf.Clamp(Mathf.CeilToInt(turns * 18f), 12, 128);
        
        // Radyal başlangıç (preserved forward'a dik)
        Vector3 exitRadial = Vector3.Cross(preservedFlat, exitUp).normalized;
        if (exitRadial.sqrMagnitude < 1e-4f) exitRadial = Vector3.right;
        
        var exitPoints = new Vector3[spiralSteps];
        for (int i = 0; i < spiralSteps; i++)
        {
            float t = (i + 1) / (float)spiralSteps;  // 0..1
            float eased = Mathf.Pow(t, 3f); // easeInCubic (ters yönde)
            
            // Spiral radius: küçükten büyüğe (içten dışa açılır)
            float radius = Mathf.Lerp(0f, startRadius, eased);
            float angle = eased * turns * Mathf.PI * 2f;
            
            Vector3 offset = Quaternion.AngleAxis(Mathf.Rad2Deg * angle, exitUp) * exitRadial * radius;
            
            // Yer altından (-exitBurrowDepth) yüzeye (0) yüksel - PORTAL MERKEZİNDE
            float heightProgress = t; // linear yükseliş
            Vector3 pos = exitTransform.position 
                + offset 
                + exitUp * Mathf.Lerp(-Mathf.Abs(exitBurrowDepth), 0f, heightProgress);
                // İleri hareket KALDIRILDI - portal merkezinde çıkar
                
            exitPoints[i] = pos;
        }
        
        seq.Append(transform.DOPath(exitPoints, outDuration, PathType.CatmullRom, PathMode.Full3D, 10, Color.white)
                            .SetEase(Ease.InOutQuad)
                            .OnUpdate(() =>
                            {
                                // Queued direction varsa ona bak, yoksa preserved forward
                                Vector3 lookDir = _queuedPortalExitDirection.HasValue 
                                    ? SwipeToWorld(_queuedPortalExitDirection.Value) 
                                    : preservedFlat;
                                
                                if (lookDir.sqrMagnitude > 1e-4f)
                                {
                                    Quaternion look = Quaternion.LookRotation(lookDir, Vector3.up);
                                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 0.15f);
                                }
                            })
                            .SetUpdate(UpdateType.Normal, false)
                            .SetLink(gameObject));
        
        // Çıkış sırasında görsel scale efekti
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

        // ---- UNFREEZE: hareketi geri aç ----
        Speed = prevSpeed;
        
        // Queued direction varsa onu kullan, yoksa preserved forward
        Vector3 exitDirection = preservedFlat;
        if (_queuedPortalExitDirection.HasValue)
        {
            exitDirection = SnapToCardinal(SwipeToWorld(_queuedPortalExitDirection.Value));
            Debug.Log($"[PlayerMovement] Using queued exit direction: {_queuedPortalExitDirection.Value} -> {exitDirection}");
            _queuedPortalExitDirection = null; // Clear queue
        }
        
        _movementDirection = exitDirection;

        if (disableAnimatorRootMotionOnFreeze && _cachedAnimator != null)
            _cachedAnimator.applyRootMotion = _cachedAnimatorRootMotion;

        teleportInProgress = false;
        _teleportSeq = null;
    }

    private void OnDisable()
    {
        // Stop any running Unity coroutines and scheduled invokes
        StopAllCoroutines();
        CancelInvoke();

        // Kill active tweens safely (do not complete)
        if (_jumpTween != null && _jumpTween.IsActive()) { _jumpTween.Kill(false); _jumpTween = null; }
        if (_knockbackTween != null && _knockbackTween.IsActive()) { _knockbackTween.Kill(false); _knockbackTween = null; }
        if (_teleportSeq != null && _teleportSeq.IsActive()) { _teleportSeq.Kill(false); _teleportSeq = null; }

        _turnBoostRoutine = null;
        _wallHitCo = null;
    }

    private void OnDestroy()
    {
        // Mirror OnDisable for safety in case of destroy directly
        StopAllCoroutines();
        CancelInvoke();

        if (_jumpTween != null && _jumpTween.IsActive()) { _jumpTween.Kill(false); _jumpTween = null; }
        if (_knockbackTween != null && _knockbackTween.IsActive()) { _knockbackTween.Kill(false); _knockbackTween = null; }
        if (_teleportSeq != null && _teleportSeq.IsActive()) { _teleportSeq.Kill(false); _teleportSeq = null; }

        _turnBoostRoutine = null;
        _wallHitCo = null;
    }

    public void Jump(float jumpHeight)
    {
        EnsureFirstMoveNotified();
        if (!IsAlive() || _isFrozen) return;
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
        if (!IsAlive() || _isFrozen) return;

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
        // lateralVelocity removed
    }

    /// <summary>Duvara çarpma hissi: sadece geriye (XZ) knockback.</summary>
    public void WallHitFeedback(Vector3 hitPoint, Vector3 hitNormal)
    {
        _frozenRotation = transform.rotation;
        
        // Hareket yönünü sakla (bounce direction için)
        Vector3 moveDir = _movementDirection.normalized;
        if (moveDir.sqrMagnitude < 0.01f) moveDir = -transform.forward;
        
        // Oyunu bitir
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

        _isFrozen = true;
        if (_turnBoostRoutine != null) { StopCoroutine(_turnBoostRoutine); _turnBoostRoutine = null; }
        if (_wallHitCo != null) StopCoroutine(_wallHitCo);
        _wallHitCo = StartCoroutine(WallHitBounceSequence(hitPoint, hitNormal, moveDir));
    }

    private IEnumerator WallHitBounceSequence(Vector3 contactPoint, Vector3 hitNormal, Vector3 movementDirection)
    {
        // 1) Bounce direction hesapla - hareket yönünü hit normal'e göre yansıt
        Vector3 bounceDir = Vector3.Reflect(-movementDirection, hitNormal);
        bounceDir = new Vector3(bounceDir.x, 0, bounceDir.z).normalized;
        
        Debug.Log($"[WallHit] MoveDir={movementDirection}, HitNormal={hitNormal}, BounceDir={bounceDir}");
        
        // 2) Particle FX spawn et
        if (wallHitParticlePrefab != null)
        {
            try
            {
                var fx = Instantiate(wallHitParticlePrefab, contactPoint, Quaternion.LookRotation(hitNormal));
                fx.Play();
                Destroy(fx.gameObject, 2f);
            }
            catch (Exception) { /* sessizce geç */ }
        }
        
        // 3) Kill active tweens
        if (_knockbackTween != null && _knockbackTween.IsActive()) _knockbackTween.Kill(false);
        
        // 4) Bounce sequence: Squash → Bounce → Unsquash
        Transform scaleTarget = visualRoot != null ? visualRoot : transform;
        var seq = DOTween.Sequence();
        
        // Squash (ezilme)
        seq.Append(scaleTarget.DOScale(wallSquashScale, 0.08f).SetEase(Ease.InQuad));
        
        // Bounce back with jump
        Vector3 bounceEnd = transform.position + bounceDir * bounceDistance;
        bounceEnd.y = transform.position.y; // Y seviyesini koru
        
        seq.Append(transform.DOJump(bounceEnd, bounceHeight, 1, bounceDuration)
            .SetEase(Ease.OutQuad));
        
        // Unsquash (normale dön)
        seq.Join(scaleTarget.DOScale(Vector3.one, bounceDuration).SetEase(Ease.OutBack));
        
        _knockbackTween = seq;
        seq.SetLink(gameObject);
        
        yield return seq.WaitForCompletion();
        
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
        if (!IsAlive() || _isFrozen) return;
        ChangeDirection(dir);
    }

    private void HandleDoubleTap()
    {
        EnsureFirstMoveNotified();
        if (!IsAlive() || _isFrozen) return;
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

    public void SetBaseSpeed(float val)
    {
        movementSpeed = val;
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