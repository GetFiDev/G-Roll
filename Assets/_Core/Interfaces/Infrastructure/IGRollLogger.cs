using System;

namespace GRoll.Core.Interfaces.Infrastructure
{
    /// <summary>
    /// Logger service interface.
    /// Logging işlemlerini soyutlar, farklı implementasyonlara izin verir.
    /// UnityEngine.ILogger ile çakışmamak için IGRollLogger adını kullanıyoruz.
    /// </summary>
    public interface IGRollLogger
    {
        /// <summary>
        /// Debug seviyesinde log yazar.
        /// </summary>
        void Log(string message);

        /// <summary>
        /// Info seviyesinde log yazar.
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Uyarı seviyesinde log yazar.
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Hata seviyesinde log yazar.
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// Exception ile birlikte hata log'u yazar.
        /// </summary>
        void LogError(string message, Exception exception);

        /// <summary>
        /// Formatted log yazar.
        /// </summary>
        void LogFormat(string format, params object[] args);
    }
}
