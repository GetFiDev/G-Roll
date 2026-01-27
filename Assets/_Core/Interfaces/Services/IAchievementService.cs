using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Achievement (başarı) yönetimi için service interface.
    /// Progress tracking ve reward claim işlemlerini yönetir.
    /// ISnapshotable ile full rollback desteği sağlar.
    /// </summary>
    public interface IAchievementService : ISnapshotable<AchievementStateSnapshot>
    {
        /// <summary>
        /// Tüm achievement'ları döndürür.
        /// </summary>
        IReadOnlyList<Achievement> GetAllAchievements();

        /// <summary>
        /// Belirtilen achievement'ı döndürür.
        /// </summary>
        Achievement GetAchievement(string achievementId);

        /// <summary>
        /// Achievement unlock olmuş mu?
        /// </summary>
        bool IsUnlocked(string achievementId);

        /// <summary>
        /// Achievement ödülü claim edilmiş mi?
        /// </summary>
        bool IsClaimed(string achievementId);

        /// <summary>
        /// Achievement ödülünü optimistic olarak claim eder.
        /// </summary>
        UniTask<OperationResult> ClaimAchievementOptimisticAsync(string achievementId);

        /// <summary>
        /// Achievement progress'ini optimistic olarak günceller.
        /// Server'a batched olarak gönderilir.
        /// </summary>
        void UpdateProgressOptimistic(string achievementId, int progress);

        /// <summary>
        /// Server ile tam senkronizasyon yapar.
        /// </summary>
        UniTask SyncWithServerAsync();

        /// <summary>
        /// Achievement değiştiğinde tetiklenen event.
        /// </summary>
        event Action<AchievementChangedMessage> OnAchievementChanged;
    }

    /// <summary>
    /// Achievement data
    /// </summary>
    public class Achievement
    {
        public string AchievementId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int CurrentProgress { get; set; }
        public int TargetProgress { get; set; }
        public bool IsUnlocked => CurrentProgress >= TargetProgress;
        public bool IsClaimed { get; set; }
        public AchievementReward Reward { get; set; }
    }

    /// <summary>
    /// Achievement ödül bilgisi
    /// </summary>
    public class AchievementReward
    {
        public CurrencyType CurrencyType { get; set; }
        public int Amount { get; set; }
        public string ItemId { get; set; } // Opsiyonel item ödülü
    }

    /// <summary>
    /// Achievement state snapshot (ISnapshotable için - full rollback desteği).
    /// Tüm achievement state'ini saklar. Deep copy ile immutability garanti edilir.
    /// </summary>
    public readonly struct AchievementStateSnapshot
    {
        public IReadOnlyDictionary<string, Achievement> Achievements { get; }

        public AchievementStateSnapshot(Dictionary<string, Achievement> achievements)
        {
            // Deep copy - her Achievement klonlanır, referans paylaşılmaz
            var deepCopy = new Dictionary<string, Achievement>(achievements.Count);
            foreach (var kvp in achievements)
            {
                deepCopy[kvp.Key] = CloneAchievement(kvp.Value);
            }
            Achievements = deepCopy;
        }

        private static Achievement CloneAchievement(Achievement achievement)
        {
            if (achievement == null) return null;

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
