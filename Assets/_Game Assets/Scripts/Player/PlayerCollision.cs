using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugTriggers = true;

    public PlayerController player; // sahneden atarsın; yoksa GetComponent ile buluruz

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
    }

    /// <summary>
    /// other üzerinde, parent'ında veya child'larında (öncelik: self > parent > children) IPlayerInteractable ara.
    /// </summary>
    private static bool TryFindInteractable(Collider other, out IPlayerInteractable interactable)
    {
        // 1) Aynı GameObject üzerinde
        if (other.TryGetComponent<IPlayerInteractable>(out interactable))
            return true;

        // 2) Parent zincirinde
        interactable = other.GetComponentInParent<IPlayerInteractable>();
        if (interactable != null)
            return true;

        // 3) Child'larda
        interactable = other.GetComponentInChildren<IPlayerInteractable>();
        return interactable != null;
    }

    // Coin'leri PlayerCollision akışından hariç tut (magnet yönetecek)
    private static bool IsCoinCollider(Collider other)
    {
        // Coin bileşeni ya da parent/child'ında varsa coin kabul et
        if (other.GetComponent<Coin>() != null) return true;
        if (other.GetComponentInParent<Coin>() != null) return true;
        if (other.GetComponentInChildren<Coin>() != null) return true;
        // Alternatif: Tag/Layer ile de genişletilebilir
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsCoinCollider(other)) return; // coin'leri magnet toplayacak, es geç
        if (TryFindInteractable(other, out var interactable))
        {
            if (debugTriggers) Debug.Log($"[PlayerCollision] Enter -> other={other.name} layer={other.gameObject.layer} on {gameObject.name}", other);
            interactable.OnPlayerEnter(player, other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsCoinCollider(other)) return; // coin'leri magnet toplayacak, es geç
        if (TryFindInteractable(other, out var interactable))
        {
            if (debugTriggers) Debug.Log($"[PlayerCollision] Stay -> other={other.name} layer={other.gameObject.layer} on {gameObject.name}", other);
            interactable.OnPlayerStay(player, other, Time.deltaTime);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsCoinCollider(other)) return; // coin'leri magnet toplayacak, es geç
        if (TryFindInteractable(other, out var interactable))
        {
            if (debugTriggers) Debug.Log($"[PlayerCollision] Exit -> other={other.name} layer={other.gameObject.layer} on {gameObject.name}", other);
            interactable.OnPlayerExit(player, other);
        }
    }
}