namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Achievement durumu değiştiğinde yayınlanan message.
    /// Progress update, unlock, claim işlemlerinde kullanılır.
    /// </summary>
    public readonly struct AchievementChangedMessage : IMessage, IOptimisticMessage
    {
        /// <summary>
        /// Achievement ID'si
        /// </summary>
        public string AchievementId { get; }

        /// <summary>
        /// Değişiklik tipi
        /// </summary>
        public AchievementChangeType ChangeType { get; }

        /// <summary>
        /// True ise optimistic update, false ise server confirmed
        /// </summary>
        public bool IsOptimistic { get; }

        /// <summary>
        /// Progress değişikliği için mevcut progress değeri
        /// </summary>
        public int CurrentProgress { get; }

        /// <summary>
        /// Progress değişikliği için hedef progress değeri
        /// </summary>
        public int TargetProgress { get; }

        public AchievementChangedMessage(
            string id,
            AchievementChangeType type,
            bool isOptimistic,
            int currentProgress = 0,
            int targetProgress = 0)
        {
            AchievementId = id;
            ChangeType = type;
            IsOptimistic = isOptimistic;
            CurrentProgress = currentProgress;
            TargetProgress = targetProgress;
        }
    }

    /// <summary>
    /// Achievement değişiklik tipleri
    /// </summary>
    public enum AchievementChangeType
    {
        /// <summary>Progress güncellendi</summary>
        ProgressUpdated,

        /// <summary>Achievement unlock oldu (tamamlandı)</summary>
        Unlocked,

        /// <summary>Achievement ödülü claim edildi</summary>
        Claimed,

        /// <summary>Rollback yapıldı</summary>
        RolledBack
    }
}
