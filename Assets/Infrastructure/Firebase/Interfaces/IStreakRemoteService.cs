using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Streak remote service interface.
    /// Gunluk giris serisi ve odul yonetimi.
    /// </summary>
    public interface IStreakRemoteService
    {
        /// <summary>
        /// Streak durumunu getir (otomatik increment icin de cagrilir).
        /// </summary>
        UniTask<StreakSnapshot> FetchAsync();

        /// <summary>
        /// Bekleyen odulleri topla.
        /// </summary>
        UniTask<StreakClaimResult> ClaimAsync();
    }

    /// <summary>
    /// Streak durum snapshot'i
    /// </summary>
    public struct StreakSnapshot
    {
        public bool Ok { get; set; }
        public string Error { get; set; }

        public long ServerNowMillis { get; set; }
        public long NextUtcMidnightMillis { get; set; }

        public int TotalDays { get; set; }
        public int UnclaimedDays { get; set; }
        public double RewardPerDay { get; set; }
        public double PendingTotalReward { get; set; }

        public bool ClaimAvailable { get; set; }
        public bool TodayCounted { get; set; }

        /// <summary>Cihaz-sunucu saat farki: serverNow - deviceNowAtFetch</summary>
        public long ServerOffsetMs { get; set; }
    }

    /// <summary>
    /// Streak claim sonucu
    /// </summary>
    public struct StreakClaimResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public double Granted { get; set; }
        public double RewardPerDay { get; set; }
        public int UnclaimedDaysAfter { get; set; }
        public double NewCurrency { get; set; }
        public StreakSnapshot FreshStatus { get; set; }
    }
}
