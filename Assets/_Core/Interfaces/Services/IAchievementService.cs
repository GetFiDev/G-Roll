using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Achievement odulunu optimistic olarak claim eder.
        /// </summary>
        UniTask<OperationResult> ClaimAchievementOptimisticAsync(string achievementId);

        /// <summary>
        /// Multi-level achievement icin tum eligible levelleri claim eder.
        /// </summary>
        UniTask<OperationResult<int>> ClaimAllEligibleOptimisticAsync(string achievementId);

        /// <summary>
        /// Achievement progress'ini optimistic olarak gunceller.
        /// Server'a batched olarak gonderilir.
        /// </summary>
        void UpdateProgressOptimistic(string achievementId, int progress);

        /// <summary>
        /// Tum achievement definitionlari dondurur.
        /// </summary>
        IReadOnlyList<AchievementDefinition> GetDefinitions();

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
    /// Achievement data (multi-level destekli)
    /// </summary>
    public class Achievement
    {
        public string AchievementId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public int CurrentProgress { get; set; }

        // Single-level compatibility
        public int TargetProgress { get; set; }
        public bool IsUnlocked => CurrentProgress >= TargetProgress;
        public bool IsClaimed { get; set; }
        public AchievementReward Reward { get; set; }

        // Multi-level support
        public IReadOnlyList<AchievementLevel> Levels { get; set; }
        public int CurrentLevel { get; set; }
        public IReadOnlyList<int> ClaimedLevels { get; set; }

        // Computed properties
        public int MaxLevel => Levels?.Count ?? 1;
        public bool IsMaxLevel => CurrentLevel >= MaxLevel;
        public bool HasMultipleLevels => Levels != null && Levels.Count > 1;

        /// <summary>Sonraki level (varsa)</summary>
        public AchievementLevel NextLevel =>
            Levels != null && CurrentLevel < Levels.Count ? Levels[CurrentLevel] : null;

        /// <summary>Sonraki level icin gereken progress</summary>
        public int NextThreshold => NextLevel?.TargetProgress ?? TargetProgress;

        /// <summary>Belirli bir level claim edilmis mi?</summary>
        public bool IsLevelClaimed(int level) => ClaimedLevels?.Contains(level) ?? false;
    }

    /// <summary>
    /// Achievement level bilgisi (multi-level achievementlar icin)
    /// </summary>
    public class AchievementLevel
    {
        public int Level { get; set; }
        public int TargetProgress { get; set; }
        public int RewardAmount { get; set; }
        public CurrencyType RewardType { get; set; }
    }

    /// <summary>
    /// Achievement odül bilgisi
    /// </summary>
    public class AchievementReward
    {
        public CurrencyType CurrencyType { get; set; }
        public int Amount { get; set; }
        public string ItemId { get; set; } // Opsiyonel item odulu
    }

    /// <summary>
    /// Achievement definition (server'dan gelen tanim)
    /// </summary>
    public class AchievementDefinition
    {
        public string TypeId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public IReadOnlyList<AchievementLevel> Levels { get; set; }
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

            // Clone levels
            List<AchievementLevel> clonedLevels = null;
            if (achievement.Levels != null)
            {
                clonedLevels = new List<AchievementLevel>(achievement.Levels.Count);
                foreach (var level in achievement.Levels)
                {
                    clonedLevels.Add(new AchievementLevel
                    {
                        Level = level.Level,
                        TargetProgress = level.TargetProgress,
                        RewardAmount = level.RewardAmount,
                        RewardType = level.RewardType
                    });
                }
            }

            // Clone claimed levels
            List<int> clonedClaimedLevels = null;
            if (achievement.ClaimedLevels != null)
            {
                clonedClaimedLevels = new List<int>(achievement.ClaimedLevels);
            }

            return new Achievement
            {
                AchievementId = achievement.AchievementId,
                Name = achievement.Name,
                Description = achievement.Description,
                IconUrl = achievement.IconUrl,
                CurrentProgress = achievement.CurrentProgress,
                TargetProgress = achievement.TargetProgress,
                IsClaimed = achievement.IsClaimed,
                CurrentLevel = achievement.CurrentLevel,
                Levels = clonedLevels,
                ClaimedLevels = clonedClaimedLevels,
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
