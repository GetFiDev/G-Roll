using System;

namespace GRoll.Core.Events
{
    /// <summary>
    /// Tüm message tipleri için marker interface.
    /// MessageBus üzerinden publish/subscribe edilebilir.
    /// </summary>
    public interface IMessage { }

    /// <summary>
    /// Timestamp içeren message'lar için.
    /// Server sync ve conflict resolution için kullanılır.
    /// </summary>
    public interface ITimestampedMessage : IMessage
    {
        DateTime Timestamp { get; }
    }

    /// <summary>
    /// Optimistic operation sonucu olan message'lar için.
    /// UI bu flag'e göre animasyon/feedback belirleyebilir.
    /// </summary>
    public interface IOptimisticMessage : IMessage
    {
        /// <summary>
        /// True ise bu bir optimistic update'tir, henüz server onaylamamış.
        /// False ise server tarafından onaylanmış (confirmed) veya rollback edilmiş.
        /// </summary>
        bool IsOptimistic { get; }
    }
}
