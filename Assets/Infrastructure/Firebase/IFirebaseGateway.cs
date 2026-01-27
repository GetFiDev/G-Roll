using Cysharp.Threading.Tasks;

namespace GRoll.Infrastructure.Firebase
{
    /// <summary>
    /// Firebase servislerine merkezi erişim noktası.
    /// Tüm Firebase çağrıları bu interface üzerinden yapılır.
    /// </summary>
    public interface IFirebaseGateway
    {
        /// <summary>
        /// Firebase'i başlatır.
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// Firebase Cloud Function çağırır.
        /// </summary>
        /// <typeparam name="T">Response tipi</typeparam>
        /// <param name="functionName">Function adı</param>
        /// <param name="data">Request datası</param>
        /// <returns>Parsed response</returns>
        UniTask<T> CallFunctionAsync<T>(string functionName, object data = null);

        /// <summary>
        /// Firebase bağlantı durumu
        /// </summary>
        bool IsInitialized { get; }
    }
}
