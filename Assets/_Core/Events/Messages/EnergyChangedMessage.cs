using System;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Energy değiştiğinde yayınlanan message.
    /// Energy consumption ve regeneration için kullanılır.
    /// </summary>
    public readonly struct EnergyChangedMessage : IMessage, IOptimisticMessage
    {
        /// <summary>
        /// Mevcut energy miktarı
        /// </summary>
        public int CurrentEnergy { get; }

        /// <summary>
        /// Maksimum energy miktarı
        /// </summary>
        public int MaxEnergy { get; }

        /// <summary>
        /// Önceki energy miktarı
        /// </summary>
        public int PreviousEnergy { get; }

        /// <summary>
        /// Değişim miktarı
        /// </summary>
        public int Delta => CurrentEnergy - PreviousEnergy;

        /// <summary>
        /// Sonraki energy regeneration zamanı
        /// </summary>
        public DateTime? NextRegenTime { get; }

        /// <summary>
        /// True ise optimistic update, false ise server confirmed
        /// </summary>
        public bool IsOptimistic { get; }

        public EnergyChangedMessage(
            int current,
            int max,
            int previous,
            DateTime? nextRegenTime,
            bool isOptimistic)
        {
            CurrentEnergy = current;
            MaxEnergy = max;
            PreviousEnergy = previous;
            NextRegenTime = nextRegenTime;
            IsOptimistic = isOptimistic;
        }
    }
}
