
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class PlayerMagnet : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Trigger collider used as magnet radius.")]
    [SerializeField] private SphereCollider magnetTrigger;
    [Tooltip("Coin layer mask to detect coins.")]
    [SerializeField] private LayerMask coinLayer;
    [Tooltip("Optional pickup particle prefab.")]
    [SerializeField] private ParticleSystem pickupParticle;
    [Tooltip("Optional pickup sound.")]
    [SerializeField] private AudioSource pickupSound;

    [Header("Settings")]
    [Tooltip("Default trigger radius at 100%.")]
    [SerializeField] private float baseRadius = 1f;
    [Tooltip("Attraction speed multiplier.")]
    [SerializeField] private float attractionSpeed = 10f;

    private Transform player;
    private bool initialized;

    private void Awake()
    {
        if (magnetTrigger == null)
            magnetTrigger = GetComponent<SphereCollider>();

        magnetTrigger.isTrigger = true;
        baseRadius = magnetTrigger.radius;
    }

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || player == null) return;

        // Find nearby coins and pull them towards player
        Collider[] hits = Physics.OverlapSphere(transform.position, magnetTrigger.radius, coinLayer, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            var coin = hit.transform;
            coin.position = Vector3.MoveTowards(coin.position, player.position, attractionSpeed * Time.fixedDeltaTime);
        }
    }

    public void ApplyPercent(int bonusPercent)
    {
        if (magnetTrigger == null) return;
        float k = 1f + (bonusPercent / 100f);
        magnetTrigger.radius = baseRadius * k;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & coinLayer) == 0) return;

        // Play FX
        if (pickupParticle != null)
            Instantiate(pickupParticle, other.transform.position, Quaternion.identity);

        if (pickupSound != null)
            pickupSound.Play();

        // Destroy coin object
        Destroy(other.gameObject);
    }
}
