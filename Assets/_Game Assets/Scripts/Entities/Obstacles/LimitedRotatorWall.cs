using UnityEngine;
using DG.Tweening;

public class LimitedRotatorWall : Wall
{
    [Header("Rotator Settings")]
    [Tooltip("The actual object that will rotate.")]
    public Transform movingPart;
    [Tooltip("Transform defining the start rotation.")]
    public Transform limitTransform1;
    [Tooltip("Transform defining the end rotation.")]
    public Transform limitTransform2;
    [Tooltip("Duration of movement from limit 1 to limit 2.")]
    public float duration = 2f;

    private Sequence _rotatorSequence;

    private void Start()
    {
        if (movingPart == null || limitTransform1 == null || limitTransform2 == null)
        {
            Debug.LogError("LimitedRotatorWall: Missing references!", this);
            return;
        }

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<LimitedRotatorCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<LimitedRotatorCollisionProxy>();
            proxy.owner = this;
        }

        StartRotationCycle();
    }

    private void StartRotationCycle()
    {
        _rotatorSequence?.Kill();
        _rotatorSequence = DOTween.Sequence();

        // Ensure we are at start
        movingPart.rotation = limitTransform1.rotation;

        // 1. Rotate to Limit 2
        _rotatorSequence.Append(movingPart.DORotateQuaternion(limitTransform2.rotation, duration).SetEase(Ease.Linear));

        // 2. Rotate back to Limit 1
        _rotatorSequence.Append(movingPart.DORotateQuaternion(limitTransform1.rotation, duration).SetEase(Ease.Linear));

        // 3. Loop: Recursive call to fetch fresh rotations next cycle (supports dynamic limits)
        // Note: Using recursive OnComplete instead of SetLoops(-1) allows limits to move at runtime.
        _rotatorSequence.OnComplete(StartRotationCycle);
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
        _rotatorSequence?.Kill();
    }
}

// Helper component added automatically to the moving part
public class LimitedRotatorCollisionProxy : MonoBehaviour
{
    public LimitedRotatorWall owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnProxyCollisionEnter(collision);
    }
}
