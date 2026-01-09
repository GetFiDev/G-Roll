using UnityEngine;
using DG.Tweening;

public class TriggerableDoor : Wall
{
    [Header("Door Settings")]
    public Transform movingPart;
    [Tooltip("Transform representing the fully closed position.")]
    public Transform closedTransform;
    [Tooltip("Transform representing the fully open position.")]
    public Transform openTransform;
    [Tooltip("How long the door stays open.")]
    public float openDuration = 2f;

    private float _timer;
    private bool _isOpen;
    private Tween _moveTween;

    private void Start()
    {
        if (movingPart == null || closedTransform == null || openTransform == null)
        {
            Debug.LogError("TriggerableDoor: Missing references!", this);
            return;
        }

        // Start closed
        movingPart.position = closedTransform.position;
        movingPart.rotation = closedTransform.rotation;
        _isOpen = false;

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<TriggerableDoorCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<TriggerableDoorCollisionProxy>();
            proxy.owner = this;
        }
    }

    private void Update()
    {
        if (_isOpen)
        {
            _timer += Time.deltaTime;
            if (_timer >= openDuration)
            {
                CloseDoor();
            }
        }
    }

    public void Trigger()
    {
        // If already open, just reset the timer
        if (_isOpen)
        {
            _timer = 0f;
            return;
        }

        // Otherwise open it
        OpenDoor();
    }

    private void OpenDoor()
    {
        _isOpen = true;
        _timer = 0f;

        _moveTween?.Kill();
        // Move to open transform (Pos + Rot) in 0.2s
        Sequence seq = DOTween.Sequence();
        seq.Join(movingPart.DOMove(openTransform.position, 0.2f));
        seq.Join(movingPart.DORotateQuaternion(openTransform.rotation, 0.2f));
        _moveTween = seq;
    }

    private void CloseDoor()
    {
        _isOpen = false;
        _timer = 0f;

        _moveTween?.Kill();
        // Move to closed transform (Pos + Rot) in 0.2s
        Sequence seq = DOTween.Sequence();
        seq.Join(movingPart.DOMove(closedTransform.position, 0.2f));
        seq.Join(movingPart.DORotateQuaternion(closedTransform.rotation, 0.2f));
        _moveTween = seq;
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

    private void OnDestroy() {
        _moveTween?.Kill();
    }
}

// Helper component added automatically
public class TriggerableDoorCollisionProxy : MonoBehaviour
{
    public TriggerableDoor owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyCollisionEnter(collision);
    }
}
