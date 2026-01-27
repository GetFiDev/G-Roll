using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Currency işlemleri için Firebase Cloud Functions implementasyonu.
    /// </summary>
    public class CurrencyRemoteService : ICurrencyRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public CurrencyRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<CurrencyOperationResponse> AddCurrencyAsync(
            CurrencyType type,
            int amount,
            string source)
        {
            var result = await _firebase.CallFunctionAsync<CurrencyOperationResponse>(
                "addCurrency",
                new { currencyType = (int)type, amount, source }
            );
            return result;
        }

        public async UniTask<CurrencyOperationResponse> SpendCurrencyAsync(
            CurrencyType type,
            int amount,
            string reason)
        {
            var result = await _firebase.CallFunctionAsync<CurrencyOperationResponse>(
                "spendCurrency",
                new { currencyType = (int)type, amount, reason }
            );
            return result;
        }

        public async UniTask<CurrencyBalancesResponse> FetchBalancesAsync()
        {
            var result = await _firebase.CallFunctionAsync<CurrencyBalancesResponse>(
                "getCurrencyBalances",
                null
            );
            return result;
        }
    }
}
