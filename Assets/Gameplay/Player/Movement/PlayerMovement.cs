using System;
using System.Collections;
using DG.Tweening;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;
using GRoll.Gameplay.Player.Core;
using UnityEngine;

namespace GRoll.Gameplay.Player.Movement
{
    /// <summary>
    /// Handles all player movement including walking, jumping, teleporting, and wall collision feedback.
    /// Integrates with IGameplaySpeedService for speed multipliers and booster effects.
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        public bool IsMoving => _isActive && _movementDirection.magnitude > 0.1f;
        public float Speed { get; private set; }
        public float SpeedDisplayDivider { get; set; } = 1f;
        public bool IsJumping { get; private set; } = false;
        public bool IsCoasting => _isCoasting;

        private IGameplaySpeedService _speedService;
        private bool _isActive = false;
        private float _playerSpeedMultiplier = 1f;
        private bool _firstMoveNotified = false;
        private float _externalAccelPer60 = 0f;
        private Vector3 _spawnScale;
        private float _spawnWorldScaleY = 1f;

        [SerializeField] private float movementSpeed = 2f;

        [Header("Jump Options")]
        [SerializeField] private float doubleTapJumpForce = 3f;
        [SerializeField] private float baseAirTime = 0.9f;
        [SerializeField] private float referenceSpeed = 5f;
        [SerializeField] private float minAirTime = 0.45f;
        [SerializeField] private float maxAirTime = 1.2f;
        [SerializeField, Range(0.1f, 0.9f)] private float ascentFraction = 0.35f;
        [SerializeField] private Ease ascentEase = Ease.OutQuad;
        [SerializeField] private Ease descentEase = Ease.InQuad;
        [SerializeField] private bool landingSquash = true;
        [SerializeField] private Vector3 squashScale = new Vector3(1.05f, 0.9f, 1.05f);
        [SerializeField] private float squashDuration = 0.08f;
        [SerializeField, Tooltip("Total air time multiplier (0.65 = 35% less)")]
        private float airtimeFactor = 0.65f;

        [Header("Jump Arc Scale")]
        [SerializeField] private bool enableJumpArcScale = true;
        [SerializeField, Tooltip("Scale at apex (1.25 = 25% bigger)")] private float jumpArcScalePeak = 1.25f;
        [SerializeField] private Ease jumpArcEaseUp = Ease.OutQuad;
        [SerializeField] private Ease jumpArcEaseDown = Ease.InQuad;

        [Header("Visual (Jump/Scale Target)")]
        [SerializeField, Tooltip("Visual root for jump/scale tweens. Assign the model/mesh child, NOT the collider root.")]
        private Transform visualRoot;

        [Header("Rotation Options")]
        [SerializeField] private float baseTurnSpeed = 360f;
        [SerializeField] private float turnBoostMultiplier = 4f;
        [SerializeField] private float turnSnapAngle = 2f;
        private float _currentTurnSpeed;
        private Coroutine _turnBoostRoutine;

        [Header("Teleport Settings")]
        [SerializeField, Tooltip("Spiral start radius (m)")] private float teleportSpiralStartRadius = 0.6f;
        [SerializeField, Tooltip("Exit burrow depth (m, positive)")] private float exitBurrowDepth = 0.5f;
        [SerializeField, Tooltip("Exit jump height (m)")] private float exitJumpHeight = 2f;

        [Header("Wall Hit Feedback")]
        [SerializeField, Tooltip("Bounce distance on wall hit")] private float bounceDistance = 1.5f;
        [SerializeField, Tooltip("Bounce height during animation")] private float bounceHeight = 0.8f;
        [SerializeField, Tooltip("Bounce animation duration")] private float bounceDuration = 0.35f;
        [SerializeField, Tooltip("Squash scale on wall hit")] private Vector3 wallSquashScale = new Vector3(0.7f, 1.3f, 0.7f);
        [SerializeField, Tooltip("Wall hit particle prefab")] private ParticleSystem wallHitParticlePrefab;
        [SerializeField] private float knockbackDistance = 0.8f;
        [SerializeField] private float knockbackDuration = 0.08f;
        [SerializeField] private float immediateSeparation = 0.08f;

        [Header("Freeze Options")]
        [SerializeField, Tooltip("Lock rotation during knockback.")]
        private bool hardRotationLockOnFreeze = true;
        [SerializeField, Tooltip("Disable animator root motion during knockback.")]
        private bool disableAnimatorRootMotionOnFreeze = true;

        [Header("Particles")]
        public ParticleSystem landingParticlePrefab;
        public Vector3 landingParticleOffset = Vector3.zero;

        // Teleport state
        private bool teleportInProgress = false;
        private SwipeDirection? _queuedPortalExitDirection = null;
        private Vector3 _preservedEntryForward = Vector3.forward;

        // Freeze state
        private Quaternion _frozenRotation;
        private Animator _cachedAnimator;
        private bool _cachedAnimatorRootMotion;
        public bool _isFrozen = false;

        // Tweens
        private Tween _knockbackTween;
        private Tween _jumpTween;
        private Tween _teleportSeq;

        // References
        private PlayerController _playerController;
        private Coroutine _wallHitCo;

        // Movement state
        private Vector3 _movementDirection = Vector3.zero;
        private bool _isIntroPlaying = false;
        private float _lastSwipeTime;
        private SwipeDirection _lastSwipeDirection = SwipeDirection.Up;

        // Coasting state
        private bool _isCoasting = false;
        private float _coastingDeceleration = 3f;

        #region Initialization

        public PlayerMovement Initialize(PlayerController playerController)
        {
            _playerController = playerController;
            Speed = movementSpeed;
            _currentTurnSpeed = baseTurnSpeed;
            _spawnScale = transform.localScale;
            _spawnWorldScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
            if (_cachedAnimator == null)
                _cachedAnimator = GetComponentInChildren<Animator>();
            return this;
        }

        public void BindToGameplay(IGameplaySpeedService speedService)
        {
            _speedService = speedService;
            _isActive = true;
            _firstMoveNotified = false;
            _playerSpeedMultiplier = (_speedService != null) ? Mathf.Max(0f, _speedService.PlayerSpeedMultiplier) : 1f;
            if (_speedService != null)
            {
                _speedService.OnPlayerSpeedMultiplierChanged += HandlePlayerSpeedMultiplierChanged;
            }
        }

        public void UnbindFromGameplay()
        {
            _isActive = false;
            if (_speedService != null)
            {
                _speedService.OnPlayerSpeedMultiplierChanged -= HandlePlayerSpeedMultiplierChanged;
            }
            _speedService = null;
            _movementDirection = Vector3.zero;
            Speed = 0f;
        }

        private void HandlePlayerSpeedMultiplierChanged(float mult)
        {
            _playerSpeedMultiplier = Mathf.Max(0f, mult);
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (teleportInProgress) return;

            if (_isCoasting)
            {
                UpdateCoasting();
                return;
            }

            if (_isActive)
            {
                if (_isFrozen)
                {
                    if (hardRotationLockOnFreeze)
                    {
                        transform.rotation = _frozenRotation;
                    }
                    return;
                }

                if (_isIntroPlaying) return;

                if (!Mathf.Approximately(_externalAccelPer60, 0f))
                {
                    float perSecond = _externalAccelPer60 / 60f;
                    movementSpeed += perSecond * Time.deltaTime;
                }
                Move();
                RotateTowardsMovement();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            CancelInvoke();
            KillAllTweens();
            _turnBoostRoutine = null;
            _wallHitCo = null;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            CancelInvoke();
            KillAllTweens();
            _turnBoostRoutine = null;
            _wallHitCo = null;
        }

        private void KillAllTweens()
        {
            if (_jumpTween != null && _jumpTween.IsActive()) { _jumpTween.Kill(false); _jumpTween = null; }
            if (_knockbackTween != null && _knockbackTween.IsActive()) { _knockbackTween.Kill(false); _knockbackTween = null; }
            if (_teleportSeq != null && _teleportSeq.IsActive()) { _teleportSeq.Kill(false); _teleportSeq = null; }
        }

        #endregion

        #region Movement

        private void Move()
        {
            if (_isFrozen || _isIntroPlaying) return;
            Speed = movementSpeed * Mathf.Max(0f, _playerSpeedMultiplier);
            transform.position += _movementDirection * (Time.deltaTime * Speed);
        }

        private void RotateTowardsMovement()
        {
            if (_isFrozen) return;
            if (_movementDirection.sqrMagnitude < 0.0001f) return;

            Vector3 fwd = new Vector3(_movementDirection.x, 0f, _movementDirection.z);
            if (fwd.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
            transform.rotation = targetRot;
        }

        public void SetDirection(Vector3 dir)
        {
            EnsureFirstMoveNotified();
            if (!IsAlive() || _isFrozen || _isIntroPlaying) return;
            if (dir.sqrMagnitude < 0.0001f) return;
            _movementDirection = SnapToCardinal(dir);
            StartTurnBoost();
        }

        public void InjectSwipe(SwipeDirection swipe)
        {
            EnsureFirstMoveNotified();
            ChangeDirection(swipe);
        }

        private void ChangeDirection(SwipeDirection swipeDirection)
        {
            EnsureFirstMoveNotified();
            if (!IsAlive()) return;

            if (teleportInProgress)
            {
                _queuedPortalExitDirection = swipeDirection;
                return;
            }

            if (_isFrozen || _isIntroPlaying) return;

            _lastSwipeTime = Time.time;
            _lastSwipeDirection = swipeDirection;

            Vector3 targetDir = SwipeToWorld(swipeDirection);
            _movementDirection = SnapToCardinal(targetDir);
            StartTurnBoost();
        }

        private Vector3 SnapToCardinal(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 normalized = dir.normalized;
            if (Mathf.Abs(normalized.x) > Mathf.Abs(normalized.z))
            {
                return new Vector3(Mathf.Sign(normalized.x), 0, 0);
            }
            else
            {
                return new Vector3(0, 0, Mathf.Sign(normalized.z));
            }
        }

        private void StartTurnBoost()
        {
            if (_turnBoostRoutine != null)
            {
                StopCoroutine(_turnBoostRoutine);
                _turnBoostRoutine = null;
            }
        }

        public void ChangeSpeed(float changeAmount) => Speed += changeAmount;

        public void SetBaseSpeed(float val) => movementSpeed = val;

        public void AddPlayerSpeed(float delta) => movementSpeed += delta;

        public void SetExternalAccelerationPer60Sec(float value) => _externalAccelPer60 = value;

        public void SetPlayerSize(int percent)
        {
            float k = 1f + (percent / 100f);
            transform.localScale = _spawnScale * k;

            const float baseContactYAtScale1 = 0.225f;
            float currentWorldScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
            float ratio = currentWorldScaleY / _spawnWorldScaleY;
            float worldContactY = baseContactYAtScale1 * ratio;

            Vector3 pos = transform.position;
            pos.y = worldContactY;
            transform.position = pos;
        }

        public void StopImmediately()
        {
            // Reserved for external stop calls
        }

        #endregion

        #region Coasting

        public void StartCoasting()
        {
            _isCoasting = true;
        }

        public IEnumerator StopCompletely(float duration)
        {
            float startSpeed = Speed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easedT = 1f - Mathf.Pow(1f - t, 2f);
                Speed = Mathf.Lerp(startSpeed, 0f, easedT);
                movementSpeed = Speed;
                yield return null;
            }

            Speed = 0f;
            movementSpeed = 0f;
            _movementDirection = Vector3.zero;
            _isCoasting = false;
        }

        private void UpdateCoasting()
        {
            if (!_isCoasting) return;
            Speed = Mathf.Max(0, Speed - _coastingDeceleration * Time.deltaTime);
            movementSpeed = Speed;
            transform.position += Vector3.forward * Speed * Time.deltaTime;
        }

        #endregion

        #region Jumping

        public void Jump(float jumpHeight)
        {
            EnsureFirstMoveNotified();
            if (!IsAlive() || _isFrozen || _isIntroPlaying) return;
            if (_jumpTween != null && _jumpTween.IsActive()) return;

            IsJumping = true;

            Transform posTarget = transform;
            Transform scaleTarget = visualRoot != null ? visualRoot : transform;

            float refSpd = Mathf.Max(0.01f, referenceSpeed);
            float curSpd = Mathf.Max(0.01f, Speed);
            float totalAir = Mathf.Clamp(baseAirTime * (refSpd / curSpd), minAirTime, maxAirTime);
            totalAir *= Mathf.Max(0.05f, airtimeFactor);

            float upDur = totalAir * ascentFraction;
            float downDur = totalAir - upDur;

            float startY = posTarget.position.y;
            float apexY = startY + jumpHeight;

            var seq = DOTween.Sequence();

            seq.Append(posTarget.DOMoveY(apexY, upDur).SetEase(ascentEase));
            if (enableJumpArcScale)
                seq.Join(scaleTarget.DOScale(Vector3.one * Mathf.Max(0.01f, jumpArcScalePeak), upDur).SetEase(jumpArcEaseUp));

            seq.Append(posTarget.DOMoveY(startY, downDur).SetEase(descentEase));
            if (enableJumpArcScale)
                seq.Join(scaleTarget.DOScale(Vector3.one, downDur).SetEase(jumpArcEaseDown));

            seq.AppendCallback(() => { PlayLandingParticle(); IsJumping = false; });

            if (landingSquash)
            {
                seq.Append(scaleTarget.DOScale(squashScale, squashDuration));
                seq.Append(scaleTarget.DOScale(Vector3.one, squashDuration));
            }

            _jumpTween = seq;
            _jumpTween.OnComplete(() => { _jumpTween = null; });
            _jumpTween.OnKill(() => { IsJumping = false; });
        }

        public void JumpCustom(float jumpHeight, float airTimeMultiplier = 1.15f, float scaleMultiplier = 1.35f)
        {
            if (!IsAlive() || _isFrozen) return;

            if (_jumpTween != null && _jumpTween.IsActive()) { _jumpTween.Kill(false); _jumpTween = null; }

            IsJumping = true;

            Transform posTarget = transform;
            Transform scaleTarget = visualRoot != null ? visualRoot : transform;

            float refSpd = Mathf.Max(0.01f, referenceSpeed);
            float curSpd = Mathf.Max(0.01f, Speed);
            float totalAir = Mathf.Clamp(baseAirTime * (refSpd / curSpd), minAirTime, maxAirTime);
            totalAir *= Mathf.Max(0.05f, airtimeFactor) * Mathf.Max(0.1f, airTimeMultiplier);

            float upDur = totalAir * ascentFraction;
            float downDur = totalAir - upDur;

            const float groundY = 0.25f;
            float startY = groundY;
            float apexY = Mathf.Max(posTarget.position.y, startY) + jumpHeight;

            float peakScale = Mathf.Max(0.01f, jumpArcScalePeak * Mathf.Max(1f, scaleMultiplier));

            var seq = DOTween.Sequence();

            seq.Append(posTarget.DOMoveY(apexY, upDur).SetEase(ascentEase));
            if (enableJumpArcScale)
                seq.Join(scaleTarget.DOScale(Vector3.one * peakScale, upDur).SetEase(jumpArcEaseUp));

            seq.Append(posTarget.DOMoveY(startY, downDur).SetEase(descentEase));
            if (enableJumpArcScale)
                seq.Join(scaleTarget.DOScale(Vector3.one, downDur).SetEase(jumpArcEaseDown));

            seq.AppendCallback(() => { PlayLandingParticle(); IsJumping = false; });

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

        #endregion

        #region Intro Sequence

        public void PlayIntroSequence(float duration = 1.25f)
        {
            if (!IsAlive()) return;

            _isIntroPlaying = true;
            transform.rotation = Quaternion.Euler(0, 180, 0);

            Transform scaleTarget = visualRoot != null ? visualRoot : transform;
            Transform posTarget = transform;

            float startY = posTarget.position.y;
            float apexY = startY + 1.5f;

            float initialDelay = 0.25f;
            float activeDuration = duration - initialDelay;
            float halfDur = activeDuration * 0.5f;

            var seq = DOTween.Sequence();

            seq.AppendInterval(initialDelay);

            seq.Append(posTarget.DOMoveY(apexY, halfDur).SetEase(Ease.OutQuad));
            if (enableJumpArcScale)
                seq.Join(scaleTarget.DOScale(Vector3.one * jumpArcScalePeak, halfDur).SetEase(jumpArcEaseUp));

            float windUpDuration = halfDur * 0.4f;
            seq.Join(transform.DORotate(new Vector3(0, 200, 0), windUpDuration).SetEase(Ease.OutSine));

            float spinDuration = activeDuration - windUpDuration;
            spinDuration *= 0.9f;

            seq.Insert(initialDelay + windUpDuration, transform.DORotate(Vector3.zero, spinDuration, RotateMode.FastBeyond360).SetEase(Ease.InOutBack));

            seq.Append(posTarget.DOMoveY(startY, halfDur).SetEase(Ease.InQuad));
            if (enableJumpArcScale)
                seq.Join(scaleTarget.DOScale(Vector3.one, halfDur).SetEase(jumpArcEaseDown));

            seq.AppendCallback(() =>
            {
                PlayLandingParticle();
                _isIntroPlaying = false;
                transform.rotation = Quaternion.identity;
                var pos = transform.position;
                pos.y = 0.25f;
                transform.position = pos;
            });

            if (landingSquash)
            {
                seq.Append(scaleTarget.DOScale(squashScale, squashDuration));
                seq.Append(scaleTarget.DOScale(Vector3.one, squashDuration));
            }

            seq.SetLink(gameObject);
        }

        #endregion

        #region Teleport

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

            float prevSpeed = Speed;
            KillAllTweens();

            Speed = 0f;
            _movementDirection = Vector3.zero;

            if (disableAnimatorRootMotionOnFreeze)
            {
                if (_cachedAnimator == null) _cachedAnimator = GetComponentInChildren<Animator>();
                if (_cachedAnimator != null)
                {
                    _cachedAnimatorRootMotion = _cachedAnimator.applyRootMotion;
                    _cachedAnimator.applyRootMotion = false;
                }
            }

            // Spiral entry
            float duration = 0.5f;
            float turns = 2f;
            float startRadius = Mathf.Max(0.01f, teleportSpiralStartRadius * 0.5f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(turns * 18f), 12, 128);

            Vector3 up = Vector3.up;
            Vector3 radial = transform.position - entryCenter.position;
            if (radial.sqrMagnitude < 1e-4f) radial = transform.right;
            radial = Vector3.ProjectOnPlane(radial, up).normalized;

            var points = new Vector3[steps];
            for (int i = 0; i < steps; i++)
            {
                float a = (i + 1) / (float)steps;
                float eased = 1f - Mathf.Pow(1f - a, 3f);
                float radius = Mathf.Lerp(startRadius, 0f, eased);
                float angle = eased * turns * Mathf.PI * 2f;

                Vector3 offset = Quaternion.AngleAxis(Mathf.Rad2Deg * angle, up) * radial * radius;
                Vector3 pos = entryCenter.position + offset + up * Mathf.Lerp(0f, -1f, eased);
                points[i] = pos;
            }

            var seq = DOTween.Sequence();

            seq.Append(transform.DOPath(points, duration, PathType.CatmullRom, PathMode.Full3D, 10, Color.white)
                .SetEase(Ease.OutCubic)
                .OnUpdate(() =>
                {
                    Vector3 toCenter = (entryCenter.position - transform.position);
                    if (toCenter.sqrMagnitude > 1e-4f)
                    {
                        Quaternion look = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, look, 0.25f);
                    }
                })
                .SetUpdate(UpdateType.Normal, false)
                .SetLink(gameObject));

            seq.AppendCallback(() =>
            {
                transform.position = exitTransform.position - Vector3.up * Mathf.Abs(exitBurrowDepth);
                Vector3 fwdFlat = Vector3.ProjectOnPlane(_preservedEntryForward, Vector3.up).normalized;
                if (fwdFlat.sqrMagnitude < 1e-4f) fwdFlat = transform.forward;
                transform.rotation = Quaternion.LookRotation(fwdFlat, Vector3.up);
            });

            // Exit spiral
            Vector3 preservedFlat = Vector3.ProjectOnPlane(_preservedEntryForward, Vector3.up).normalized;
            if (preservedFlat.sqrMagnitude < 1e-4f) preservedFlat = transform.forward;
            float outDuration = 0.5f;

            Vector3 exitUp = Vector3.up;
            int spiralSteps = Mathf.Clamp(Mathf.CeilToInt(turns * 18f), 12, 128);

            Vector3 exitRadial = Vector3.Cross(preservedFlat, exitUp).normalized;
            if (exitRadial.sqrMagnitude < 1e-4f) exitRadial = Vector3.right;

            var exitPoints = new Vector3[spiralSteps];
            for (int i = 0; i < spiralSteps; i++)
            {
                float t = (i + 1) / (float)spiralSteps;
                float eased = Mathf.Pow(t, 3f);

                float radius = Mathf.Lerp(0f, startRadius, eased);
                float angle = eased * turns * Mathf.PI * 2f;

                Vector3 offset = Quaternion.AngleAxis(Mathf.Rad2Deg * angle, exitUp) * exitRadial * radius;
                float heightProgress = t;
                Vector3 pos = exitTransform.position
                    + offset
                    + exitUp * Mathf.Lerp(-Mathf.Abs(exitBurrowDepth), 0f, heightProgress);

                exitPoints[i] = pos;
            }

            seq.Append(transform.DOPath(exitPoints, outDuration, PathType.CatmullRom, PathMode.Full3D, 10, Color.white)
                .SetEase(Ease.InOutQuad)
                .OnUpdate(() =>
                {
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

            Transform scaleTarget = visualRoot != null ? visualRoot : transform;
            var scaleSeq = DOTween.Sequence()
                .Append(scaleTarget.DOScale(Vector3.one * Mathf.Max(0.01f, jumpArcScalePeak), outDuration * 0.5f).SetEase(jumpArcEaseUp))
                .Append(scaleTarget.DOScale(Vector3.one, outDuration * 0.5f).SetEase(jumpArcEaseDown))
                .SetUpdate(UpdateType.Normal, false)
                .SetLink(gameObject);
            seq.Join(scaleSeq);

            seq.AppendCallback(() => PlayLandingParticle());

            _teleportSeq = seq;

            yield return _teleportSeq.WaitForCompletion();

            Speed = prevSpeed;

            Vector3 exitDirection = preservedFlat;
            if (_queuedPortalExitDirection.HasValue)
            {
                exitDirection = SnapToCardinal(SwipeToWorld(_queuedPortalExitDirection.Value));
                _queuedPortalExitDirection = null;
            }

            _movementDirection = exitDirection;

            if (disableAnimatorRootMotionOnFreeze && _cachedAnimator != null)
                _cachedAnimator.applyRootMotion = _cachedAnimatorRootMotion;

            teleportInProgress = false;
            _teleportSeq = null;
        }

        #endregion

        #region Wall Hit

        public void WallHitFeedback(Vector3 hitPoint, Vector3 hitNormal)
        {
            _frozenRotation = transform.rotation;

            Vector3 moveDir = _movementDirection.normalized;
            if (moveDir.sqrMagnitude < 0.01f) moveDir = -transform.forward;

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
            Vector3 bounceDir = Vector3.Reflect(-movementDirection, hitNormal);
            bounceDir = new Vector3(bounceDir.x, 0, bounceDir.z).normalized;

            if (wallHitParticlePrefab != null)
            {
                try
                {
                    var fx = Instantiate(wallHitParticlePrefab, contactPoint, Quaternion.LookRotation(hitNormal));
                    fx.Play();
                    Destroy(fx.gameObject, 2f);
                }
                catch (Exception) { }
            }

            if (_knockbackTween != null && _knockbackTween.IsActive()) _knockbackTween.Kill(false);

            Transform scaleTarget = visualRoot != null ? visualRoot : transform;
            var seq = DOTween.Sequence();

            seq.Append(scaleTarget.DOScale(wallSquashScale, 0.08f).SetEase(Ease.InQuad));

            Vector3 bounceEnd = transform.position + bounceDir * bounceDistance;
            bounceEnd.y = transform.position.y;

            seq.Append(transform.DOJump(bounceEnd, bounceHeight, 1, bounceDuration).SetEase(Ease.OutQuad));
            seq.Join(scaleTarget.DOScale(Vector3.one, bounceDuration).SetEase(Ease.OutBack));

            _knockbackTween = seq;
            seq.SetLink(gameObject);

            yield return seq.WaitForCompletion();

            _wallHitCo = null;
        }

        #endregion

        #region Particles

        private void PlayLandingParticle()
        {
            if (landingParticlePrefab == null) return;
            try
            {
                var pos = transform.position + landingParticleOffset;
                pos.y = 0f;

                var fx = Instantiate(landingParticlePrefab);
                fx.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90f, 0f, 0f));
                fx.Play(true);

                var main = fx.main;
                float life = main.duration + (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                    ? Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax)
                    : main.startLifetime.constant);
                Destroy(fx.gameObject, Mathf.Max(0.1f, life + 0.1f));
            }
            catch (Exception) { }
        }

        #endregion

        #region Helpers

        private bool IsAlive() => this != null && isActiveAndEnabled;

        private void EnsureFirstMoveNotified()
        {
            if (_firstMoveNotified) return;
            _firstMoveNotified = true;
            _speedService?.NotifyFirstPlayerMove();
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

        #endregion
    }
}
