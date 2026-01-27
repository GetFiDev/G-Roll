namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Optimistic bir operation rollback yapıldığında yayınlanan message.
    /// UI rollback animasyonları ve kullanıcı bildirimleri için kullanılır.
    /// </summary>
    public readonly struct OperationRolledBackMessage : IMessage
    {
        /// <summary>
        /// Rollback yapılan operation tipi (örn: "EquipItem", "ClaimAchievement")
        /// </summary>
        public string OperationType { get; }

        /// <summary>
        /// Rollback sebebi
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Kullanıcıya bildirim gösterilmeli mi?
        /// </summary>
        public bool ShouldNotifyUser { get; }

        /// <summary>
        /// Hata kategorisi
        /// </summary>
        public RollbackCategory Category { get; }

        public OperationRolledBackMessage(
            string operationType,
            string reason,
            bool shouldNotify,
            RollbackCategory category = RollbackCategory.BusinessRule)
        {
            OperationType = operationType;
            Reason = reason;
            ShouldNotifyUser = shouldNotify;
            Category = category;
        }
    }

    /// <summary>
    /// Rollback kategorileri - farklı UI feedback'leri için
    /// </summary>
    public enum RollbackCategory
    {
        /// <summary>Geçici network hatası - retry ile çözülebilir</summary>
        Transient,

        /// <summary>İş kuralı hatası - örn: yetersiz bakiye</summary>
        BusinessRule,

        /// <summary>State uyuşmazlığı - full resync gerekli</summary>
        StateConflict,

        /// <summary>Kritik hata - feature disable edilmeli</summary>
        Critical
    }
}
