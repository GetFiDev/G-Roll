using UnityEngine;

/// <summary>
/// Movement yönüne dön ve topu kat edilen mesafe kadar döndür.
/// NOT: Burada Unity Animator kullanılmıyor; "Animator" ismi tarihsel.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField, Tooltip("Dönen topun görsel kökü")]
    private Transform ballTransform;

    [Header("Turn / Roll")]
    [SerializeField, Tooltip("Top yarıçapı (m)")]
    private float ballRadius = 0.5f;
    [SerializeField, Tooltip("Karakterin yön değiştirme hızı (derece/sn)")]
    private float turnSpeedDegPerSec = 720f;
    [SerializeField, Tooltip("Hassasiyet için minimum hareket eşiği (m)")]
    private float minDeltaToRotate = 0.0001f;

    private PlayerController _playerController;
    private bool _isInitialized = false;
    private bool _isFrozen = false;
    private Vector3 _lastPosition;

    public PlayerAnimator Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        _isInitialized = true;
        _lastPosition = transform.position;
        return this;
    }

    /// <summary>Animasyonu (yön & dönme) dondur / geri aç.</summary>
    public void Freeze(bool freeze)
    {
        _isFrozen = freeze;
    }

    private void OnEnable()
    {
        _lastPosition = transform.position;
    }

    private void Update()
    {
        if (!_isInitialized || _isFrozen) return;
        UpdateRotationAndRoll();
    }

    private void UpdateRotationAndRoll()
    {
        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - _lastPosition;
        delta.y = 0f;

        // Hareket yoksa güncelleme
        if (delta.sqrMagnitude < (minDeltaToRotate * minDeltaToRotate))
        {
            _lastPosition = currentPos;
            return;
        }

        // 1) Rotate to face movement direction (which is always cardinal)
        Vector3 targetDirection = delta.normalized;
        if (targetDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRot, 
                turnSpeedDegPerSec * Time.deltaTime
            );
        }

        // 2) Kat edilen mesafe kadar topu yuvarla (dünya uzayında sağ ekseni etrafında)
        if (ballTransform != null)
        {
            float distance = delta.magnitude;
            float angleDeg = distance / Mathf.Max(0.0001f, ballRadius) * Mathf.Rad2Deg;
            Vector3 rollAxisWorld = transform.right; // düzlemsel yuvarlanma
            ballTransform.Rotate(rollAxisWorld, angleDeg, Space.World);
        }

        _lastPosition = currentPos;
    }

    // Eski kalıntılar için boş struct (derleme hatasını önler; referans yoksa kaldırılabilir)
    private struct AnimatorParameterKey { }
}