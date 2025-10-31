using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class UIPlayerStatsHandler : MonoBehaviour
{
    [SerializeField] private bool refreshOnInitialize = true;
    [SerializeField] private List<UIPlayerStatDisplay> displays = new List<UIPlayerStatDisplay>();

    private const string KEY_COMBO_POWER              = "comboPower";                     // int (delta over base 1)
    private const string KEY_COIN_MULTIPLIER_PERCENT  = "coinMultiplierPercent";          // int %
    private const string KEY_GAMEPLAY_SPEED_PERCENT   = "gameplaySpeedMultiplierPercent"; // int % (remote JSON anahtarı)
    private const string KEY_PLAYER_ACCELERATION      = "playerAcceleration";             // float
    private const string KEY_PLAYER_SPEED_ADD         = "playerSpeed";                    // float (additive)
    private const string KEY_PLAYER_SIZE_PERCENT      = "playerSizePercent";              // int %
    private const string KEY_MAGNET_POWER_PERCENT     = "magnetPowerPercent";             // int % (new)

    [System.Serializable]
    private class StatsDTO
    {
        public int   comboPower = 0;                          // JSON: delta, UI'da integer göster
        public int   coinMultiplierPercent = 0;               // %
        public int   gameplaySpeedMultiplierPercent = 0;      // %
        public float playerAcceleration = 0.1f;               // dakikada
        public float playerSpeed = 0f;                        // additive
        public int   playerSizePercent = 0;                   // %
        public int   magnetPowerPercent = 0;                  // % (new)
    }

    public void OnEnable()
    {
        var found = GetComponentsInChildren<UIPlayerStatDisplay>(true);
        displays = new List<UIPlayerStatDisplay>(found);
        StartCoroutine(ShowAllRoutine());
    }

    private IEnumerator ShowAllRoutine()
    {
        if (refreshOnInitialize && PlayerStatsRemoteService.Instance != null)
        {
            yield return PlayerStatsRemoteService.Instance.RefreshLatestCoroutine();
        }
        string json = PlayerStatsRemoteService.Instance != null ? PlayerStatsRemoteService.Instance.LatestStatsJson : string.Empty;
        StatsDTO dto = string.IsNullOrWhiteSpace(json) ? new StatsDTO() : JsonUtility.FromJson<StatsDTO>(json);

        // small frame defer to ensure UI is ready
        yield return null;

        for (int i = 0; i < displays.Count; i++)
        {
            var d = displays[i];
            if (d == null) continue;

            string key = (d.statKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key)) continue;

            // normalize key
            key = key.Replace(" ", string.Empty);

            bool isPercent = false;
            bool isDecimal = false;
            string valueStr = "0";

            switch (key)
            {
                case KEY_COMBO_POWER:
                    valueStr = dto.comboPower.ToString(CultureInfo.InvariantCulture);
                    isPercent = true;
                    isDecimal = false; // integer göster
                    break;
                case KEY_COIN_MULTIPLIER_PERCENT:
                    valueStr = dto.coinMultiplierPercent.ToString(CultureInfo.InvariantCulture);
                    isPercent = true;
                    isDecimal = false;
                    break;
                case KEY_GAMEPLAY_SPEED_PERCENT:
                    valueStr = dto.gameplaySpeedMultiplierPercent.ToString(CultureInfo.InvariantCulture);
                    isPercent = true;
                    isDecimal = false;
                    break;
                case KEY_PLAYER_ACCELERATION:
                    valueStr = dto.playerAcceleration.ToString(CultureInfo.InvariantCulture);
                    isPercent = false;
                    isDecimal = true; // tek ondalık
                    break;
                case KEY_PLAYER_SPEED_ADD:
                    valueStr = dto.playerSpeed.ToString(CultureInfo.InvariantCulture);
                    isPercent = false;
                    isDecimal = true; // tek ondalık
                    break;
                case KEY_PLAYER_SIZE_PERCENT:
                    valueStr = dto.playerSizePercent.ToString(CultureInfo.InvariantCulture);
                    isPercent = true;
                    isDecimal = false;
                    break;
                case KEY_MAGNET_POWER_PERCENT:
                    valueStr = dto.magnetPowerPercent.ToString(CultureInfo.InvariantCulture);
                    isPercent = true;
                    isDecimal = false;
                    break;
                default:
                    // Fallback: if key ends with Percent, assume percent; else decimal
                    if (key.EndsWith("Percent"))
                    {
                        isPercent = true; isDecimal = false; valueStr = "0";
                    }
                    else
                    {
                        isPercent = false; isDecimal = true; valueStr = "0";
                    }
                    break;
            }

            d.ShowStat(valueStr, isPercent, isDecimal);
        }
    }
}
