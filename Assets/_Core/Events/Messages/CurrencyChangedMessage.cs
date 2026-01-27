using GRoll.Core;

namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Currency miktarı değiştiğinde yayınlanan message.
    /// Optimistic update ve rollback durumlarında kullanılır.
    /// </summary>
    public readonly struct CurrencyChangedMessage : IMessage, IOptimisticMessage
    {
        /// <summary>
        /// Currency tipi (SoftCurrency, HardCurrency)
        /// </summary>
        public CurrencyType Type { get; }

        /// <summary>
        /// Değişiklik öncesi miktar
        /// </summary>
        public int PreviousAmount { get; }

        /// <summary>
        /// Yeni miktar
        /// </summary>
        public int NewAmount { get; }

        /// <summary>
        /// Değişim miktarı (pozitif = kazanç, negatif = harcama)
        /// </summary>
        public int Delta => NewAmount - PreviousAmount;

        /// <summary>
        /// True ise optimistic update, false ise server confirmed
        /// </summary>
        public bool IsOptimistic { get; }

        /// <summary>
        /// Değişikliğin kaynağı (achievement, purchase, task reward, etc.)
        /// </summary>
        public string Source { get; }

        public CurrencyChangedMessage(
            CurrencyType type,
            int previous,
            int newAmount,
            bool isOptimistic = false,
            string source = null)
        {
            Type = type;
            PreviousAmount = previous;
            NewAmount = newAmount;
            IsOptimistic = isOptimistic;
            Source = source;
        }
    }
}
