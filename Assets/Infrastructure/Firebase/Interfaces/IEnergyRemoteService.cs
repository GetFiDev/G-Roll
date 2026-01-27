using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Energy işlemleri için remote service interface.
    /// Firebase Cloud Functions çağrılarını soyutlar.
    /// </summary>
    public interface IEnergyRemoteService
    {
        /// <summary>
        /// Energy harcar.
        /// </summary>
        UniTask<EnergyOperationResponse> ConsumeEnergyAsync(int amount);

        /// <summary>
        /// Energy'yi doldurur (ad/IAP sonrası).
        /// </summary>
        UniTask<EnergyOperationResponse> RefillEnergyAsync();

        /// <summary>
        /// Server'dan güncel energy state'ini alır.
        /// </summary>
        UniTask<EnergyStateResponse> FetchEnergyStateAsync();
    }

    /// <summary>
    /// Energy operation response
    /// </summary>
    public struct EnergyOperationResponse
    {
        public bool Success { get; set; }
        public int CurrentEnergy { get; set; }
        public DateTime NextRegenTime { get; set; }
        public string ErrorMessage { get; set; }

        public static EnergyOperationResponse Successful(int currentEnergy, DateTime nextRegenTime)
        {
            return new EnergyOperationResponse
            {
                Success = true,
                CurrentEnergy = currentEnergy,
                NextRegenTime = nextRegenTime
            };
        }

        public static EnergyOperationResponse Failed(string error)
        {
            return new EnergyOperationResponse
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Energy state response
    /// </summary>
    public struct EnergyStateResponse
    {
        public int CurrentEnergy { get; set; }
        public int MaxEnergy { get; set; }
        public DateTime NextRegenTime { get; set; }
        public int RegenIntervalSeconds { get; set; }
    }
}
