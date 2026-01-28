using System;
using GRoll.Core.Interfaces.Services;
using GRoll.Gameplay.Player.Movement;
using UnityEngine;

namespace GRoll.Gameplay.Player.Stats
{
    /// <summary>
    /// Handles player stat calculations and applies them at run start.
    /// Processes equipment and item bonuses from JSON configuration.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStatHandler : MonoBehaviour
    {
        [Header("Base Stats (Inspector defaults)")]
        [SerializeField] private int baseComboPower = 25;
        [SerializeField] private int baseCoinMultiplierPercent = 0;
        [SerializeField] private int baseMagnetPowerPercent = 0;
        [SerializeField] private int baseGameplaySpeedBonusPercent = 0;
        [SerializeField] private float basePlayerAccelerationPer60 = 0.1f;
        [SerializeField] private float basePlayerSpeedAdd = 0f;
        [SerializeField] private int basePlayerSizePercent = 0;

        private string _extrasJson = string.Empty;

        public int FinalComboPowerFactor { get; private set; }
        public int FinalCoinMultiplierPercent { get; private set; }
        public int FinalMagnetPowerPct { get; private set; }
        public int FinalGameplaySpeedPct { get; private set; }
        public float FinalPlayerAcceleration { get; private set; }
        public float FinalPlayerSpeedAdd { get; private set; }
        public int FinalPlayerSizePct { get; private set; }

        [Serializable]
        private class StatExtrasDTO
        {
            public int comboPower = int.MinValue;
            public int coinMultiplierPercent = int.MinValue;
            public int gameplaySpeedMultiplierPercent = int.MinValue;
            public float playerAcceleration = float.NaN;
            public float playerSpeed = float.NaN;
            public int playerSize = int.MinValue;
            public int playerSizePercent = int.MinValue;
            public int magnetPowerPercent = int.MinValue;
        }

        private StatExtrasDTO _parsed;

        private void Awake()
        {
            TryParseJson();
            ComputeFinals();
        }

        private void TryParseJson()
        {
            _parsed = null;
            var txt = (_extrasJson ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt)) return;
            try { _parsed = JsonUtility.FromJson<StatExtrasDTO>(txt); } catch { }
        }

        private void ComputeFinals()
        {
            int cpBase = baseComboPower;
            int cmpBase = baseCoinMultiplierPercent;
            int magBase = baseMagnetPowerPercent;
            int gspBase = baseGameplaySpeedBonusPercent;
            float accBase = basePlayerAccelerationPer60;
            float spdBase = basePlayerSpeedAdd;
            int sizeBase = basePlayerSizePercent;

            int cpExtra = 0;
            int cmpExtra = 0;
            int magExtra = 0;
            int gspExtra = 0;
            float accExtra = 0f;
            float spdExtra = 0f;
            int sizeExtra = 0;

            if (_parsed != null)
            {
                if (_parsed.comboPower != int.MinValue) cpExtra = _parsed.comboPower;
                if (_parsed.coinMultiplierPercent != int.MinValue) cmpExtra = _parsed.coinMultiplierPercent;
                if (_parsed.gameplaySpeedMultiplierPercent != int.MinValue) gspExtra = _parsed.gameplaySpeedMultiplierPercent;
                if (!float.IsNaN(_parsed.playerAcceleration)) accExtra = _parsed.playerAcceleration;
                if (!float.IsNaN(_parsed.playerSpeed)) spdExtra = _parsed.playerSpeed;
                if (_parsed.playerSizePercent != int.MinValue) sizeExtra = _parsed.playerSizePercent;
                if (_parsed.magnetPowerPercent != int.MinValue) magExtra = _parsed.magnetPowerPercent;
            }

            FinalComboPowerFactor = (_parsed != null && _parsed.comboPower != int.MinValue)
                ? _parsed.comboPower
                : cpBase;
            FinalCoinMultiplierPercent = cmpBase + cmpExtra;
            FinalMagnetPowerPct = magBase + magExtra;
            FinalGameplaySpeedPct = gspBase + gspExtra;
            FinalPlayerAcceleration = accBase + accExtra;
            FinalPlayerSpeedAdd = spdBase + (spdExtra * 0.1f);
            FinalPlayerSizePct = sizeBase + sizeExtra;
        }

        public void SetExtrasJson(string json)
        {
            _extrasJson = json ?? string.Empty;
            TryParseJson();
            ComputeFinals();
        }

        public void ApplyOnRunStart(IGameplaySpeedService speedService, PlayerMovement movement)
        {
            if (speedService == null || movement == null) return;

            TryParseJson();
            ComputeFinals();

            if (FinalGameplaySpeedPct != 0)
            {
                speedService.SetGameplaySpeedMultiplier(1f + (float)FinalGameplaySpeedPct / 100f);
            }

            if (!Mathf.Approximately(FinalPlayerSpeedAdd, 0f))
            {
                movement.SetBaseSpeed(FinalPlayerSpeedAdd * 1.35f);
                movement.SpeedDisplayDivider = 1.35f;
            }

            if (!Mathf.Approximately(FinalPlayerSizePct, 0f))
            {
                movement.SetPlayerSize(Mathf.RoundToInt(FinalPlayerSizePct));
            }

            if (movement != null)
                movement.SetExternalAccelerationPer60Sec(FinalPlayerAcceleration);
        }

        public void OnRunEnd()
        {
            // Reserved for cleanup
        }
    }
}
