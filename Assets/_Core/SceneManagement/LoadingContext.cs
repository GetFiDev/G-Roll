using System;
using System.Threading;

namespace GRoll.Core.SceneManagement
{
    /// <summary>
    /// Loading ekrani icin context bilgileri
    /// </summary>
    public class LoadingContext
    {
        /// <summary>Loading tipi</summary>
        public LoadingType Type { get; set; } = LoadingType.SceneTransition;

        /// <summary>Ozel mesaj (null ise varsayilan mesaj kullanilir)</summary>
        public string CustomMessage { get; set; }

        /// <summary>Tahmini sure (progress bar icin)</summary>
        public float EstimatedDuration { get; set; }

        /// <summary>Progress degeri saglayan fonksiyon (0-1 arasi)</summary>
        public Func<float> ProgressProvider { get; set; }

        /// <summary>Iptal token'i</summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Varsayilan context olusturur
        /// </summary>
        public static LoadingContext Default => new()
        {
            Type = LoadingType.SceneTransition
        };

        /// <summary>
        /// Network operasyonu icin context olusturur
        /// </summary>
        public static LoadingContext ForNetwork(string message = null) => new()
        {
            Type = LoadingType.NetworkOperation,
            CustomMessage = message
        };

        /// <summary>
        /// Veri senkronizasyonu icin context olusturur
        /// </summary>
        public static LoadingContext ForDataSync(string message = null) => new()
        {
            Type = LoadingType.DataSync,
            CustomMessage = message
        };
    }
}
