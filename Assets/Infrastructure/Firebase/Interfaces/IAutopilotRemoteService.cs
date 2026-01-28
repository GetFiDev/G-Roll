using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Autopilot remote service interface.
    /// Otomatik kazanc sistemi yonetimi.
    /// </summary>
    public interface IAutopilotRemoteService
    {
        /// <summary>
        /// Autopilot durumunu getir.
        /// </summary>
        UniTask<AutopilotStatus> GetStatusAsync();

        /// <summary>
        /// Autopilot'u ac/kapat.
        /// </summary>
        UniTask<AutopilotToggleResult> ToggleAsync(bool on);

        /// <summary>
        /// Biriken kazanci topla.
        /// </summary>
        UniTask<AutopilotClaimResult> ClaimAsync();
    }

    /// <summary>
    /// Autopilot durum bilgisi
    /// </summary>
    public struct AutopilotStatus
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public long ServerNowMillis { get; set; }

        public bool IsElite { get; set; }
        public bool IsAutopilotOn { get; set; }
        public double AutopilotWallet { get; set; }
        public long Currency { get; set; }

        public double NormalUserEarningPerHour { get; set; }
        public double EliteUserEarningPerHour { get; set; }
        public double NormalUserMaxAutopilotDurationInHours { get; set; }

        public long? AutopilotActivationDateMillis { get; set; }
        public long AutopilotLastClaimedAtMillis { get; set; }

        public long? TimeToCapSeconds { get; set; }
        public bool IsClaimReady { get; set; }

        public DateTime ServerNowUtc => DateTimeOffset.FromUnixTimeMilliseconds(ServerNowMillis).UtcDateTime;
        public double CapGainNormal => NormalUserEarningPerHour * NormalUserMaxAutopilotDurationInHours;
    }

    /// <summary>
    /// Autopilot toggle sonucu
    /// </summary>
    public struct AutopilotToggleResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public bool IsAutopilotOn { get; set; }
    }

    /// <summary>
    /// Autopilot claim sonucu
    /// </summary>
    public struct AutopilotClaimResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public long Claimed { get; set; }
        public long CurrencyAfter { get; set; }
    }
}
