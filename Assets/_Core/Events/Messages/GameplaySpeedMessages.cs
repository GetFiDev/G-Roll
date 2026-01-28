using UnityEngine;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Speed multiplier tipi
    /// </summary>
    public enum SpeedMultiplierType
    {
        Player,
        Gameplay,
        Total
    }

    /// <summary>
    /// Hiz carpani degistiginde yayinlanan message
    /// </summary>
    public readonly struct SpeedMultiplierChangedMessage : IMessage
    {
        public SpeedMultiplierType Type { get; }
        public float PreviousMultiplier { get; }
        public float NewMultiplier { get; }

        public SpeedMultiplierChangedMessage(SpeedMultiplierType type, float previous, float newMultiplier)
        {
            Type = type;
            PreviousMultiplier = previous;
            NewMultiplier = newMultiplier;
        }
    }

    /// <summary>
    /// Booster aktif edildiginde yayinlanan message
    /// </summary>
    public readonly struct BoosterActivatedMessage : IMessage
    {
        public float Duration { get; }
        public float SpeedMultiplier { get; }

        public BoosterActivatedMessage(float duration, float speedMultiplier)
        {
            Duration = duration;
            SpeedMultiplier = speedMultiplier;
        }
    }

    /// <summary>
    /// Booster deaktif edildiginde yayinlanan message
    /// </summary>
    public readonly struct BoosterDeactivatedMessage : IMessage { }

    /// <summary>
    /// Booster fill degistiginde yayinlanan message
    /// </summary>
    public readonly struct BoosterFillChangedMessage : IMessage
    {
        public float PreviousFill { get; }
        public float NewFill { get; }
        public float MinFill { get; }
        public float MaxFill { get; }

        public BoosterFillChangedMessage(float previousFill, float newFill, float minFill = 0f, float maxFill = 1f)
        {
            PreviousFill = previousFill;
            NewFill = newFill;
            MinFill = minFill;
            MaxFill = maxFill;
        }
    }

    /// <summary>
    /// Combo gucu degistiginde yayinlanan message
    /// </summary>
    public readonly struct ComboChangedMessage : IMessage
    {
        public int PreviousPower { get; }
        public int NewPower { get; }
        public float Multiplier { get; }

        public ComboChangedMessage(int previousPower, int newPower, float multiplier = 1f)
        {
            PreviousPower = previousPower;
            NewPower = newPower;
            Multiplier = multiplier;
        }
    }

    /// <summary>
    /// Combo sifirlandiginda yayinlanan message
    /// </summary>
    public readonly struct ComboResetMessage : IMessage
    {
        public int FinalPower { get; }

        public ComboResetMessage(int finalPower)
        {
            FinalPower = finalPower;
        }
    }

    /// <summary>
    /// Coin FX talebi icin message (visual effect sistemi icin)
    /// </summary>
    public readonly struct CoinPickupFXRequestMessage : IMessage
    {
        public Vector3 WorldPosition { get; }
        public int Count { get; }

        public CoinPickupFXRequestMessage(Vector3 worldPosition, int count)
        {
            WorldPosition = worldPosition;
            Count = count;
        }
    }
}
