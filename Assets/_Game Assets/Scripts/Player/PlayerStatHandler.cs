using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStatHandler : MonoBehaviour
{
    [Header("Base Stats (Inspector defaults)")]
    [SerializeField] private float baseCoinMultiplier = 1f;
    [SerializeField] private int   baseCoinBonusPercent = 0;
    [SerializeField] private int   baseGameplaySpeedBonusPercent = 0;
    [SerializeField] private float basePlayerAccelerationPer60 = 0f;
    [SerializeField] private float basePlayerSpeedAdd = 0f;
    [SerializeField] private int   basePlayerSizePercent = 0;
    [SerializeField] private int   baseCollectibleBonusPercent = 0;

    private string _extrasJson = string.Empty;

    public float FinalCoinFactor         { get; private set; }
    public float FinalGameplaySpeedPct   { get; private set; }
    public float FinalPlayerAcceleration { get; private set; }
    public float FinalPlayerSpeedAdd     { get; private set; }
    public float FinalPlayerSizePct      { get; private set; }
    public float FinalCollectiblePct     { get; private set; }

    [Serializable]
    private class StatExtrasDTO
    {
        public float coinMultiplier = float.NaN;
        public int   coinMultiplierPercent = int.MinValue;
        public int   gameplaySpeedMultiplierPercent = int.MinValue;
        public float playerAcceleration = float.NaN;
        public float playerSpeed = float.NaN;
        public int   playerSize = int.MinValue;
        public int   playerSizePercent = int.MinValue;
        public int   collectibleMultiplierPercent = int.MinValue;
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
        float cmBase   = baseCoinMultiplier;
        int   cmpBase  = baseCoinBonusPercent;
        int   gspBase  = baseGameplaySpeedBonusPercent;
        float accBase  = basePlayerAccelerationPer60;
        float spdBase  = basePlayerSpeedAdd;
        int   sizeBase = basePlayerSizePercent;
        int   colBase  = baseCollectibleBonusPercent;

        float cmExtra   = 0f;
        int   cmpExtra  = 0;
        int   gspExtra  = 0;
        float accExtra  = 0f;
        float spdExtra  = 0f;
        int   sizeExtra = 0;
        int   colExtra  = 0;

        if (_parsed != null)
        {
            if (!float.IsNaN(_parsed.coinMultiplier))                 cmExtra   = _parsed.coinMultiplier - 1f;
            if (_parsed.coinMultiplierPercent != int.MinValue)        cmpExtra  = _parsed.coinMultiplierPercent;
            if (_parsed.gameplaySpeedMultiplierPercent != int.MinValue) gspExtra = _parsed.gameplaySpeedMultiplierPercent;
            if (!float.IsNaN(_parsed.playerAcceleration))             accExtra  = _parsed.playerAcceleration;
            if (!float.IsNaN(_parsed.playerSpeed))                    spdExtra  = _parsed.playerSpeed;
            if (_parsed.playerSizePercent != int.MinValue)            sizeExtra = _parsed.playerSizePercent;
            else if (_parsed.playerSize != int.MinValue)              sizeExtra = _parsed.playerSize;
            if (_parsed.collectibleMultiplierPercent != int.MinValue) colExtra  = _parsed.collectibleMultiplierPercent;
        }

        FinalCoinFactor         = cmBase + cmExtra + ((cmpBase + cmpExtra) / 100f);
        FinalGameplaySpeedPct   = gspBase + gspExtra;
        FinalPlayerAcceleration = accBase + accExtra;
        FinalPlayerSpeedAdd     = spdBase + spdExtra;
        FinalPlayerSizePct      = sizeBase + sizeExtra;
        FinalCollectiblePct     = colBase + colExtra;
    }

    public void SetExtrasJson(string json)
    {
        _extrasJson = json ?? string.Empty;
        TryParseJson();
        ComputeFinals();
    }

    public float ApplyCoinMultiplier(float baseReward)
        => baseReward * FinalCoinFactor;

    public float BoostCollectibleDelta(float baseDelta)
        => baseDelta * (1f + (FinalCollectiblePct / 100f));

    public void ApplyOnRunStart(GameplayLogicApplier logic, PlayerMovement movement)
    {
        if (logic == null || movement == null) return;

        TryParseJson();
        ComputeFinals();

        if (!Mathf.Approximately(FinalGameplaySpeedPct, 0f))
        {
            logic.ApplyGameplaySpeedPercent(FinalGameplaySpeedPct / 100f);
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