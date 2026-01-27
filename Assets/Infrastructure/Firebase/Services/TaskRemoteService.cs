using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Task işlemleri için Firebase Cloud Functions implementasyonu.
    /// </summary>
    public class TaskRemoteService : ITaskRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public TaskRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<BatchProgressResponse> BatchUpdateProgressAsync(Dictionary<string, int> progressUpdates)
        {
            var result = await _firebase.CallFunctionAsync<BatchProgressResponse>(
                "batchUpdateTaskProgress",
                new { progressUpdates }
            );
            return result;
        }

        public async UniTask<ClaimTaskRewardResponse> ClaimTaskRewardAsync(string taskId)
        {
            var result = await _firebase.CallFunctionAsync<ClaimTaskRewardResponse>(
                "claimTaskReward",
                new { taskId }
            );
            return result;
        }

        public async UniTask<List<GameTask>> FetchTasksAsync()
        {
            var result = await _firebase.CallFunctionAsync<List<GameTask>>(
                "getTasks",
                null
            );
            return result;
        }
    }
}
