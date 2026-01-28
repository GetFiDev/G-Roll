using GRoll.Gameplay.Player.Core;
using GRoll.Gameplay.Player.Interfaces;
using GRoll.Gameplay.Player.Movement;
using UnityEngine;

public class Fan : MonoBehaviour, IPlayerInteractable
{
    [Header("Fan Boost")]
    [Tooltip("Zıplama yüksekliği (m) – normal double tap'ten daha yüksek.")]
    public float boostJumpHeight = 3.5f;
    [Tooltip("Zıplama süresi (s) – normalden biraz daha uzun.")]
    public float boostJumpDuration = 0.65f;

    [Header("Direction (opsiyonel)")]
    [Tooltip("Objenin kendi forward'ı ile üfleme yönünü hizala. Bu sınıfta sadece zıplama uygulanır; yön itilmesi yapılmaz.")]
    public bool useLocalForward = true;

    public void OnPlayerEnter(PlayerController player, Collider other)
    {
        if (player == null) return;

        // PlayerMovement'ı runtime'da al (assembly boundary nedeniyle)
        var movement = player.GetComponent<PlayerMovement>();
        if (movement == null) return;

        // Fan'a girilir girilmez: double-tap benzeri ama daha güçlü bir zıplama uygula
        movement.JumpCustom(boostJumpHeight, boostJumpDuration);
    }

    public void OnPlayerStay(PlayerController player, Collider other, float dt) { }

    public void OnPlayerExit(PlayerController p, Collider o) { }
}