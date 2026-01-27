namespace GRoll.Core.Optimistic.Strategies
{
    /// <summary>
    /// Rollback stratejileri interface'i.
    /// Farklı rollback davranışları için kullanılır.
    /// </summary>
    public interface IRollbackStrategy
    {
        /// <summary>
        /// Soft rollback: Kullanıcıya bilgi vermeden sessizce geri al.
        /// Örnek: Coin sayısı düzeltme (küçük farklar), minor sync issues
        /// </summary>
        void SoftRollback();

        /// <summary>
        /// Hard rollback: Kullanıcıya bilgi vererek geri al.
        /// Örnek: Satın alma iptal, achievement geri alma
        /// </summary>
        /// <param name="reason">Kullanıcıya gösterilecek sebep</param>
        void HardRollback(string reason);

        /// <summary>
        /// Deferred rollback: Daha sonra sync edilecek şekilde işaretle.
        /// Örnek: Offline modda yapılan işlemler
        /// </summary>
        /// <param name="operation">Bekleyen operation bilgisi</param>
        void DeferRollback(PendingOperation operation);
    }

    /// <summary>
    /// Deferred rollback için bekleyen operation bilgisi
    /// </summary>
    public class PendingOperation
    {
        /// <summary>Operation tipi adı</summary>
        public string OperationType { get; set; }

        /// <summary>Operation parametreleri (JSON serialized)</summary>
        public string Parameters { get; set; }

        /// <summary>Operation timestamp</summary>
        public long Timestamp { get; set; }

        /// <summary>Retry sayısı</summary>
        public int RetryCount { get; set; }

        /// <summary>Maksimum retry sayısı</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Retry yapılabilir mi?</summary>
        public bool CanRetry => RetryCount < MaxRetries;
    }
}
