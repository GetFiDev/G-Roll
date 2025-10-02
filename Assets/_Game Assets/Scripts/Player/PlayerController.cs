using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [ReadOnly, Required] public PlayerMovement playerMovement;
    [ReadOnly, Required] public PlayerAnimator playerAnimator;

    [Header("Booster")]
    [Tooltip("Booster aktif mi? GameplayManager tarafından yönetilir.")]
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
            // Burst particle -> tek seferlik Play
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

    /// <summary>
    /// O anki koşu hızını yüzdelik oran kadar ANINDA arttırır (kalıcı). Örn: percent=0.2 -> %20 artış.
    /// </summary>
    public void ApplyRunSpeedBoostPercentInstant(float percent)
    {
        if (playerMovement == null) return;
        float delta = playerMovement.Speed * percent;
        playerMovement.ChangeSpeed(delta);
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
    /// Oyunu bitirme (FAIL) işlemi, çarpma animasyonu/feedback'i BİTTİKTEN sonra yapılır.
    /// </summary>
    public void HitTheWall(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_wallHit && lockOnFirstWallHit) return;
        _wallHit = true;

        // 1) Koşuyu hemen durdur
        if (stopRunOnHit && GameplayManager.Instance != null)
            GameplayManager.Instance.SetSpeed(0f);

        // 2) Animator'u durdur (görsel olarak donsun)
        if (playerAnimator != null) playerAnimator.Freeze(true);

        // 3) Hareket tarafına tek adımlık geri itişi yaptır (XZ)
        if (playerMovement != null)
        {
            playerMovement.WallHitFeedback(hitNormal);
        }
        else
        {
            Debug.LogWarning("[PlayerController] playerMovement yok, knockback oynatılamadı.");
        }

        // 4) Oyunu anında bitir (FAIL akışını GameManager yönetir)
        GameManager.Instance?.EnterLevelComplete();
    }

    /// <summary>
    /// Parametresiz çarpma bildirimi; mevcut forward/position'dan tahmini normal ile çağır.
    /// </summary>
    public void HitTheWall()
    {
        var hitNormal = -transform.forward; // tahmini geri yön
        HitTheWall(transform.position, hitNormal);
    }

    // İhtiyaç duyabileceğin küçük yardımcılar:
    public void StopImmediately()
    {
        if (playerMovement != null)
            playerMovement.StopImmediately();
        GameplayManager.Instance?.SetSpeed(0f);
    }
    
    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>()?.Initialize(this);
        playerAnimator = GetComponentInChildren<PlayerAnimator>()?.Initialize(this);

        // capture spawn transform once at game start
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

        // Animator donmuşsa aç
        if (playerAnimator != null) playerAnimator.Freeze(false);

        // Anlık hareketleri kes (yan hız vb.)
        if (playerMovement != null) playerMovement.StopImmediately();

        // Dön ve yerine koy
        transform.SetPositionAndRotation(_startPosition, _startRotation);
    }
}