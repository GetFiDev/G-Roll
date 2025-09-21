using System.Collections;
using UnityEngine;

public class Fan : MonoBehaviour, IPlayerInteractable
{
    [Header("Fan Jump")]
    [SerializeField] private float fanJumpHeight = 5.0f;   // normal double tap’ten daha yüksek
    [SerializeField] private float reuseCooldown = 0.15f;  // çok hızlı üst üste tetiklenmesin
    [SerializeField] private bool lockWhileJumping = true; // oyuncu havadayken tekrar tetikleme

    [Header("FX (opsiyonel)")]
    [SerializeField] private ParticleSystem blowFX;
    [SerializeField] private AudioSource sfx;

    private bool _locked;

    public void OnInteract(PlayerController player)
    {
        if (player == null || _locked) return;

        var move = player.GetComponent<PlayerMovement>();
        if (move == null) return;

        // Oyuncu zaten havadaysa tekrar tetikleme (opsiyonel)
        if (lockWhileJumping && IsPlayerJumping(move)) return;

        // Daha güçlü zıplayış
        move.Jump(fanJumpHeight);

        // Basit FX/SFX
        if (blowFX != null) blowFX.Play();
        if (sfx != null) sfx.Play();

        // Reuse kilidi
        if (reuseCooldown > 0f) StartCoroutine(UnlockAfter(reuseCooldown));
    }

    private IEnumerator UnlockAfter(float t)
    {
        _locked = true;
        yield return new WaitForSeconds(t);
        _locked = false;
    }

    // PlayerMovement’te IsJumping exposed ise kullan; yoksa güvenli fallback
    private static bool IsPlayerJumping(PlayerMovement m)
    {
        var prop = typeof(PlayerMovement).GetProperty("IsJumping");
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            var v = prop.GetValue(m, null);
            if (v is bool b) return b;
        }
        return false;
    }
}
