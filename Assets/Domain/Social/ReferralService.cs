using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Social
{
    /// <summary>
    /// Referral servisi implementasyonu.
    /// Referral kodlari, kazanclar ve referral listesi yonetimini saglar.
    /// </summary>
    public class ReferralService : IReferralService
    {
        #region Dependencies

        private readonly IReferralRemoteService _remoteService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        #endregion

        #region State

        private readonly List<ReferralEntry> _cachedReferrals = new();
        private string _myReferralKey = "-";
        private int _globalReferralCount;
        private decimal _pendingEarnings;
        private bool _isCacheLoaded;
        private readonly object _stateLock = new();

        #endregion

        #region Properties

        public bool IsCacheLoaded
        {
            get { lock (_stateLock) return _isCacheLoaded; }
        }

        public string MyReferralKey
        {
            get { lock (_stateLock) return _myReferralKey; }
        }

        public int GlobalReferralCount
        {
            get { lock (_stateLock) return _globalReferralCount; }
        }

        public decimal PendingEarnings
        {
            get { lock (_stateLock) return _pendingEarnings; }
        }

        public IReadOnlyList<ReferralEntry> CachedReferrals
        {
            get { lock (_stateLock) return _cachedReferrals.AsReadOnly(); }
        }

        #endregion

        #region Events

        public event Action OnCacheUpdated;
        public event Action<decimal> OnEarningsClaimed;

        #endregion

        #region Constructor

        [Inject]
        public ReferralService(
            IReferralRemoteService remoteService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _remoteService = remoteService;
            _messageBus = messageBus;
            _logger = logger;
        }

        #endregion

        #region Methods

        public async UniTask<OperationResult<ReferralCacheResult>> RefreshCacheAsync(int limit = 100)
        {
            try
            {
                _logger.Log($"[ReferralService] Refreshing cache with limit: {limit}");

                var response = await _remoteService.GetReferralsAsync(limit);

                if (!response.Success)
                {
                    _logger.LogWarning($"[ReferralService] Refresh failed: {response.ErrorMessage}");
                    return OperationResult<ReferralCacheResult>.Failed(response.ErrorMessage);
                }

                // Update state
                lock (_stateLock)
                {
                    _cachedReferrals.Clear();

                    if (response.Referrals != null)
                    {
                        foreach (var data in response.Referrals)
                        {
                            _cachedReferrals.Add(new ReferralEntry
                            {
                                UserId = data.UserId,
                                Username = data.Username,
                                EarnedTotal = data.EarnedTotal,
                                JoinedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.JoinedAtTimestamp).DateTime
                            });
                        }
                    }

                    _globalReferralCount = response.TotalCount;
                    _pendingEarnings = response.PendingTotal;
                    _myReferralKey = response.MyReferralKey ?? "-";
                    _isCacheLoaded = true;
                }

                // Publish events
                OnCacheUpdated?.Invoke();
                _messageBus?.Publish(new ReferralCacheUpdatedMessage(_globalReferralCount, _pendingEarnings));

                var result = new ReferralCacheResult
                {
                    Referrals = _cachedReferrals.AsReadOnly(),
                    TotalCount = _globalReferralCount,
                    PendingTotal = _pendingEarnings,
                    MyReferralKey = _myReferralKey
                };

                _logger.Log($"[ReferralService] Cache refreshed. Count: {_globalReferralCount}, Pending: {_pendingEarnings}");
                return OperationResult<ReferralCacheResult>.Succeeded(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ReferralService] Refresh error: {ex.Message}");
                return OperationResult<ReferralCacheResult>.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult<decimal>> ClaimEarningsAsync()
        {
            decimal pendingAmount;

            lock (_stateLock)
            {
                if (_pendingEarnings <= 0)
                {
                    return OperationResult<decimal>.ValidationError("No earnings to claim");
                }
                pendingAmount = _pendingEarnings;
            }

            try
            {
                _logger.Log($"[ReferralService] Claiming earnings: {pendingAmount}");

                // Optimistic update
                lock (_stateLock)
                {
                    _pendingEarnings = 0;
                }
                _messageBus?.Publish(new ReferralEarningsClaimedMessage(pendingAmount, true));

                // Server request
                var response = await _remoteService.ClaimEarningsAsync();

                if (!response.Success)
                {
                    // Rollback
                    lock (_stateLock)
                    {
                        _pendingEarnings = pendingAmount;
                    }
                    _logger.LogWarning($"[ReferralService] Claim failed: {response.ErrorMessage}");
                    return OperationResult<decimal>.RolledBack(response.ErrorMessage);
                }

                // Confirm
                OnEarningsClaimed?.Invoke(response.ClaimedAmount);
                _messageBus?.Publish(new ReferralEarningsClaimedMessage(response.ClaimedAmount, false));

                _logger.Log($"[ReferralService] Earnings claimed: {response.ClaimedAmount}");
                return OperationResult<decimal>.Succeeded(response.ClaimedAmount);
            }
            catch (Exception ex)
            {
                // Rollback
                lock (_stateLock)
                {
                    _pendingEarnings = pendingAmount;
                }
                _logger.LogError($"[ReferralService] Claim error: {ex.Message}");
                return OperationResult<decimal>.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult<string>> GetOrCreateReferralKeyAsync()
        {
            try
            {
                _logger.Log("[ReferralService] Getting or creating referral key");

                var response = await _remoteService.GetOrCreateReferralKeyAsync();

                if (!response.Success)
                {
                    _logger.LogWarning($"[ReferralService] GetOrCreate failed: {response.ErrorMessage}");
                    return OperationResult<string>.Failed(response.ErrorMessage);
                }

                lock (_stateLock)
                {
                    _myReferralKey = response.ReferralKey;
                }

                if (response.IsNewlyCreated)
                {
                    _messageBus?.Publish(new ReferralKeyGeneratedMessage(response.ReferralKey));
                }

                _logger.Log($"[ReferralService] Referral key: {response.ReferralKey} (New: {response.IsNewlyCreated})");
                return OperationResult<string>.Succeeded(response.ReferralKey);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ReferralService] GetOrCreate error: {ex.Message}");
                return OperationResult<string>.NetworkError(ex);
            }
        }

        public async UniTask<OperationResult> ApplyReferralCodeAsync(string referralCode)
        {
            if (string.IsNullOrWhiteSpace(referralCode))
            {
                return OperationResult.ValidationError("Referral code is required");
            }

            try
            {
                _logger.Log($"[ReferralService] Applying referral code: {referralCode}");

                var response = await _remoteService.ApplyReferralCodeAsync(referralCode);

                if (!response.Success)
                {
                    _logger.LogWarning($"[ReferralService] Apply failed: {response.ErrorMessage}");
                    return OperationResult.Failed(response.ErrorMessage);
                }

                _logger.Log("[ReferralService] Referral code applied successfully");
                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ReferralService] Apply error: {ex.Message}");
                return OperationResult.NetworkError(ex);
            }
        }

        #endregion
    }
}
