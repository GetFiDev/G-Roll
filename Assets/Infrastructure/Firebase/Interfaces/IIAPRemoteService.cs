using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// IAP (In-App Purchase) remote service interface.
    /// Satin alma dogrulama ve odul verme islemleri.
    /// </summary>
    public interface IIAPRemoteService
    {
        /// <summary>
        /// Satin alma makbuzunu sunucuda dogrula ve odulleri ver.
        /// </summary>
        /// <param name="productId">Satin alinan urun ID'si</param>
        /// <param name="receipt">Unity IAP'den gelen makbuz string'i</param>
        /// <returns>Dogrulama sonucu ve oduller</returns>
        UniTask<IAPVerifyResponse> VerifyPurchaseAsync(string productId, string receipt);
    }

    /// <summary>
    /// IAP dogrulama sonucu
    /// </summary>
    public struct IAPVerifyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Rewards { get; set; }
        public string ErrorMessage { get; set; }

        public static IAPVerifyResponse Succeeded(string message, Dictionary<string, object> rewards) => new()
        {
            Success = true,
            Message = message,
            Rewards = rewards
        };

        public static IAPVerifyResponse Failed(string error) => new()
        {
            Success = false,
            ErrorMessage = error
        };
    }
}
