using System;
using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Events.Messages;
using GRoll.Core.Optimistic;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Currency (para birimi) yönetimi için service interface.
    /// Optimistic update ve rollback destekler.
    /// </summary>
    public interface ICurrencyService : ISnapshotable<CurrencySnapshot>
    {
        /// <summary>
        /// Belirtilen para biriminin mevcut bakiyesini döndürür.
        /// </summary>
        int GetBalance(CurrencyType type);

        /// <summary>
        /// Belirtilen miktarı karşılayabilir mi?
        /// </summary>
        bool CanAfford(CurrencyType type, int amount);

        /// <summary>
        /// Optimistic olarak currency ekler.
        /// UI hemen güncellenir, arka planda server'a sync edilir.
        /// </summary>
        /// <param name="type">Para birimi tipi</param>
        /// <param name="amount">Eklenecek miktar</param>
        /// <param name="source">Kaynak (achievement, task, purchase, etc.)</param>
        UniTask<OperationResult> AddCurrencyOptimisticAsync(CurrencyType type, int amount, string source);

        /// <summary>
        /// Optimistic olarak currency harcar.
        /// UI hemen güncellenir, başarısızlıkta rollback yapılır.
        /// </summary>
        /// <param name="type">Para birimi tipi</param>
        /// <param name="amount">Harcanacak miktar</param>
        /// <param name="reason">Harcama sebebi</param>
        UniTask<OperationResult> SpendCurrencyOptimisticAsync(CurrencyType type, int amount, string reason);

        /// <summary>
        /// Server ile tam senkronizasyon yapar.
        /// Local state'i server state'i ile değiştirir.
        /// </summary>
        UniTask SyncWithServerAsync();

        /// <summary>
        /// Currency değiştiğinde tetiklenen event.
        /// </summary>
        event Action<CurrencyChangedMessage> OnCurrencyChanged;
    }

    /// <summary>
    /// Currency state snapshot - rollback için
    /// </summary>
    public class CurrencySnapshot
    {
        public int SoftCurrencyBalance { get; set; }
        public int HardCurrencyBalance { get; set; }
        public long Timestamp { get; set; }
    }
}
