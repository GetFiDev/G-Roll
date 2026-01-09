using UnityEngine;
public class Wall : MonoBehaviour
{
    [Tooltip("Bu collider trigger olmalı veya Player tarafı trigger olmalı.")]
    public bool expectTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayer(other);
    }

    // Eğer bazı duvarlar trigger değilse, aşağıyı da açabilirsin.
    private void OnCollisionEnter(Collision collision)
    {
        if (!expectTrigger)
            NotifyPlayer(collision.collider);
    }

    protected virtual void NotifyPlayer(Collider other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // Normal'i kabaca hesapla (trigger'da contact normal yok)
        Vector3 playerPos = player.transform.position;
        Vector3 hitPoint  = other.ClosestPoint(playerPos);
        Vector3 dir       = (playerPos - hitPoint);
        Vector3 hitNormal = dir.sqrMagnitude > 1e-5f ? dir.normalized : -transform.forward;

        player.HitTheWall(hitPoint, hitNormal);
    }
}