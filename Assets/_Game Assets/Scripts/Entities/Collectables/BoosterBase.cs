using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Interfaces;
using UnityEngine;
using VContainer;

public abstract class BoosterBase : MonoBehaviour, IPlayerInteractable
{
    [Header("Booster Base")]
    public bool oneShot = true;
    public bool destroyOnUse = true;
    public GameObject vfxOnUse;
    public AudioClip sfxOnUse;
    public string requiredTag = "Player"; // istersen boş bırak

    [Header("Tracking / Marker")]
    [Tooltip("If true, this collectable will be counted as a Power-Up for analytics/achievements.")]
    public bool countsAsPowerUp = true;

    [Tooltip("Optional kind/category for debugging/telemetry (e.g., Magnet, Shield, ScoreBoost)")]
    public string powerUpKind = "generic";

    [Inject] protected IMessageBus _messageBus;

    protected bool _used;
    protected virtual void PlayFx()
    {
        if (vfxOnUse) Instantiate(vfxOnUse, transform.position, Quaternion.identity);
        if (sfxOnUse) AudioSource.PlayClipAtPoint(sfxOnUse, transform.position);
    }

    // Booster davranışı buraya yazılacak
    protected abstract void Apply(PlayerController player);

    [Header("UI Notification")]
    public string notificationString;

    public void OnPlayerEnter(PlayerController player, Collider other)
    {
        if (_used && oneShot) return;

        // Tag kontrolünü collider yerine Player root üzerinden yap (child collider'larda kaçmasın)
        if (!string.IsNullOrEmpty(requiredTag))
        {
            GameObject tagTarget = player != null
                ? player.gameObject
                : (other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject);

            if (!tagTarget.CompareTag(requiredTag))
                return;
        }

        _used = true;
        Apply(player);
        PlayFx();

        // Trigger Notification via MessageBus (UIGamePlay subscribes to this)
        if (!string.IsNullOrEmpty(notificationString))
        {
            _messageBus?.Publish(new PlayerCollectMessage(
                itemType: "Notification",
                amount: 0,
                position: transform.position
            ));
            Debug.Log($"[BoosterBase] Notification: {notificationString}");
        }

        // Notify gameplay logic for Power-Up tracking via MessageBus
        if (countsAsPowerUp)
        {
            _messageBus?.Publish(new PlayerCollectMessage(
                itemType: $"PowerUp_{powerUpKind}",
                amount: 1,
                position: transform.position
            ));
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(powerUpKind))
                Debug.Log($"[BoosterBase] Power-Up collected: {powerUpKind}");
#endif
        }

        if (destroyOnUse)
        {
            Destroy(gameObject); // setActive yerine tamamen yok et
        }
    }

    public void OnPlayerStay(PlayerController p, Collider o, float dt) { }
    public void OnPlayerExit(PlayerController p, Collider o) { }
}