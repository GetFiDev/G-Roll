using UnityEngine;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Gameplay sirasinda currency toplandigi zaman yayinlanan message.
    /// UI animasyonlari ve FX sistemleri icin kullanilir.
    /// </summary>
    public readonly struct CurrencyCollectedMessage : IMessage
    {
        /// <summary>
        /// Toplanan currency tipi
        /// </summary>
        public CurrencyType Type { get; }

        /// <summary>
        /// Toplanan miktar
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Toplamanin gerceklestigi world pozisyonu (FX icin)
        /// </summary>
        public Vector3 WorldPosition { get; }

        public CurrencyCollectedMessage(CurrencyType type, int amount, Vector3 worldPosition)
        {
            Type = type;
            Amount = amount;
            WorldPosition = worldPosition;
        }
    }
}
