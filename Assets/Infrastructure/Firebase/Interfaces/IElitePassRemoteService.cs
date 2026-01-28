using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Elite Pass remote service interface.
    /// Firebase Functions ile Elite Pass durumu kontrolu ve satin alma.
    /// </summary>
    public interface IElitePassRemoteService
    {
        /// <summary>
        /// Elite Pass satin alma.
        /// </summary>
        /// <param name="purchaseId">Idempotent islem icin unique ID (opsiyonel)</param>
        /// <returns>Aktif durumu ve bitis tarihi</returns>
        UniTask<ElitePassResponse> PurchaseAsync(string purchaseId = null);

        /// <summary>
        /// Elite Pass durumunu kontrol et.
        /// </summary>
        /// <returns>Aktif durumu ve bitis tarihi</returns>
        UniTask<ElitePassResponse> CheckAsync();
    }

    /// <summary>
    /// Elite Pass islem sonucu
    /// </summary>
    public struct ElitePassResponse
    {
        public bool Success { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string ErrorMessage { get; set; }

        public static ElitePassResponse Succeeded(bool isActive, DateTime? expiresAt) => new()
        {
            Success = true,
            IsActive = isActive,
            ExpiresAtUtc = expiresAt
        };

        public static ElitePassResponse Failed(string error) => new()
        {
            Success = false,
            ErrorMessage = error
        };
    }
}
