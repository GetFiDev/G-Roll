using UnityEngine;
using DG.Tweening;

public class TriggerPushButton : MonoBehaviour
{
    [Header("Button Settings")]
    [Tooltip("The Mesh/Object that physically moves down.")]
    public Transform buttonMovingPart;
    [Tooltip("Transform for the unpressed (up) state.")]
    public Transform startTransform;
    [Tooltip("Transform for the pressed (down) state.")]
    public Transform targetTransform;

    [Header("Interaction")]
    [Tooltip("The door this button controls.")]
    public TriggerableDoor targetDoor;

    private Sequence _pressSequence;

    private void Start()
    {
        if (buttonMovingPart == null || startTransform == null || targetTransform == null)
        {
            Debug.LogError("TriggerPushButton: Missing references!", this);
            return;
        }

        // Ensure start pos
        buttonMovingPart.position = startTransform.position;

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<TriggerPushButtonProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<TriggerPushButtonProxy>();
            proxy.owner = this;
        }
    }

    // Changed to public so proxy can call it
    public void OnProxyTriggerEnter(Collider other)
    {
        // Check for player
        if (other.GetComponentInParent<PlayerController>() != null || other.CompareTag("Player"))
        {
            PressButton();
        }
    }

    private void PressButton()
    {
        // Notify the door immediately
        if (targetDoor != null)
        {
            targetDoor.Trigger();
        }

        // Animate Button: Down (0.2s) -> Up (0.2s)
        _pressSequence?.Kill();
        _pressSequence = DOTween.Sequence();

        // Move To Target
        _pressSequence.Append(buttonMovingPart.DOMove(targetTransform.position, 0.2f).SetEase(Ease.OutQuad));
        
        // Move Back To Start
        _pressSequence.Append(buttonMovingPart.DOMove(startTransform.position, 0.2f).SetEase(Ease.OutQuad));
    }

    private void OnDestroy()
    {
        _pressSequence?.Kill();
    }
}

// Helper component added automatically
public class TriggerPushButtonProxy : MonoBehaviour
{
    public TriggerPushButton owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && gameObject.activeInHierarchy) owner.OnProxyTriggerEnter(other);
    }
}
