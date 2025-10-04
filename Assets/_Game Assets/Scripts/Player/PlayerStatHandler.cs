using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStatHandler : MonoBehaviour
{
    [Header("Base Stats (Inspector defaults)")]
    [SerializeField] private int   baseComboPower = 1;                 // integer, default 1
    [SerializeField] private int   baseCoinMultiplierPercent = 0;      // integer percent, default 0
    [SerializeField] private int   baseMagnetPowerPercent = 0;         // integer percent, default 0
    [SerializeField] private int   baseGameplaySpeedBonusPercent = 0;  // integer percent, default 0
    [SerializeField] private float basePlayerAccelerationPer60 = 0.1f; // float, default 0.1 per minute
    [SerializeField] private float basePlayerSpeedAdd = 0f;            // float additive speed
    [SerializeField] private int   basePlayerSizePercent = 0;          // integer percent, default 0

    private string _extrasJson = string.Empty;

    public int   FinalComboPowerFactor      { get; private set; } // integer factor, base 1
    public int   FinalCoinMultiplierPercent { get; private set; } // integer percent
    public int   FinalMagnetPowerPct        { get; private set; } // integer percent
    public int   FinalGameplaySpeedPct      { get; private set; } // integer percent
    public float FinalPlayerAcceleration    { get; private set; } // float per minute
    public float FinalPlayerSpeedAdd        { get; private set; } // float additive
    public int   FinalPlayerSizePct         { get; private set; } // integer percent

    [Serializable]
    private class StatExtrasDTO
    {
        public int   comboPower = int.MinValue;                     // integer additive over base 1 (e.g., 20 -> final 21)
        public int   coinMultiplierPercent = int.MinValue;          // integer percent
        public int   gameplaySpeedMultiplierPercent = int.MinValue; // integer percent
        public float playerAcceleration = float.NaN;                // float per minute
        public float playerSpeed = float.NaN;                       // float additive
        public int   playerSize = int.MinValue;                     // legacy
        public int   playerSizePercent = int.MinValue;              // integer percent
        public int   magnetPowerPercent = int.MinValue;             // integer percent
    }

    private StatExtrasDTO _parsed;

    private void Awake()
    {
        TryParseJson();
        ComputeFinals();
    }

    // OnValidate removed; JSON no longer editable in inspector

    private void TryParseJson()
    {
        _parsed = null;
        var txt = (_extrasJson ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(txt)) return;
        try { _parsed = JsonUtility.FromJson<StatExtrasDTO>(txt); } catch { }
    }

    private void ComputeFinals()
    {
        int   cpBase   = baseComboPower;
        int   cmpBase  = baseCoinMultiplierPercent;
        int   magBase  = baseMagnetPowerPercent;
        int   gspBase  = baseGameplaySpeedBonusPercent;
        float accBase  = basePlayerAccelerationPer60;
        float spdBase  = basePlayerSpeedAdd;
        int   sizeBase = basePlayerSizePercent;

        int   cpExtra   = 0;
        int   cmpExtra  = 0;
        int   magExtra  = 0;
        int   gspExtra  = 0;
        float accExtra  = 0f;
        float spdExtra  = 0f;
        int   sizeExtra = 0;

        if (_parsed != null)
        {
            if (_parsed.comboPower != int.MinValue)                       cpExtra   = _parsed.comboPower;
            if (_parsed.coinMultiplierPercent != int.MinValue)            cmpExtra  = _parsed.coinMultiplierPercent;
            if (_parsed.gameplaySpeedMultiplierPercent != int.MinValue)   gspExtra  = _parsed.gameplaySpeedMultiplierPercent;
            if (!float.IsNaN(_parsed.playerAcceleration))                 accExtra  = _parsed.playerAcceleration;
            if (!float.IsNaN(_parsed.playerSpeed))                        spdExtra  = _parsed.playerSpeed;
            if (_parsed.playerSizePercent != int.MinValue)                sizeExtra = _parsed.playerSizePercent;
            if (_parsed.magnetPowerPercent != int.MinValue)               magExtra  = _parsed.magnetPowerPercent;
        }

        FinalComboPowerFactor      = cpBase + cpExtra;     // integer result
        FinalCoinMultiplierPercent = cmpBase + cmpExtra;   // integer result
        FinalMagnetPowerPct        = magBase + magExtra;   // integer result
        FinalGameplaySpeedPct      = gspBase + gspExtra;   // integer result
        FinalPlayerAcceleration    = accBase + accExtra;   // float result
        FinalPlayerSpeedAdd        = spdBase + spdExtra;   // float result
        FinalPlayerSizePct         = sizeBase + sizeExtra; // integer result
    }

    public void SetExtrasJson(string json)
    {
        _extrasJson = json ?? string.Empty;
        TryParseJson();
        ComputeFinals();
    }

    public void ApplyOnRunStart(GameplayLogicApplier logic, PlayerMovement movement)
    {
        if (logic == null || movement == null) return;

        TryParseJson();
        ComputeFinals();

        if (FinalGameplaySpeedPct != 0)
        {
            logic.ApplyGameplaySpeedPercent((float)FinalGameplaySpeedPct / 100f);
        }

        // PlayerStatHandler.cs - ApplyOnRunStart
        if (!Mathf.Approximately(FinalPlayerSpeedAdd, 0f))
        {
            movement.AddPlayerSpeed(FinalPlayerSpeedAdd);
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
    }
}