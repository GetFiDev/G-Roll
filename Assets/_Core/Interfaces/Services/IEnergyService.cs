using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Energy (enerji) yönetimi için service interface.
    /// Energy consumption, regeneration ve refill işlemlerini yönetir.
    /// ISnapshotable ile full rollback desteği sağlar.
    /// </summary>
    public interface IEnergyService : ISnapshotable<EnergyStateSnapshot>
    {
        /// <summary>
        /// Mevcut energy miktarı
        /// </summary>
        int CurrentEnergy { get; }

        /// <summary>
        /// Maksimum energy miktarı
        /// </summary>
        int MaxEnergy { get; }

        /// <summary>
        /// Sonraki energy regeneration zamanı
        /// </summary>
        DateTime NextRegenTime { get; }

        /// <summary>
        /// Energy dolana kadar kalan süre
        /// </summary>
        TimeSpan TimeUntilFull { get; }

        /// <summary>
        /// Yeterli energy var mı?
        /// </summary>
        bool HasEnoughEnergy(int amount);

        /// <summary>
        /// Energy optimistic olarak harcar.
        /// Gameplay başlatmak için kullanılır.
        /// </summary>
        UniTask<OperationResult> ConsumeEnergyOptimisticAsync(int amount);

        /// <summary>
        /// Energy'yi tam doldurur (ad izleme veya satın alma sonrası).
        /// </summary>
        UniTask<OperationResult> RefillEnergyOptimisticAsync();

        /// <summary>
        /// Server'dan güncel energy snapshot'ını alır.
        /// </summary>
        UniTask<EnergySnapshot> FetchSnapshotAsync();

        /// <summary>
        /// Energy değiştiğinde tetiklenen event.
        /// </summary>
        event Action<EnergyChangedMessage> OnEnergyChanged;
    }

    /// <summary>
    /// Energy state snapshot (server response için)
    /// </summary>
    public class EnergySnapshot
    {
        public int CurrentEnergy { get; set; }
        public int MaxEnergy { get; set; }
        public long NextRegenTimestamp { get; set; } // Unix timestamp
        public int RegenIntervalSeconds { get; set; }
    }

    /// <summary>
    /// Energy state snapshot (ISnapshotable için - full rollback desteği).
    /// Tüm energy state'ini saklar.
    /// </summary>
    public readonly struct EnergyStateSnapshot
    {
        public int CurrentEnergy { get; }
        public int MaxEnergy { get; }
        public DateTime NextRegenTime { get; }

        public EnergyStateSnapshot(int currentEnergy, int maxEnergy, DateTime nextRegenTime)
        {
            CurrentEnergy = currentEnergy;
            MaxEnergy = maxEnergy;
            NextRegenTime = nextRegenTime;
        }
    }
}
