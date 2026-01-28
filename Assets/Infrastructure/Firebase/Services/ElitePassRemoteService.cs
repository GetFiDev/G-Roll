using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Elite Pass remote service implementation.
    /// Firebase Functions ile Elite Pass islemleri.
    /// </summary>
    public class ElitePassRemoteService : IElitePassRemoteService
    {
        private readonly IGRollLogger _logger;
        private FirebaseFunctions _functions;

        [Inject]
        public ElitePassRemoteService(IGRollLogger logger)
        {
            _logger = logger;
        }

        private FirebaseFunctions Functions => _functions ??= FirebaseFunctions.DefaultInstance;

        public async UniTask<ElitePassResponse> PurchaseAsync(string purchaseId = null)
        {
            try
            {
                var callable = Functions.GetHttpsCallable("purchaseElitePass");
                var data = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(purchaseId))
                    data["purchaseId"] = purchaseId;

                var result = await callable.CallAsync(data);
                return ParseResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ElitePassRemoteService] Purchase error: {ex.Message}");
                return ElitePassResponse.Failed(ex.Message);
            }
        }

        public async UniTask<ElitePassResponse> CheckAsync()
        {
            try
            {
                var callable = Functions.GetHttpsCallable("checkElitePass");
                var result = await callable.CallAsync(new Dictionary<string, object>());
                return ParseResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ElitePassRemoteService] Check error: {ex.Message}");
                return ElitePassResponse.Failed(ex.Message);
            }
        }

        private ElitePassResponse ParseResponse(HttpsCallableResult result)
        {
            var dict = result.Data as IDictionary;

            bool active = dict != null && dict["active"] is bool b && b;

            DateTime? expires = null;
            if (dict != null && dict["expiresAt"] != null)
            {
                if (DateTime.TryParse(
                    dict["expiresAt"].ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var dt))
                {
                    expires = dt.ToUniversalTime();
                }
            }

            return ElitePassResponse.Succeeded(active, expires);
        }
    }
}
