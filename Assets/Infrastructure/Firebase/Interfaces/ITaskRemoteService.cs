using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Task işlemleri için remote service interface.
    /// Firebase Cloud Functions çağrılarını soyutlar.
    /// </summary>
    public interface ITaskRemoteService
    {
        /// <summary>
        /// Batch olarak progress güncellemesi gönderir.
        /// </summary>
        UniTask<BatchProgressResponse> BatchUpdateProgressAsync(Dictionary<string, int> progressUpdates);

        /// <summary>
        /// Task reward'ını claim eder.
        /// </summary>
        UniTask<ClaimTaskRewardResponse> ClaimTaskRewardAsync(string taskId);

        /// <summary>
        /// Server'dan tüm task'ları alır.
        /// </summary>
        UniTask<List<GameTask>> FetchTasksAsync();
    }

    /// <summary>
    /// Batch progress response
    /// </summary>
    public struct BatchProgressResponse
    {
        public bool Success { get; set; }
        public List<GameTask> Tasks { get; set; }
        public string ErrorMessage { get; set; }

        public static BatchProgressResponse Successful(List<GameTask> tasks)
        {
            return new BatchProgressResponse { Success = true, Tasks = tasks };
        }

        public static BatchProgressResponse Failed(string error)
        {
            return new BatchProgressResponse { Success = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// Claim task reward response
    /// </summary>
    public struct ClaimTaskRewardResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static ClaimTaskRewardResponse Successful()
        {
            return new ClaimTaskRewardResponse { Success = true };
        }

        public static ClaimTaskRewardResponse Failed(string error)
        {
            return new ClaimTaskRewardResponse { Success = false, ErrorMessage = error };
        }
    }
}
