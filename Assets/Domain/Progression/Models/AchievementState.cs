using System;
using System.Collections.Generic;
using System.Linq;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Domain.Progression.Models
{
    /// <summary>
    /// Achievement state'ini tutan internal class.
    /// AchievementService tarafından yönetilir.
    /// </summary>
    public class AchievementState
    {
        private readonly Dictionary<string, Achievement> _achievements = new();
        private readonly object _lock = new();

        /// <summary>
        /// Belirtilen achievement'ı döndürür.
        /// </summary>
        public Achievement GetAchievement(string achievementId)
        {
            lock (_lock)
            {
                return _achievements.TryGetValue(achievementId, out var achievement) ? achievement : null;
            }
        }

        /// <summary>
        /// Tüm achievement'ları döndürür.
        /// </summary>
        public IReadOnlyList<Achievement> GetAllAchievements()
        {
            lock (_lock)
            {
                return _achievements.Values.ToList();
            }
        }

        /// <summary>
        /// Achievement unlock olmuş mu?
        /// </summary>
        public bool IsUnlocked(string achievementId)
        {
            lock (_lock)
            {
                return _achievements.TryGetValue(achievementId, out var a) && a.IsUnlocked;
            }
        }

        /// <summary>
        /// Achievement claim edilmiş mi?
        /// </summary>
        public bool IsClaimed(string achievementId)
        {
            lock (_lock)
            {
                return _achievements.TryGetValue(achievementId, out var a) && a.IsClaimed;
            }
        }

        /// <summary>
        /// Achievement'ları ayarlar.
        /// </summary>
        public void SetAchievements(List<Achievement> achievements)
        {
            lock (_lock)
            {
                _achievements.Clear();
                foreach (var achievement in achievements)
                {
                    _achievements[achievement.AchievementId] = achievement;
                }
            }
        }

        /// <summary>
        /// Achievement progress'ini günceller.
        /// </summary>
        public void UpdateProgress(string achievementId, int progress)
        {
            lock (_lock)
            {
                if (_achievements.TryGetValue(achievementId, out var achievement))
                {
                    achievement.CurrentProgress = Math.Min(progress, achievement.TargetProgress);
                }
            }
        }

        /// <summary>
        /// Achievement'ı claimed olarak işaretler.
        /// </summary>
        public void MarkClaimed(string achievementId)
        {
            lock (_lock)
            {
                if (_achievements.TryGetValue(achievementId, out var achievement))
                {
                    achievement.IsClaimed = true;
                }
            }
        }

        /// <summary>
        /// Achievement'ın claimed durumunu ayarlar.
        /// </summary>
        public void SetClaimed(string achievementId, bool isClaimed)
        {
            lock (_lock)
            {
                if (_achievements.TryGetValue(achievementId, out var achievement))
                {
                    achievement.IsClaimed = isClaimed;
                }
            }
        }

        /// <summary>
        /// Achievement'ların kopyasını döndürür.
        /// </summary>
        public Dictionary<string, Achievement> GetAchievementsCopy()
        {
            lock (_lock)
            {
                var copy = new Dictionary<string, Achievement>();
                foreach (var kvp in _achievements)
                {
                    copy[kvp.Key] = CloneAchievement(kvp.Value);
                }
                return copy;
            }
        }

        /// <summary>
        /// Snapshot'tan full state restore eder.
        /// Deep copy yapılarak immutability korunur.
        /// </summary>
        public void RestoreFromSnapshot(IReadOnlyDictionary<string, Achievement> achievements)
        {
            lock (_lock)
            {
                _achievements.Clear();
                foreach (var kvp in achievements)
                {
                    // Deep copy to prevent snapshot mutation affecting restored state
                    _achievements[kvp.Key] = CloneAchievement(kvp.Value);
                }
            }
        }

        private static Achievement CloneAchievement(Achievement achievement)
        {
            return new Achievement
            {
                AchievementId = achievement.AchievementId,
                Name = achievement.Name,
                Description = achievement.Description,
                CurrentProgress = achievement.CurrentProgress,
                TargetProgress = achievement.TargetProgress,
                IsClaimed = achievement.IsClaimed,
                Reward = achievement.Reward != null ? new AchievementReward
                {
                    CurrencyType = achievement.Reward.CurrencyType,
                    Amount = achievement.Reward.Amount,
                    ItemId = achievement.Reward.ItemId
                } : null
            };
        }
    }
}
