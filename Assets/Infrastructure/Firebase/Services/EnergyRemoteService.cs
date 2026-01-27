using Cysharp.Threading.Tasks;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Energy işlemleri için Firebase Cloud Functions implementasyonu.
    /// </summary>
    public class EnergyRemoteService : IEnergyRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public EnergyRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<EnergyOperationResponse> ConsumeEnergyAsync(int amount)
        {
            var result = await _firebase.CallFunctionAsync<EnergyOperationResponse>(
                "consumeEnergy",
                new { amount }
            );
            return result;
        }

        public async UniTask<EnergyOperationResponse> RefillEnergyAsync()
        {
            var result = await _firebase.CallFunctionAsync<EnergyOperationResponse>(
                "refillEnergy",
                null
            );
            return result;
        }

        public async UniTask<EnergyStateResponse> FetchEnergyStateAsync()
        {
            var result = await _firebase.CallFunctionAsync<EnergyStateResponse>(
                "getEnergyState",
                null
            );
            return result;
        }
    }
}
