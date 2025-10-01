using UnityEngine;

public class Teleport : MonoBehaviour, IPlayerInteractable
{
    public Teleport otherPortal;

    public void OnPlayerEnter(PlayerController player, Collider other)
    {
        if (otherPortal == null) return;
        var t = player.transform;
        t.position = otherPortal.transform.position;
        t.rotation = otherPortal.transform.rotation;
    }

    public void OnPlayerStay(PlayerController p, Collider o, float dt) { }
    public void OnPlayerExit(PlayerController p, Collider o) { }
}