using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Task (görev) yönetimi için service interface.
    /// Daily/weekly task progress ve reward claim işlemlerini yönetir.
    /// ISnapshotable ile full rollback desteği sağlar.
    /// </summary>
    public interface ITaskService : ISnapshotable<TaskStateSnapshot>
    {
        /// <summary>
        /// Aktif (tamamlanmamış) task'ları döndürür.
        /// </summary>
        IReadOnlyList<GameTask> GetActiveTasks();

        /// <summary>
        /// Tamamlanmış task'ları döndürür.
        /// </summary>
        IReadOnlyList<GameTask> GetCompletedTasks();

        /// <summary>
        /// Belirtilen task'ı döndürür.
        /// </summary>
        GameTask GetTask(string taskId);

        /// <summary>
        /// Task progress'ini optimistic olarak artırır.
        /// Batched olarak server'a gönderilir.
        /// </summary>
        void AddProgressOptimistic(string taskId, int amount);

        /// <summary>
        /// Task ödülünü optimistic olarak claim eder.
        /// </summary>
        UniTask<OperationResult> ClaimTaskRewardOptimisticAsync(string taskId);

        /// <summary>
        /// Server ile tam senkronizasyon yapar.
        /// </summary>
        UniTask SyncWithServerAsync();

        /// <summary>
        /// Task progress değiştiğinde tetiklenen event.
        /// </summary>
        event Action<TaskProgressMessage> OnTaskProgressChanged;
    }

    /// <summary>
    /// Game task data
    /// </summary>
    public class GameTask
    {
        public string TaskId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskType Type { get; set; }
        public int CurrentProgress { get; set; }
        public int TargetProgress { get; set; }
        public bool IsCompleted => CurrentProgress >= TargetProgress;
        public bool IsClaimed { get; set; }
        public TaskReward Reward { get; set; }
        public long ExpiresAt { get; set; } // Unix timestamp
    }

    /// <summary>
    /// Task tipleri
    /// </summary>
    public enum TaskType
    {
        Daily,
        Weekly,
        Special,
        Achievement
    }

    /// <summary>
    /// Task ödül bilgisi
    /// </summary>
    public class TaskReward
    {
        public CurrencyType CurrencyType { get; set; }
        public int Amount { get; set; }
        public int ExperiencePoints { get; set; }
    }

    /// <summary>
    /// Task state snapshot (ISnapshotable için - full rollback desteği).
    /// Tüm task state'ini saklar. Deep copy ile immutability garanti edilir.
    /// </summary>
    public readonly struct TaskStateSnapshot
    {
        public IReadOnlyDictionary<string, GameTask> Tasks { get; }

        public TaskStateSnapshot(Dictionary<string, GameTask> tasks)
        {
            // Deep copy - her GameTask klonlanır, referans paylaşılmaz
            var deepCopy = new Dictionary<string, GameTask>(tasks.Count);
            foreach (var kvp in tasks)
            {
                deepCopy[kvp.Key] = CloneTask(kvp.Value);
            }
            Tasks = deepCopy;
        }

        private static GameTask CloneTask(GameTask task)
        {
            if (task == null) return null;

            return new GameTask
            {
                TaskId = task.TaskId,
                Name = task.Name,
                Description = task.Description,
                Type = task.Type,
                CurrentProgress = task.CurrentProgress,
                TargetProgress = task.TargetProgress,
                IsClaimed = task.IsClaimed,
                Reward = task.Reward != null ? new TaskReward
                {
                    CurrencyType = task.Reward.CurrencyType,
                    Amount = task.Reward.Amount,
                    ExperiencePoints = task.Reward.ExperiencePoints
                } : null,
                ExpiresAt = task.ExpiresAt
            };
        }
    }
}
