using UnityEngine;
using DG.Tweening;

public class Piston : Wall
{
    [Header("Piston Settings")]
    [Tooltip("The actual object that will move.")]
    public Transform movingPart;
    [Tooltip("Transform defining the start position.")]
    public Transform startTransform;
    [Tooltip("Transform defining the target position.")]
    public Transform targetTransform;
    [Tooltip("Time in seconds between strikes.")]
    public float interval = 2f;

    [Header("Animation Settings")]
    public Ease strikeEase = Ease.OutCubic;
    public Ease returnEase = Ease.OutFlash;

    [Header("Visual Effects")]
    public ParticleSystem pistonParticle;

    private Sequence _pistonSequence;

    private void Start()
    {
        if (movingPart == null || startTransform == null || targetTransform == null)
        {
            Debug.LogError("Piston: Missing references!", this);
            return;
        }

        // Ensure starting position
        movingPart.position = startTransform.position;

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<PistonCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<PistonCollisionProxy>();
            proxy.pistonOwner = this;
        }

        StartPistonCycle();
    }

    // Public methods for the proxy to call
    public void OnProxyTriggerEnter(Collider other)
    {
        // Use the base Wall logic
        if (expectTrigger) NotifyPlayer(other);
    }

    public void OnProxyCollisionEnter(Collision collision)
    {
        // Use the base Wall logic
        if (!expectTrigger) NotifyPlayer(collision.collider);
    }

    private void StartPistonCycle()
    {
        // Kill previous sequence if any to be safe, though OnComplete handles standard flow.
        _pistonSequence?.Kill();

        _pistonSequence = DOTween.Sequence();

        // 1. Strike: Move movingPart to CURRENT targetTransform.position
        _pistonSequence.Append(movingPart.DOMove(targetTransform.position, 0.3f).SetEase(strikeEase));

        // 2. Play Particle AND Return
        // We us AppendCallback to play particle exactly when Strike ends.
        _pistonSequence.AppendCallback(() => 
        {
            if (pistonParticle != null) pistonParticle.Play();
        });

        // 3. Return: Move back to CURRENT startTransform.position
        _pistonSequence.Append(movingPart.DOMove(startTransform.position, 0.1f).SetEase(returnEase));

        // 4. Wait: Interval
        _pistonSequence.AppendInterval(interval);

        // 5. Recursion: Restart cycle to account for moving start/target positions
        _pistonSequence.OnComplete(StartPistonCycle);
    }

    private void OnDestroy()
    {
        _pistonSequence?.Kill();
    }
}

// Helper component added automatically to the moving part
public class PistonCollisionProxy : MonoBehaviour
{
    public Piston pistonOwner;

    private void OnTriggerEnter(Collider other)
    {
        if (pistonOwner != null) pistonOwner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (pistonOwner != null) pistonOwner.OnProxyCollisionEnter(collision);
    }
}
