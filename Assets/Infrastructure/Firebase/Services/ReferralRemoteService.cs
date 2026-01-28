using Cysharp.Threading.Tasks;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Infrastructure.Firebase.Services
{
    /// <summary>
    /// Referral remote service implementasyonu.
    /// Firebase Cloud Functions ile iletisim saglar.
    /// </summary>
    public class ReferralRemoteService : IReferralRemoteService
    {
        private readonly IFirebaseGateway _firebase;

        [Inject]
        public ReferralRemoteService(IFirebaseGateway firebase)
        {
            _firebase = firebase;
        }

        public async UniTask<ReferralListResponse> GetReferralsAsync(int limit)
        {
            return await _firebase.CallFunctionAsync<ReferralListResponse>(
                "getReferrals",
                new { limit });
        }

        public async UniTask<ClaimEarningsResponse> ClaimEarningsAsync()
        {
            return await _firebase.CallFunctionAsync<ClaimEarningsResponse>("claimReferralEarnings");
        }

        public async UniTask<ReferralKeyResponse> GetOrCreateReferralKeyAsync()
        {
            return await _firebase.CallFunctionAsync<ReferralKeyResponse>("getOrCreateReferralKey");
        }

        public async UniTask<ApplyReferralResponse> ApplyReferralCodeAsync(string referralCode)
        {
            return await _firebase.CallFunctionAsync<ApplyReferralResponse>(
                "applyReferralCode",
                new { referralCode });
        }
    }
}
