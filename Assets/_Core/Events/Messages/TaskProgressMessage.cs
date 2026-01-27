namespace GRoll.Core.Events.Messages
{
    /// <summary>
    /// Task progress değiştiğinde yayınlanan message.
    /// Daily/weekly task ilerlemesi için kullanılır.
    /// </summary>
    public readonly struct TaskProgressMessage : IMessage, IOptimisticMessage
    {
        /// <summary>
        /// Task ID'si
        /// </summary>
        public string TaskId { get; }

        /// <summary>
        /// Mevcut progress değeri
        /// </summary>
        public int CurrentProgress { get; }

        /// <summary>
        /// Hedef progress değeri
        /// </summary>
        public int TargetProgress { get; }

        /// <summary>
        /// Task tamamlandı mı?
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// True ise optimistic update, false ise server confirmed
        /// </summary>
        public bool IsOptimistic { get; }

        /// <summary>
        /// Ödül claim edildi mi?
        /// </summary>
        public bool IsClaimed { get; }

        public TaskProgressMessage(
            string taskId,
            int current,
            int target,
            bool completed,
            bool isOptimistic,
            bool isClaimed = false)
        {
            TaskId = taskId;
            CurrentProgress = current;
            TargetProgress = target;
            IsCompleted = completed;
            IsOptimistic = isOptimistic;
            IsClaimed = isClaimed;
        }
    }
}
