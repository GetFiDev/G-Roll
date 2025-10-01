using UnityEngine;

public class Fan : MonoBehaviour, IPlayerInteractable
{
    public Vector3 forceDirection = Vector3.forward; // Dünya ekseninde yön
    public float forcePerSecond = 10f;               // m/sn kazancı gibi düşün
    public bool useLocalForward = true;              // objenin kendi forward’ı

    public void OnPlayerEnter(PlayerController p, Collider o) { }

    public void OnPlayerStay(PlayerController player, Collider other, float dt) { }

    public void OnPlayerExit(PlayerController p, Collider o) { }
}