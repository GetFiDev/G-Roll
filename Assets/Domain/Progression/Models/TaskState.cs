using System;
using System.Collections.Generic;
using System.Linq;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Domain.Progression.Models
{
    /// <summary>
    /// Task state'ini tutan internal class.
    /// TaskService tarafından yönetilir.
    /// </summary>
    public class TaskState
    {
        private readonly Dictionary<string, GameTask> _tasks = new();
        private readonly object _lock = new();

        /// <summary>
        /// Belirtilen task'ı döndürür.
        /// </summary>
        public GameTask GetTask(string taskId)
        {
            lock (_lock)
            {
                return _tasks.TryGetValue(taskId, out var task) ? task : null;
            }
        }

        /// <summary>
        /// Aktif (tamamlanmamış) task'ları döndürür.
        /// </summary>
        public IReadOnlyList<GameTask> GetActiveTasks()
        {
            lock (_lock)
            {
                return _tasks.Values.Where(t => !t.IsCompleted).ToList();
            }
        }

        /// <summary>
        /// Tamamlanmış ama claim edilmemiş task'ları döndürür.
        /// </summary>
        public IReadOnlyList<GameTask> GetCompletedTasks()
        {
            lock (_lock)
            {
                return _tasks.Values.Where(t => t.IsCompleted && !t.IsClaimed).ToList();
            }
        }

        /// <summary>
        /// Tüm task'ları döndürür.
        /// </summary>
        public IReadOnlyList<GameTask> GetAllTasks()
        {
            lock (_lock)
            {
                return _tasks.Values.ToList();
            }
        }

        /// <summary>
        /// Task'ları ayarlar.
        /// </summary>
        public void SetTasks(List<GameTask> tasks)
        {
            lock (_lock)
            {
                _tasks.Clear();
                foreach (var task in tasks)
                {
                    _tasks[task.TaskId] = task;
                }
            }
        }

        /// <summary>
        /// Task progress'ini günceller.
        /// </summary>
        public void UpdateProgress(string taskId, int progress)
        {
            lock (_lock)
            {
                if (_tasks.TryGetValue(taskId, out var task))
                {
                    task.CurrentProgress = Math.Min(progress, task.TargetProgress);
                }
            }
        }

        /// <summary>
        /// Task progress'ine miktar ekler.
        /// </summary>
        public void AddProgress(string taskId, int amount)
        {
            lock (_lock)
            {
                if (_tasks.TryGetValue(taskId, out var task))
                {
                    task.CurrentProgress = Math.Min(task.CurrentProgress + amount, task.TargetProgress);
                }
            }
        }

        /// <summary>
        /// Task'ı claimed olarak işaretler.
        /// </summary>
        public void MarkClaimed(string taskId)
        {
            lock (_lock)
            {
                if (_tasks.TryGetValue(taskId, out var task))
                {
                    task.IsClaimed = true;
                }
            }
        }

        /// <summary>
        /// Task'ın claimed durumunu ayarlar.
        /// </summary>
        public void SetClaimed(string taskId, bool isClaimed)
        {
            lock (_lock)
            {
                if (_tasks.TryGetValue(taskId, out var task))
                {
                    task.IsClaimed = isClaimed;
                }
            }
        }

        /// <summary>
        /// Task'ların kopyasını döndürür.
        /// </summary>
        public Dictionary<string, GameTask> GetTasksCopy()
        {
            lock (_lock)
            {
                var copy = new Dictionary<string, GameTask>();
                foreach (var kvp in _tasks)
                {
                    copy[kvp.Key] = CloneTask(kvp.Value);
                }
                return copy;
            }
        }

        /// <summary>
        /// Snapshot'tan full state restore eder.
        /// Deep copy yapılarak immutability korunur.
        /// </summary>
        public void RestoreFromSnapshot(IReadOnlyDictionary<string, GameTask> tasks)
        {
            lock (_lock)
            {
                _tasks.Clear();
                foreach (var kvp in tasks)
                {
                    // Deep copy to prevent snapshot mutation affecting restored state
                    _tasks[kvp.Key] = CloneTask(kvp.Value);
                }
            }
        }

        private static GameTask CloneTask(GameTask task)
        {
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
