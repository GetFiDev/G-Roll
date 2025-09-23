using UnityEngine;
using DG.Tweening; // en üst using’lere ekle

public class PlayerCollision : MonoBehaviour
{
    private PlayerController _playerController;
    // PlayerCollision sınıfının İÇİNDE (fields bölümüne) ekle:
    [SerializeField] private PlayerMovement movement;        // Inspector’dan tak; yoksa Awake’te GetComponent
    [SerializeField] private float knockbackDistance = 1.25f;
    [SerializeField] private float knockbackDuration = 0.25f;
    [SerializeField] private Ease knockbackEase = Ease.OutCubic;
    [SerializeField] private bool endGameAfterKnockback = true;

    private bool _crashInProgress = false;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
    }

    public PlayerCollision Initialize(PlayerController playerController)
    {
        _playerController = playerController;

        return this;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Interactable"))
            return;

        // 1) Önce toplanabilir/etkileşilebilir nesneler (collectable vb.)
        var interactable = other.GetComponent<IPlayerInteractable>();
        if (interactable != null)
        {
            interactable.OnInteract(_playerController);
            return;
        }

        // 2) Engel ise: zıplıyorsak yok say, değilse knockback + finish
        if (IsObstacleCollider(other))
        {
            if (__PM_GetJumpingSafe(movement))
                return; // havadayken duvar/engel görmezden gel

            if (TryCrashWithKnockbackFromTrigger(other))
                return;
        }

        // 3) Diğer etkileşim türleri yoksa sessizce çık
    }
    
    // Jump durumunu güvenli okumak için (IsJumping property yoksa false döner)
    private static bool __PM_GetJumpingSafe(PlayerMovement m)
    {
        if (m == null) return false;
        var prop = typeof(PlayerMovement).GetProperty("IsJumping");
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            object v = prop.GetValue(m, null);
            if (v is bool b) return b;
        }
        return false;
    }

    private static bool IsObstacleCollider(Collider c)
    {
        return c != null && (c.GetComponent<Wall>() != null || c.GetComponent<Obstacle>() != null);
    }

    /// Mevcut handler’lardan çağıracağımız tek yer.
    /// Başarılıysa true döner ve handler’dan return edersin.
    private bool TryCrashWithKnockbackFromTrigger(Collider other)
    {
        if (_crashInProgress || movement == null) return false;

        // Duvar/engel mi? (tip kontrolünü kendi projenin türlerine göre genişletebilirsin)
        if (other.GetComponent<Wall>() == null && other.GetComponent<Obstacle>() == null)
            return false;

        _crashInProgress = true;

        // Trigger’da temas noktası yok; en yakın noktayla yaklaşık normal
        Vector3 closest = other.ClosestPoint(transform.position);
        Vector3 approxNormal = (transform.position - closest);
        if (approxNormal.sqrMagnitude < 0.0001f) approxNormal = -transform.forward;
        approxNormal.Normalize();

        movement.BeginCrashKnockback(
            approxNormal,
            distance: knockbackDistance,
            duration: knockbackDuration,
            ease: knockbackEase,
            onCompleted: () =>
            {
                if (endGameAfterKnockback)
                    GameManager.Instance.LevelFinish(false);
            }
        );

        return true;
    }

}