using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class PlayerMagnet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SphereCollider magnetTrigger;   // isTrigger = true
    [SerializeField] private LayerMask coinLayer;
    [SerializeField] private Transform player;               // Initialize ile verilir

    [Header("Settings")]
    [SerializeField] private float baseRadius = 1f;          // 100% görsel boyutta 1
    [SerializeField] private float attractionSpeed = 10f;    // basit çekiş hızı

    private bool initialized;

    private void Awake()
    {
        if (magnetTrigger == null) magnetTrigger = GetComponent<SphereCollider>();
        magnetTrigger.isTrigger = true;

        // Eğer prefab'ta radius farklıysa onu baz al
        if (baseRadius <= 0f) baseRadius = Mathf.Max(0.01f, magnetTrigger.radius);
    }

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
        initialized = (player != null);
    }

    /// <summary>
    /// Magnet yarıçapını belirler:
    /// - Üzerine magnet bonus yüzdesi (magnetPct) çarpan olarak eklenir.
    /// - Negatif bonus görselin altına düşüremez (min = sadece görsel).
    /// </summary>
    private float _currentMagnetPct = 0f;
    private float _boosterMultiplier = 1f;

    /// <summary>
    /// Magnet yarıçapını belirler:
    /// - Üzerine magnet bonus yüzdesi (magnetPct) çarpan olarak eklenir.
    /// - Negatif bonus görselin altına düşüremez (min = sadece görsel).
    /// </summary>
    public void ApplySizeAndMagnet(int magnetPct)
    {
        _currentMagnetPct = magnetPct;
        UpdateRadius();
    }

    public void SetBoosterMultiplier(float multiplier)
    {
        _boosterMultiplier = multiplier;
        UpdateRadius();
    }

    private void UpdateRadius()
    {
        if (magnetTrigger == null) return;

        float magnetK = 1f + (_currentMagnetPct / 100f);         // ek magnet faktörü
        if (magnetK < 1f) magnetK = 1f;                  // negatif bonus görselin altına düşmesin

        magnetTrigger.radius = baseRadius * magnetK * _boosterMultiplier;
    }

    private void FixedUpdate()
    {
        if (!initialized || player == null) return;

        // Opsiyonel: yakın coin'leri player'a doğru çek (coin pickup coin'in kendi trigger'ında)
        var r = magnetTrigger.radius;
        var hits = Physics.OverlapSphere(transform.position, r, coinLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            var t = hits[i].transform;
            t.position = Vector3.MoveTowards(t.position, player.position, attractionSpeed * Time.fixedDeltaTime);
        }
    }
}