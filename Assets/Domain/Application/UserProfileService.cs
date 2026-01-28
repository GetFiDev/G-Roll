using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Events.Messages;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using GRoll.Core.Optimistic;
using GRoll.Infrastructure.Firebase.Interfaces;
using VContainer;

namespace GRoll.Domain.Application
{
    /// <summary>
    /// Kullanıcı profili yönetimi servisi.
    /// User profile CRUD operasyonlarını yönetir.
    /// Eski UserDatabaseManager'ın profile fonksiyonlarının yerine geçer.
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private readonly IFirebaseGateway _firebaseGateway;
        private readonly IAuthService _authService;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private UserProfile _currentProfile;

        public UserProfile CurrentProfile => _currentProfile;
        public string DisplayName => _currentProfile?.Username;
        public bool IsLoaded => _currentProfile != null;
        public bool IsComplete => _currentProfile?.IsComplete ?? false;

        public event Action<UserProfile> OnProfileChanged;

        [Inject]
        public UserProfileService(
            IFirebaseGateway firebaseGateway,
            IAuthService authService,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _firebaseGateway = firebaseGateway;
            _authService = authService;
            _messageBus = messageBus;
            _logger = logger;

            // Auth state değişikliklerini dinle - logout olunca profile'ı temizle
            _authService.OnAuthStateChanged += HandleAuthStateChanged;
        }

        public async UniTask<OperationResult<UserProfile>> LoadProfileAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                _logger.LogError("[UserProfileService] Cannot load profile - not authenticated");
                return OperationResult<UserProfile>.Failed("Not authenticated");
            }

            try
            {
                _logger.LogInfo("[UserProfileService] Loading user profile...");

                var response = await _firebaseGateway.CallFunctionAsync<UserProfileResponse>("getUserProfile", null);

                if (response == null)
                {
                    _logger.LogWarning("[UserProfileService] Profile not found, creating new profile");
                    _currentProfile = new UserProfile
                    {
                        UserId = _authService.CurrentUserId,
                        CreatedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    _currentProfile = new UserProfile
                    {
                        UserId = response.userId ?? _authService.CurrentUserId,
                        Username = response.username,
                        AvatarId = response.avatarId,
                        HasElitePass = response.hasElitePass,
                        TotalGamesPlayed = response.totalGamesPlayed,
                        HighScore = response.highScore,
                        TotalCoinsEarned = response.totalCoinsEarned,
                        CreatedAt = response.createdAt,
                        LastLoginAt = response.lastLoginAt
                    };
                }

                _logger.LogInfo($"[UserProfileService] Profile loaded. Username: {_currentProfile.Username}, Complete: {_currentProfile.IsComplete}");

                NotifyProfileChanged();

                return OperationResult<UserProfile>.Succeeded(_currentProfile);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UserProfileService] Failed to load profile: {ex.Message}");
                return OperationResult<UserProfile>.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult> SetUsernameAsync(string username)
        {
            if (!_authService.IsAuthenticated)
            {
                return OperationResult.Failed("Not authenticated");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return OperationResult.Failed("Username cannot be empty");
            }

            try
            {
                _logger.LogInfo($"[UserProfileService] Setting username: {username}");

                var response = await _firebaseGateway.CallFunctionAsync<CompleteProfileResponse>(
                    "completeUserProfile",
                    new { username = username, referralCode = "" }
                );

                if (response != null && response.success)
                {
                    if (_currentProfile != null)
                    {
                        _currentProfile.Username = username;
                    }

                    _logger.LogInfo("[UserProfileService] Username set successfully");
                    NotifyProfileChanged();

                    return OperationResult.Succeeded();
                }
                else
                {
                    var errorCode = response?.errorCode ?? "UNKNOWN_ERROR";
                    _logger.LogError($"[UserProfileService] Failed to set username: {errorCode}");
                    return OperationResult.Failed(errorCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UserProfileService] Failed to set username: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult> SetAvatarAsync(string avatarId)
        {
            if (!_authService.IsAuthenticated)
            {
                return OperationResult.Failed("Not authenticated");
            }

            try
            {
                _logger.LogInfo($"[UserProfileService] Setting avatar: {avatarId}");

                var response = await _firebaseGateway.CallFunctionAsync<GenericResponse>(
                    "updateUserAvatar",
                    new { avatarId = avatarId }
                );

                if (response != null && response.success)
                {
                    if (_currentProfile != null)
                    {
                        _currentProfile.AvatarId = avatarId;
                    }

                    _logger.LogInfo("[UserProfileService] Avatar set successfully");
                    NotifyProfileChanged();

                    return OperationResult.Succeeded();
                }
                else
                {
                    return OperationResult.Failed("Failed to set avatar");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UserProfileService] Failed to set avatar: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult> UpdateDisplayNameOptimisticAsync(string displayName)
        {
            if (!_authService.IsAuthenticated)
            {
                return OperationResult.Failed("Not authenticated");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return OperationResult.Failed("Display name cannot be empty");
            }

            // Optimistic update - update local state immediately
            var previousName = _currentProfile?.Username;
            if (_currentProfile != null)
            {
                _currentProfile.Username = displayName;
                NotifyProfileChanged();
            }

            try
            {
                _logger.LogInfo($"[UserProfileService] Updating display name optimistically: {displayName}");

                var response = await _firebaseGateway.CallFunctionAsync<GenericResponse>(
                    "updateDisplayName",
                    new { displayName = displayName }
                );

                if (response != null && response.success)
                {
                    _logger.LogInfo("[UserProfileService] Display name updated successfully");
                    return OperationResult.Succeeded();
                }
                else
                {
                    // Rollback on failure
                    if (_currentProfile != null)
                    {
                        _currentProfile.Username = previousName;
                        NotifyProfileChanged();
                    }
                    return OperationResult.Failed(response?.errorMessage ?? "Failed to update display name");
                }
            }
            catch (Exception ex)
            {
                // Rollback on exception
                if (_currentProfile != null)
                {
                    _currentProfile.Username = previousName;
                    NotifyProfileChanged();
                }
                _logger.LogError($"[UserProfileService] Failed to update display name: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult> UpdateProfileAsync(UserProfileUpdateRequest request)
        {
            if (!_authService.IsAuthenticated)
            {
                return OperationResult.Failed("Not authenticated");
            }

            try
            {
                // Update fields that are provided
                if (!string.IsNullOrWhiteSpace(request.Username))
                {
                    var result = await SetUsernameAsync(request.Username);
                    if (!result.IsSuccess)
                    {
                        return result;
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.AvatarId))
                {
                    var result = await SetAvatarAsync(request.AvatarId);
                    if (!result.IsSuccess)
                    {
                        return result;
                    }
                }

                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UserProfileService] Failed to update profile: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult> ApplyReferralCodeAsync(string code)
        {
            if (!_authService.IsAuthenticated)
            {
                return OperationResult.Failed("Not authenticated");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return OperationResult.Failed("Referral code cannot be empty");
            }

            try
            {
                _logger.LogInfo($"[UserProfileService] Applying referral code: {code}");

                var response = await _firebaseGateway.CallFunctionAsync<GenericResponse>(
                    "applyReferralCode",
                    new { referralCode = code }
                );

                if (response != null && response.success)
                {
                    _logger.LogInfo("[UserProfileService] Referral code applied successfully");
                    return OperationResult.Succeeded();
                }
                else
                {
                    return OperationResult.Failed(response?.errorMessage ?? "Failed to apply referral code");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UserProfileService] Failed to apply referral code: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        public async UniTask<int> GetReferralCountAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                return 0;
            }

            try
            {
                var response = await _firebaseGateway.CallFunctionAsync<ReferralCountResponse>(
                    "getReferralCount",
                    null
                );

                return response?.count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UserProfileService] Failed to get referral count: {ex.Message}");
                return 0;
            }
        }

        private void HandleAuthStateChanged(AuthStateChangedEventArgs args)
        {
            if (args.Reason == AuthStateChangeReason.SignedOut)
            {
                _logger.LogInfo("[UserProfileService] User signed out, clearing profile");
                _currentProfile = null;
                NotifyProfileChanged();
            }
        }

        private void NotifyProfileChanged()
        {
            OnProfileChanged?.Invoke(_currentProfile);
            _messageBus.Publish(new UserProfileChangedMessage(_currentProfile));
        }

        #region Response DTOs

        private class UserProfileResponse
        {
            public string userId { get; set; }
            public string username { get; set; }
            public string avatarId { get; set; }
            public bool hasElitePass { get; set; }
            public int totalGamesPlayed { get; set; }
            public int highScore { get; set; }
            public int totalCoinsEarned { get; set; }
            public DateTime createdAt { get; set; }
            public DateTime lastLoginAt { get; set; }
        }

        private class CompleteProfileResponse
        {
            public bool success { get; set; }
            public string errorCode { get; set; }
        }

        private class GenericResponse
        {
            public bool success { get; set; }
            public string errorMessage { get; set; }
        }

        private class ReferralCountResponse
        {
            public int count { get; set; }
        }

        #endregion
    }
}
