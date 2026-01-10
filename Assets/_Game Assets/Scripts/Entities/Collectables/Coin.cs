using UnityEngine;
using MapDesignerTool; // for IMapConfigurable
using System.Collections.Generic;
using System.Globalization;

public class Coin : MonoBehaviour, IPlayerInteractable, IMapConfigurable
{
    [Header("Coin Settings")]
    public float value = 0.1f;
    public GameObject vfxOnCollect;

    private bool _collected = false;

    // ---- IPlayerInteractable (legacy/compat) ----
    public void OnPlayerEnter(PlayerController player, Collider other)
    {

    }
    public void OnPlayerStay(PlayerController p, Collider o, float dt) { }
    public void OnPlayerExit(PlayerController p, Collider o) { }

    // ---- Magnet/Trigger based pickup ----
    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        // Player tag veya parent'ta PlayerMagnet varlığına göre topla
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMagnet>() != null)
        {
            TryCollect();
        }
    }

    [Header("UI Notification")]
    public string notificationString;

    private void TryCollect()
    {
        if (_collected) return; // double-collect güvenliği
        _collected = true;      // önce işaretle -> yarış olasılığına karşı güvenli

        // Para ekle (varsa konum geç)
        var gm = GameplayManager.Instance;
        if (gm != null)
        {
            // Eğer AddCoins overload'u pozisyon alıyorsa onu kullan, yoksa değerli olanı çağır
            try { gm.AddCoins(value, transform.position, 1); }
            catch { gm.AddCoins(value); }
        }

        // Trigger Notification
        if (!string.IsNullOrEmpty(notificationString))
        {
            GameplayManager.TriggerCollectibleNotification(notificationString);
        }

        if (vfxOnCollect)
        {
            Instantiate(vfxOnCollect, transform.position, Quaternion.identity);
        }

        // Pooling için SetActive(false) daha güvenli
        gameObject.SetActive(false);
    }

    // --- IMapConfigurable Implementation ---

    public List<ConfigDefinition> GetConfigDefinitions()
    {
        return new List<ConfigDefinition>
        {
            new ConfigDefinition
            {
                key = "value",
                displayName = "Coin Value",
                type = ConfigType.FloatInput, // Use Input Field
                defaultValue = 0.1f // Default value
            }
        };
    }

    public void ApplyConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("value", out var val))
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                value = f;
            }
        }
    }
}