using Cysharp.Threading.Tasks;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Inventory işlemleri için Firebase Cloud Functions implementasyonu.
    /// </summary>
    public class InventoryRemoteService : IInventoryRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public InventoryRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<EquipItemResponse> EquipItemAsync(string itemId, string slotId)
        {
            var result = await _firebase.CallFunctionAsync<EquipItemResponse>(
                "equipItem",
                new { itemId, slotId }
            );
            return result;
        }

        public async UniTask<UnequipItemResponse> UnequipItemAsync(string itemId)
        {
            var result = await _firebase.CallFunctionAsync<UnequipItemResponse>(
                "unequipItem",
                new { itemId }
            );
            return result;
        }

        public async UniTask<AcquireItemResponse> AcquireItemAsync(string itemId, string source)
        {
            var result = await _firebase.CallFunctionAsync<AcquireItemResponse>(
                "acquireItem",
                new { itemId, source }
            );
            return result;
        }

        public async UniTask<InventoryStateResponse> FetchInventoryAsync()
        {
            var result = await _firebase.CallFunctionAsync<InventoryStateResponse>(
                "getInventory",
                null
            );
            return result;
        }
    }
}
