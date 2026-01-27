using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Currency işlemleri için remote service interface.
    /// Firebase Cloud Functions çağrılarını soyutlar.
    /// </summary>
    public interface ICurrencyRemoteService
    {
        /// <summary>
        /// Currency ekler.
        /// </summary>
        UniTask<CurrencyOperationResponse> AddCurrencyAsync(CurrencyType type, int amount, string source);

        /// <summary>
        /// Currency harcar.
        /// </summary>
        UniTask<CurrencyOperationResponse> SpendCurrencyAsync(CurrencyType type, int amount, string reason);

        /// <summary>
        /// Server'dan tüm bakiyeleri alır.
        /// </summary>
        UniTask<CurrencyBalancesResponse> FetchBalancesAsync();
    }

    /// <summary>
    /// Currency operation response
    /// </summary>
    public struct CurrencyOperationResponse
    {
        public bool Success { get; set; }
        public int NewBalance { get; set; }
        public string ErrorMessage { get; set; }

        public static CurrencyOperationResponse Successful(int newBalance)
        {
            return new CurrencyOperationResponse
            {
                Success = true,
                NewBalance = newBalance
            };
        }

        public static CurrencyOperationResponse Failed(string error)
        {
            return new CurrencyOperationResponse
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Currency balances response
    /// </summary>
    public struct CurrencyBalancesResponse
    {
        public Dictionary<CurrencyType, int> Balances { get; set; }
    }
}
