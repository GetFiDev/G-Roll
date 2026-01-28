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
    /// Authentication service implementasyonu.
    /// IFirebaseGateway üzerinden Firebase Auth işlemlerini gerçekleştirir.
    /// Eski UserDatabaseManager'ın auth fonksiyonlarının yerine geçer.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IFirebaseGateway _firebaseGateway;
        private readonly IMessageBus _messageBus;
        private readonly IGRollLogger _logger;

        private UserInfo _currentUser;
        private bool _isAuthenticated;

        public bool IsAuthenticated => _isAuthenticated;
        public bool IsLoggedIn => _isAuthenticated;
        public string CurrentUserId => _currentUser?.UserId;
        public UserInfo CurrentUser => _currentUser;

        public event Action<AuthStateChangedEventArgs> OnAuthStateChanged;

        [Inject]
        public AuthService(
            IFirebaseGateway firebaseGateway,
            IMessageBus messageBus,
            IGRollLogger logger)
        {
            _firebaseGateway = firebaseGateway;
            _messageBus = messageBus;
            _logger = logger;

            // Firebase Gateway'den auth state değişikliklerini dinle
            _firebaseGateway.OnAuthStateChanged += HandleFirebaseAuthStateChanged;

            // Başlangıç durumunu kontrol et
            CheckInitialAuthState();
        }

        private void CheckInitialAuthState()
        {
            if (_firebaseGateway.IsAuthenticated)
            {
                _isAuthenticated = true;
                _currentUser = new UserInfo
                {
                    UserId = _firebaseGateway.CurrentUserId,
                    DisplayName = _firebaseGateway.CurrentUserDisplayName,
                    Email = _firebaseGateway.CurrentUserEmail,
                    IsAnonymous = _firebaseGateway.IsAnonymous,
                    Provider = _firebaseGateway.IsAnonymous ? AuthProvider.Anonymous : AuthProvider.Google
                };
            }
        }

        public async UniTask<OperationResult<UserInfo>> SignInAnonymouslyAsync()
        {
            _logger.LogInfo("[AuthService] Signing in anonymously...");

            try
            {
                var success = await _firebaseGateway.SignInAnonymouslyAsync();

                if (success)
                {
                    _isAuthenticated = true;
                    _currentUser = new UserInfo
                    {
                        UserId = _firebaseGateway.CurrentUserId,
                        IsAnonymous = true,
                        Provider = AuthProvider.Anonymous
                    };

                    _logger.LogInfo($"[AuthService] Anonymous sign-in successful. UserId: {_currentUser.UserId}");

                    NotifyAuthStateChanged(AuthStateChangeReason.SignedIn);

                    return OperationResult<UserInfo>.Succeeded(_currentUser);
                }
                else
                {
                    _logger.LogError("[AuthService] Anonymous sign-in failed");
                    return OperationResult<UserInfo>.Failed("Anonymous sign-in failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AuthService] Anonymous sign-in exception: {ex.Message}");
                return OperationResult<UserInfo>.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult<UserInfo>> SignInWithGoogleAsync()
        {
            _logger.LogInfo("[AuthService] Signing in with Google...");

            try
            {
                var success = await _firebaseGateway.SignInWithGoogleAsync();

                if (success)
                {
                    _isAuthenticated = true;
                    _currentUser = new UserInfo
                    {
                        UserId = _firebaseGateway.CurrentUserId,
                        DisplayName = _firebaseGateway.CurrentUserDisplayName,
                        Email = _firebaseGateway.CurrentUserEmail,
                        IsAnonymous = false,
                        Provider = AuthProvider.Google
                    };

                    _logger.LogInfo($"[AuthService] Google sign-in successful. UserId: {_currentUser.UserId}");

                    NotifyAuthStateChanged(AuthStateChangeReason.SignedIn);

                    return OperationResult<UserInfo>.Succeeded(_currentUser);
                }
                else
                {
                    _logger.LogError("[AuthService] Google sign-in failed");
                    return OperationResult<UserInfo>.Failed("Google sign-in failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AuthService] Google sign-in exception: {ex.Message}");
                return OperationResult<UserInfo>.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult<UserInfo>> SignInWithAppleAsync()
        {
            _logger.LogInfo("[AuthService] Signing in with Apple...");

            try
            {
                var success = await _firebaseGateway.SignInWithAppleAsync();

                if (success)
                {
                    _isAuthenticated = true;
                    _currentUser = new UserInfo
                    {
                        UserId = _firebaseGateway.CurrentUserId,
                        DisplayName = _firebaseGateway.CurrentUserDisplayName,
                        Email = _firebaseGateway.CurrentUserEmail,
                        IsAnonymous = false,
                        Provider = AuthProvider.Apple
                    };

                    _logger.LogInfo($"[AuthService] Apple sign-in successful. UserId: {_currentUser.UserId}");

                    NotifyAuthStateChanged(AuthStateChangeReason.SignedIn);

                    return OperationResult<UserInfo>.Succeeded(_currentUser);
                }
                else
                {
                    _logger.LogError("[AuthService] Apple sign-in failed");
                    return OperationResult<UserInfo>.Failed("Apple sign-in failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AuthService] Apple sign-in exception: {ex.Message}");
                return OperationResult<UserInfo>.Failed(ex.Message);
            }
        }

        public async UniTask SignOutAsync()
        {
            _logger.LogInfo("[AuthService] Signing out...");

            try
            {
                await _firebaseGateway.SignOutAsync();

                _isAuthenticated = false;
                _currentUser = null;

                _logger.LogInfo("[AuthService] Sign out successful");

                NotifyAuthStateChanged(AuthStateChangeReason.SignedOut);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AuthService] Sign out exception: {ex.Message}");
            }
        }

        public async UniTask LogoutAsync()
        {
            await SignOutAsync();
        }

        public async UniTask<OperationResult> DeleteAccountAsync()
        {
            _logger.LogInfo("[AuthService] Deleting account...");

            if (!_isAuthenticated)
            {
                return OperationResult.Failed("Not authenticated");
            }

            try
            {
                var success = await _firebaseGateway.DeleteAccountAsync();

                if (success)
                {
                    _isAuthenticated = false;
                    _currentUser = null;

                    _logger.LogInfo("[AuthService] Account deleted successfully");

                    NotifyAuthStateChanged(AuthStateChangeReason.SignedOut);

                    return OperationResult.Succeeded();
                }
                else
                {
                    _logger.LogError("[AuthService] Account deletion failed");
                    return OperationResult.Failed("Account deletion failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AuthService] Delete account exception: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        public async UniTask<OperationResult> LinkWithGoogleAsync()
        {
            _logger.LogInfo("[AuthService] Linking with Google...");

            if (!_isAuthenticated || _currentUser == null)
            {
                return OperationResult.Failed("Not authenticated");
            }

            if (!_currentUser.IsAnonymous)
            {
                return OperationResult.Failed("Account is not anonymous");
            }

            try
            {
                var success = await _firebaseGateway.LinkWithGoogleAsync();

                if (success)
                {
                    _currentUser.IsAnonymous = false;
                    _currentUser.Provider = AuthProvider.Google;
                    _currentUser.DisplayName = _firebaseGateway.CurrentUserDisplayName;
                    _currentUser.Email = _firebaseGateway.CurrentUserEmail;

                    _logger.LogInfo("[AuthService] Link with Google successful");

                    NotifyAuthStateChanged(AuthStateChangeReason.AccountLinked);

                    return OperationResult.Succeeded();
                }
                else
                {
                    _logger.LogError("[AuthService] Link with Google failed");
                    return OperationResult.Failed("Link with Google failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AuthService] Link with Google exception: {ex.Message}");
                return OperationResult.Failed(ex.Message);
            }
        }

        private void HandleFirebaseAuthStateChanged(bool isAuthenticated, string userId)
        {
            if (isAuthenticated && !_isAuthenticated)
            {
                // Logged in externally
                _isAuthenticated = true;
                // User info will be updated when methods are called
            }
            else if (!isAuthenticated && _isAuthenticated)
            {
                // Logged out externally
                _isAuthenticated = false;
                _currentUser = null;
                NotifyAuthStateChanged(AuthStateChangeReason.SessionExpired);
            }
        }

        private void NotifyAuthStateChanged(AuthStateChangeReason reason)
        {
            var args = new AuthStateChangedEventArgs
            {
                IsAuthenticated = _isAuthenticated,
                User = _currentUser,
                Reason = reason
            };

            OnAuthStateChanged?.Invoke(args);
            _messageBus.Publish(new AuthStateChangedMessage(args));
        }
    }
}
