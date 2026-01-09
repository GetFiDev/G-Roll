using UnityEngine;
using DG.Tweening;

public class RotatorHammer : Wall
{
    [Header("Hammer Settings")]
    [Tooltip("The actual object that will rotate.")]
    public Transform movingPart;
    [Tooltip("Transform defining the first target rotation.")]
    public Transform rotationTransform1;
    [Tooltip("Transform defining the second target rotation.")]
    public Transform rotationTransform2;
    [Tooltip("Duration of one swing from Rotation A to Rotation B.")]
    public float duration = 1f;
    [Tooltip("Animation curve for the rotation movement.")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Particle Settings")]
    public bool enableFirstParticle;
    public ParticleSystem firstParticle;
    public bool enableSecondParticle;
    public ParticleSystem secondParticle;

    private Sequence _hammerSequence;

    private void Start()
    {
        if (movingPart == null || rotationTransform1 == null || rotationTransform2 == null)
        {
            Debug.LogError("RotatorHammer: Missing references!", this);
            return;
        }

        // Ensure starting rotation
        movingPart.rotation = rotationTransform1.rotation;

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<RotatorHammerCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<RotatorHammerCollisionProxy>();
            proxy.owner = this;
        }

        StartHammerCycle();
    }

    private void StartHammerCycle()
    {
        _hammerSequence?.Kill();
        _hammerSequence = DOTween.Sequence();

        // 1. Move to Rotation 1 (Current rotation of Transform 1)
        // Note: Using RotateMode.FastBeyond360 if full spin needed, but mostly standard is fine.
        _hammerSequence.Append(movingPart.DORotateQuaternion(rotationTransform1.rotation, duration).SetEase(movementCurve));
        
        // Callback 1
        _hammerSequence.AppendCallback(() => 
        {
            if (enableFirstParticle && firstParticle != null) firstParticle.Play();
        });

        // 2. Move to Rotation 2 (Current rotation of Transform 2)
        _hammerSequence.Append(movingPart.DORotateQuaternion(rotationTransform2.rotation, duration).SetEase(movementCurve));

        // Callback 2
        _hammerSequence.AppendCallback(() => 
        {
            if (enableSecondParticle && secondParticle != null) secondParticle.Play();
        });

        // 3. Loop: Recursive call to fetch fresh rotations next cycle
        _hammerSequence.OnComplete(StartHammerCycle);
    }

    // Public methods for the proxy to call
    public void OnProxyTriggerEnter(Collider other)
    {
        if (expectTrigger) NotifyPlayer(other);
    }

    public void OnProxyCollisionEnter(Collision collision)
    {
        if (!expectTrigger) NotifyPlayer(collision.collider);
    }

    private void OnDestroy()
    {
        _hammerSequence?.Kill();
    }
}

// Helper component added automatically to the moving part
public class RotatorHammerCollisionProxy : MonoBehaviour
{
    public RotatorHammer owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnProxyCollisionEnter(collision);
    }
}
