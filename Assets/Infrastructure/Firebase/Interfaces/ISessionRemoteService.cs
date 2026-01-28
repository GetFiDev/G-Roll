using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;

namespace GRoll.Infrastructure.Firebase.Interfaces
{
    /// <summary>
    /// Session işlemleri için remote service interface.
    /// Firebase Cloud Functions çağrılarını soyutlar.
    /// </summary>
    public interface ISessionRemoteService
    {
        /// <summary>
        /// Yeni session için token ister.
        /// </summary>
        UniTask<RequestSessionResponse> RequestSessionAsync(Core.GameMode mode);

        /// <summary>
        /// Session sonuçlarını gönderir.
        /// </summary>
        UniTask<SubmitSessionResponse> SubmitSessionAsync(SessionData data);

        /// <summary>
        /// Session'ı iptal eder.
        /// </summary>
        UniTask CancelSessionAsync(string sessionId);
    }

    /// <summary>
    /// Request session response
    /// </summary>
    public struct RequestSessionResponse
    {
        public bool Success { get; set; }
        public SessionInfo SessionInfo { get; set; }
        public string ErrorMessage { get; set; }

        public static RequestSessionResponse Successful(SessionInfo info)
        {
            return new RequestSessionResponse { Success = true, SessionInfo = info };
        }

        public static RequestSessionResponse Failed(string error)
        {
            return new RequestSessionResponse { Success = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// Submit session response
    /// </summary>
    public struct SubmitSessionResponse
    {
        public bool Success { get; set; }
        public SessionResult Result { get; set; }
        public string ErrorMessage { get; set; }

        public static SubmitSessionResponse Successful(SessionResult result)
        {
            return new SubmitSessionResponse { Success = true, Result = result };
        }

        public static SubmitSessionResponse Failed(string error)
        {
            return new SubmitSessionResponse { Success = false, ErrorMessage = error };
        }
    }
}
