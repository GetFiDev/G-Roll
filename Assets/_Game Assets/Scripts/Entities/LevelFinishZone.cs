using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using UnityEngine;
using VContainer;

/// <summary>
/// Level finish zone - oyuncu bu alana girdiğinde level tamamlanır.
/// MessageBus üzerinden LevelCompleteMessage yayınlar.
/// </summary>
public class LevelFinishZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string requiredTag = "Player";
    [SerializeField] private bool triggerOnce = true;

    [Inject] private IMessageBus _messageBus;

    private bool _triggered;

    public event System.Action OnPlayerReachedFinish;

    /// <summary>
    /// Called by LevelFinishZoneTrigger when player enters the trigger zone.
    /// </summary>
    public void HandleTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (_triggered && triggerOnce) return;

        // Tag check
        GameObject tagTarget = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (!string.IsNullOrEmpty(requiredTag) && !tagTarget.CompareTag(requiredTag))
            return;

        _triggered = true;

        Debug.Log("[LevelFinishZone] Player reached finish zone");

        // Notify via event (for legacy listeners)
        OnPlayerReachedFinish?.Invoke();

        // Publish level complete message via MessageBus
        _messageBus?.Publish(new LevelCompleteMessage(transform.position));
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>
    /// Resets the trigger state (useful for level restart).
    /// </summary>
    public void ResetTrigger()
    {
        _triggered = false;
    }
}
