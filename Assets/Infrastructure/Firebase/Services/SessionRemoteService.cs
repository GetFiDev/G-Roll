using Cysharp.Threading.Tasks;
using GRoll.Core;
using GRoll.Core.Interfaces.Services;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Session işlemleri için Firebase Cloud Functions implementasyonu.
    /// </summary>
    public class SessionRemoteService : ISessionRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public SessionRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<RequestSessionResponse> RequestSessionAsync(GameMode mode)
        {
            var result = await _firebase.CallFunctionAsync<RequestSessionResponse>(
                "requestSession",
                new { gameMode = (int)mode }
            );
            return result;
        }

        public async UniTask<SubmitSessionResponse> SubmitSessionAsync(SessionData data)
        {
            var result = await _firebase.CallFunctionAsync<SubmitSessionResponse>(
                "submitSession",
                new
                {
                    sessionId = data.SessionId,
                    score = data.Score,
                    durationSeconds = data.DurationSeconds,
                    coinsCollected = data.CoinsCollected,
                    distance = data.Distance,
                    endReason = data.EndReason
                }
            );
            return result;
        }

        public async UniTask CancelSessionAsync(string sessionId)
        {
            await _firebase.CallFunctionAsync<object>(
                "cancelSession",
                new { sessionId }
            );
        }
    }
}
