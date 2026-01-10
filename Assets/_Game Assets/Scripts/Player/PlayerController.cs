using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [ReadOnly, Required] public PlayerMovement playerMovement;
    [ReadOnly, Required] public PlayerAnimator playerAnimator;

    [Header("Booster")]
    [Tooltip("Booster aktif mi? GameplayLogicApplier yönetir.")]
    public bool isBoosterEnabled = false;

    [Tooltip("Booster açılırken tek seferlik patlama (Burst, Play One Shot).")]
    public ParticleSystem boosterActivationParticle;

    [Tooltip("Booster açık kaldığı sürece dönecek looping particle (Rate over Time).")]
    public ParticleSystem boosterLoopParticle;

    /// <summary>Booster başladı: tek seferlik patlama ve looping'i başlat.</summary>
    public void OnBoosterStart()
    {
        if (boosterActivationParticle != null)
        {
            boosterActivationParticle.Play(true);
        }
        if (boosterLoopParticle != null && !boosterLoopParticle.isPlaying)
        {
            boosterLoopParticle.Play(true);
        }
    }

    /// <summary>Booster bitti: looping'i durdur.</summary>
    public void OnBoosterEnd()
    {
        if (boosterLoopParticle != null && boosterLoopParticle.isPlaying)
        {
            boosterLoopParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    [Header("Wall Hit Flow")]
    [Tooltip("Çarpma anında koşuyu hemen durdur (hızı 0’a çek).")]
    public bool stopRunOnHit = true;

    [Tooltip("Duvara çarpıldığında bir kereden fazla tetiklenmesin.")]
    [SerializeField] private bool lockOnFirstWallHit = true;

    private bool _wallHit;

    // Spawn snapshot
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    /// <summary>
    /// Duvara çarpıldığında Wall tarafından çağrılır.
    /// Oyunu bitirme işlemi, çarpma animasyonu/feedback'i tetiklendikten sonra yapılır.
    /// </summary>
    public void HitTheWall(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_wallHit && lockOnFirstWallHit) return;
        _wallHit = true;

        // 1) Animasyonu dondur (görsel)
        if (playerAnimator != null) playerAnimator.Freeze(true);

        // 2) Hareketi dondur (input kapansın)
        if (playerMovement != null) playerMovement._isFrozen = true;

        // 3) Geri itiş / feedback
        if (playerMovement != null)
        {
            playerMovement.WallHitFeedback(hitPoint, hitNormal);
        }
        else
        {
            Debug.LogWarning("[PlayerController] playerMovement yok, knockback oynatılamadı.");
        }

        // 4) İsteğe bağlı: koşuyu hemen durdurmayı tercih ediyorsan (kamera akışını da kesmek için)
        if (stopRunOnHit)
        {
            // Gameplay hızını çarpan üzerinden tamamen kesmek istersen:
            // GameplayManager.Instance?.ApplyGameplaySpeedPercent(-1f); // multiplier -> 0 (tam duruş)
        }

        // 5) Bitişi GameplayManager’a devret (FAIL UI sekansı -> teardown)
        GameplayManager.Instance?.StartFailFlow();
    }

    /// <summary>
    /// Parametresiz çarpma bildirimi; mevcut forward/position'dan tahmini normal ile çağır.
    /// </summary>
    public void HitTheWall()
    {
        var hitNormal = -transform.forward; // tahmini geri yön
        HitTheWall(transform.position, hitNormal);
    }

    /// <summary>
    /// Hareketi anında kesmek için yardımcı.
    /// </summary>
    public void StopImmediately()
    {
        if (playerMovement != null)
            playerMovement.StopImmediately();

        // Eskiden GameManager/SetSpeed vardı. Yeni mimaride kamera hızını kesmek istersen:
        // GameplayManager.Instance?.ApplyGameplaySpeedPercent(-1f); // (opsiyonel)
    }

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>()?.Initialize(this);
        playerAnimator = GetComponentInChildren<PlayerAnimator>()?.Initialize(this);

        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    private void OnValidate()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
    }

    /// <summary>
    /// Player'ı başlangıç konum/rotasyonuna döndürür ve çarpışma akışını temizler.
    /// </summary>
    public void ResetPlayer()
    {
        _wallHit = false;
        if (playerMovement != null) playerMovement._isFrozen = false;

        // Animator donmuşsa aç
        if (playerAnimator != null) playerAnimator.Freeze(false);

        // Anlık hareketleri kes (yan hız vb.)
        if (playerMovement != null) playerMovement.StopImmediately();

        // Dön ve yerine koy
        transform.SetPositionAndRotation(_startPosition, _startRotation);
    }

    /// <summary>
    /// Revive için player state'ini resetle. Pozisyon dışarıdan ayarlanır.
    /// Başlangıç pozisyonuna dönmez, sadece wall hit ve freeze durumunu temizler.
    /// </summary>
    public void ResetPlayerForRevive()
    {
        _wallHit = false;
        
        if (playerMovement != null) 
        {
            playerMovement._isFrozen = false;
            playerMovement.StopImmediately();
        }
        
        if (playerAnimator != null) 
            playerAnimator.Freeze(false);
        
        Debug.Log("[PlayerController] Player reset for revive.");
    }
}