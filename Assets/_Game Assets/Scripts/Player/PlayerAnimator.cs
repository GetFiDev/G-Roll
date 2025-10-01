using UnityEngine;

/// <summary>
/// Minimal, clean animator wrapper.
/// - Initialize() ile PlayerController'a bağlanır.
/// - Freeze(true) çağrıldığında animator tamamen durur (speed=0) ve root motion kapanır.
/// - Freeze(false) çağrıldığında önceki ayarlar geri yüklenir.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private PlayerController _player;
    private bool _isInitialized;
    private bool _isFrozen;

    private float _cachedSpeed = 1f;
    private bool _cachedApplyRootMotion = false;

    public PlayerAnimator Initialize(PlayerController player)
    {
        _player = player;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("[PlayerAnimator] Animator not found on children.", this);
            _isInitialized = false;
        }
        else
        {
            _cachedSpeed = Mathf.Max(0.01f, animator.speed);
            _cachedApplyRootMotion = animator.applyRootMotion;
            _isInitialized = true;
        }

        return this;
    }

    /// <summary>Animasyonu tamamen dondur / geri aç.</summary>
    public void Freeze(bool freeze)
    {
        if (!_isInitialized || animator == null) return;
        if (_isFrozen == freeze) return;

        _isFrozen = freeze;
        if (freeze)
        {
            _cachedSpeed = Mathf.Max(0.01f, animator.speed);
            _cachedApplyRootMotion = animator.applyRootMotion;

            animator.applyRootMotion = false;
            animator.speed = 0f;
        }
        else
        {
            animator.applyRootMotion = _cachedApplyRootMotion;
            animator.speed = _cachedSpeed;
        }
    }

    private void OnDisable()
    {
        // Beklenmedik disable'da donmuş kalmasın diye güvence
        if (_isFrozen) Freeze(false);
    }
}