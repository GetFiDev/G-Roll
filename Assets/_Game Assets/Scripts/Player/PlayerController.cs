using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [ReadOnly, Required] public PlayerMovement playerMovement;
    [ReadOnly, Required] public PlayerAnimator playerAnimator;

    [Header("Wall Hit Flow")]
    [Tooltip("Çarpma feedback/animasyonu için bekleme süresi (s). PlayerMovement içindeki gerçek süreyle eşleştir.")]
    public float wallHitFeedbackWait = 0.45f;

    [Tooltip("Çarpma anında koşuyu hemen durdur (hızı 0’a çek).")]
    public bool stopRunOnHit = true;

    [Tooltip("Duvara çarpıldığında bir kereden fazla tetiklenmesin.")]
    [SerializeField] private bool lockOnFirstWallHit = true;

    private bool _wallHit;
    private Coroutine _wallHitFlowCo;

    /// <summary>
    /// Duvara çarpıldığında Wall tarafından çağrılır.
    /// Oyunu bitirme (FAIL) işlemi, çarpma animasyonu/feedback'i BİTTİKTEN sonra yapılır.
    /// </summary>
    public void HitTheWall(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_wallHit && lockOnFirstWallHit) return;
        _wallHit = true;

        // 1) Koşuyu hemen durdur (StopRun değil; o complete akışını tetikler)
        if (stopRunOnHit && GameplayManager.Instance != null)
            GameplayManager.Instance.SetSpeed(0f);

        // 2) Animator'u kökten durdur (child objede olsa dahi)
        if (playerAnimator != null) playerAnimator.Freeze(true);

        // 3) Hareket tarafına “durdur + sadece geriye knockback (XZ)” feedback’i ver
        if (playerMovement != null)
        {
            playerMovement.WallHitFeedback(hitNormal);
        }
        else
        {
            Debug.LogWarning("[PlayerController] playerMovement yok, sekme oynatılamadı.");
        }

        // 4) Feedback bittiğinde oyunu FAIL ile kapat
        if (_wallHitFlowCo != null) StopCoroutine(_wallHitFlowCo);
        _wallHitFlowCo = StartCoroutine(WallHitFlowAfterFeedback());
    }

    private IEnumerator WallHitFlowAfterFeedback()
    {
        // PlayerMovement içindeki gerçek sekans süresine göre ayarla
        yield return new WaitForSeconds(Mathf.Max(0f, wallHitFeedbackWait));

        // Animator'u eski haline getir (görsel kapanmadan önce istersen açık bırakabilirsin)
        if (playerAnimator != null) playerAnimator.Freeze(false);

        // Doğrudan GameManager üzerinden FAIL'e geç
        GameManager.Instance?.EnterLevelComplete();

        _wallHitFlowCo = null;
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
    }

    private void OnValidate()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
    }
}