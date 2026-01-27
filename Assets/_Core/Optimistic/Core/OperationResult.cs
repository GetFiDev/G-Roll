using System;

namespace GRoll.Core.Optimistic
{
    /// <summary>
    /// Operation sonuç durumları
    /// </summary>
    public enum OperationStatus
    {
        /// <summary>Operation başarılı</summary>
        Success,

        /// <summary>Server reddetti, rollback yapıldı</summary>
        RolledBack,

        /// <summary>Network hatası</summary>
        NetworkError,

        /// <summary>Validation hatası (client-side)</summary>
        ValidationError,

        /// <summary>Operation iptal edildi</summary>
        Cancelled
    }

    /// <summary>
    /// Optimistic operation sonucu.
    /// Success, failure ve rollback durumlarını temsil eder.
    /// </summary>
    public class OperationResult
    {
        /// <summary>Operation durumu</summary>
        public OperationStatus Status { get; }

        /// <summary>Hata veya bilgi mesajı</summary>
        public string Message { get; }

        /// <summary>Exception (varsa)</summary>
        public Exception Exception { get; }

        /// <summary>Retry yapılabilir mi?</summary>
        public bool CanRetry { get; }

        /// <summary>Bu bir optimistic operation mıydı?</summary>
        public bool WasOptimistic { get; }

        protected OperationResult(
            OperationStatus status,
            string message,
            Exception ex,
            bool canRetry,
            bool wasOptimistic)
        {
            Status = status;
            Message = message;
            Exception = ex;
            CanRetry = canRetry;
            WasOptimistic = wasOptimistic;
        }

        /// <summary>Operation başarılı mı?</summary>
        public bool IsSuccess => Status == OperationStatus.Success;

        /// <summary>Operation başarısız mı?</summary>
        public bool IsFailure => Status != OperationStatus.Success;

        /// <summary>Rollback yapıldı mı?</summary>
        public bool WasRolledBack => Status == OperationStatus.RolledBack;

        // Factory methods

        /// <summary>Başarılı sonuç oluşturur</summary>
        public static OperationResult Success(string message = null)
            => new(OperationStatus.Success, message, null, false, true);

        /// <summary>Rollback yapılmış sonuç oluşturur</summary>
        public static OperationResult RolledBack(string reason)
            => new(OperationStatus.RolledBack, reason, null, false, true);

        /// <summary>Network hatası sonucu oluşturur</summary>
        public static OperationResult NetworkError(Exception ex, bool canRetry = true)
            => new(OperationStatus.NetworkError, ex.Message, ex, canRetry, true);

        /// <summary>Validation hatası sonucu oluşturur</summary>
        public static OperationResult ValidationError(string message)
            => new(OperationStatus.ValidationError, message, null, false, false);

        /// <summary>İptal edilmiş sonuç oluşturur</summary>
        public static OperationResult Cancelled()
            => new(OperationStatus.Cancelled, "Operation cancelled", null, false, false);
    }

    /// <summary>
    /// Generic operation sonucu - data içerir.
    /// </summary>
    /// <typeparam name="T">Sonuç data tipi</typeparam>
    public class OperationResult<T> : OperationResult
    {
        /// <summary>Sonuç datası (başarılı ise)</summary>
        public T Data { get; }

        private OperationResult(
            OperationStatus status,
            T data,
            string message,
            Exception ex,
            bool canRetry,
            bool wasOptimistic)
            : base(status, message, ex, canRetry, wasOptimistic)
        {
            Data = data;
        }

        // Factory methods

        /// <summary>Başarılı sonuç oluşturur</summary>
        public static OperationResult<T> Success(T data, string message = null)
            => new(OperationStatus.Success, data, message, null, false, true);

        /// <summary>Rollback yapılmış sonuç oluşturur</summary>
        public new static OperationResult<T> RolledBack(string reason)
            => new(OperationStatus.RolledBack, default, reason, null, false, true);

        /// <summary>Network hatası sonucu oluşturur</summary>
        public new static OperationResult<T> NetworkError(Exception ex, bool canRetry = true)
            => new(OperationStatus.NetworkError, default, ex.Message, ex, canRetry, true);

        /// <summary>Validation hatası sonucu oluşturur</summary>
        public new static OperationResult<T> ValidationError(string message)
            => new(OperationStatus.ValidationError, default, message, null, false, false);

        /// <summary>İptal edilmiş sonuç oluşturur</summary>
        public new static OperationResult<T> Cancelled()
            => new(OperationStatus.Cancelled, default, "Operation cancelled", null, false, false);
    }
}
