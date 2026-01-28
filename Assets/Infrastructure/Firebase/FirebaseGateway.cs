using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Infrastructure.Firebase.Interfaces;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;
using VContainer.Unity;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
// AppleAuth is conditionally compiled only when the package is available
// To enable Apple Sign-In, add the sign-in-with-apple package via Unity Package Manager

namespace GRoll.Infrastructure.Firebase
{
    /// <summary>
    /// Firebase servislerine merkezi erişim noktası.
    /// Tüm Firebase çağrıları bu class üzerinden yapılır.
    /// IAsyncStartable implementasyonu ile VContainer tarafından otomatik başlatılır.
    /// </summary>
    public class FirebaseGateway : IFirebaseGateway, IAsyncStartable
    {
        private readonly IGRollLogger _logger;
        private FirebaseFunctions _functions;
        private FirebaseAuth _auth;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        // Auth properties
        public bool IsAuthenticated => _auth?.CurrentUser != null;
        public bool IsAnonymous => _auth?.CurrentUser?.IsAnonymous ?? true;
        public string CurrentUserId => _auth?.CurrentUser?.UserId;
        public string CurrentUserDisplayName => _auth?.CurrentUser?.DisplayName;
        public string CurrentUserEmail => _auth?.CurrentUser?.Email;

        public event Action<bool, string> OnAuthStateChanged;

        [Inject]
        public FirebaseGateway(IGRollLogger logger)
        {
            _logger = logger;
        }

        #region IAsyncStartable Implementation

        /// <summary>
        /// VContainer tarafından otomatik olarak çağrılır.
        /// Uygulama başlangıcında Firebase'i initialize eder.
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await InitializeAsync();
        }

        #endregion

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogWarning("[Firebase] Already initialized");
                return;
            }

            try
            {
                var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

                if (dependencyStatus != DependencyStatus.Available)
                {
                    throw new Exception($"Firebase dependencies not available: {dependencyStatus}");
                }

                _auth = FirebaseAuth.DefaultInstance;
                _functions = FirebaseFunctions.DefaultInstance;

                // Auth state listener
                _auth.StateChanged += HandleAuthStateChanged;

                _isInitialized = true;

                _logger.Log("[Firebase] Initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Initialization failed: {ex.Message}");
                throw;
            }
        }

        private void HandleAuthStateChanged(object sender, EventArgs e)
        {
            var user = _auth?.CurrentUser;
            OnAuthStateChanged?.Invoke(user != null, user?.UserId);
        }

        public async UniTask<T> CallFunctionAsync<T>(string functionName, object data = null)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Firebase not initialized");
            }

            try
            {
                _logger.Log($"[Firebase] Calling function: {functionName}");

                var callable = _functions.GetHttpsCallable(functionName);
                var result = await callable.CallAsync(data);

                return ParseResult<T>(result.Data);
            }
            catch (FunctionsException ex)
            {
                _logger.LogError($"[Firebase] Function error ({functionName}): {ex.ErrorCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Unexpected error ({functionName}): {ex.Message}");
                throw;
            }
        }

        private T ParseResult<T>(object data)
        {
            if (data == null)
                return default;

            // Firebase returns data as Dictionary/List, we need to serialize then deserialize
            var json = JsonConvert.SerializeObject(data);
            return JsonConvert.DeserializeObject<T>(json);
        }

        #region Auth Methods

        public async UniTask<bool> SignInAnonymouslyAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogError("[Firebase] Not initialized");
                return false;
            }

            try
            {
                _logger.Log("[Firebase] Signing in anonymously...");
                var result = await _auth.SignInAnonymouslyAsync();
                _logger.Log($"[Firebase] Anonymous sign-in successful. UserId: {result.User.UserId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Anonymous sign-in failed: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> SignInWithGoogleAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogError("[Firebase] Not initialized");
                return false;
            }

#if UNITY_ANDROID
            try
            {
                _logger.Log("[Firebase] Signing in with Google Play Games...");

                // Activate Google Play Games
                var config = new PlayGamesClientConfiguration.Builder().RequestServerAuthCode(false).Build();
                PlayGamesPlatform.InitializeInstance(config);
                PlayGamesPlatform.Activate();

                // Sign in to Google Play Games
                var tcs = new UniTaskCompletionSource<string>();

                PlayGamesPlatform.Instance.Authenticate(SignInInteractivity.CanPromptAlways, (signInStatus) =>
                {
                    if (signInStatus == SignInStatus.Success)
                    {
                        PlayGamesPlatform.Instance.RequestServerSideAccess(false, (authCode) =>
                        {
                            tcs.TrySetResult(authCode);
                        });
                    }
                    else
                    {
                        tcs.TrySetException(new Exception($"Google Play sign-in failed: {signInStatus}"));
                    }
                });

                var authCode = await tcs.Task;

                // Sign in to Firebase with Google credential
                var credential = GoogleAuthProvider.GetCredential(null, authCode);
                var result = await _auth.SignInWithCredentialAsync(credential);

                _logger.Log($"[Firebase] Google sign-in successful. UserId: {result.User.UserId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Google sign-in failed: {ex.Message}");
                return false;
            }
#else
            _logger.LogWarning("[Firebase] Google sign-in is only supported on Android");
            return false;
#endif
        }

        /// <summary>
        /// Signs in with Game Center on iOS.
        /// Links the Game Center account to Firebase Auth.
        /// </summary>
        public async UniTask<bool> SignInWithGameCenterAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogError("[Firebase] Not initialized");
                return false;
            }

#if UNITY_IOS
            try
            {
                _logger.Log("[Firebase] Signing in with Game Center...");

                // Authenticate with Game Center
                var tcs = new UniTaskCompletionSource<bool>();

                Social.localUser.Authenticate((success, error) =>
                {
                    if (success)
                    {
                        _logger.Log($"[Firebase] Game Center authenticated: {Social.localUser.id}");
                        tcs.TrySetResult(true);
                    }
                    else
                    {
                        _logger.LogError($"[Firebase] Game Center auth failed: {error}");
                        tcs.TrySetResult(false);
                    }
                });

                var gameCenterSuccess = await tcs.Task;
                if (!gameCenterSuccess)
                {
                    return false;
                }

                // Get Game Center credential for Firebase
                var credential = await GameCenterAuthProvider.GetCredentialAsync();
                var result = await _auth.SignInWithCredentialAsync(credential);

                _logger.Log($"[Firebase] Game Center sign-in successful. UserId: {result.UserId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Game Center sign-in failed: {ex.Message}");
                return false;
            }
#else
            _logger.LogWarning("[Firebase] Game Center sign-in is only supported on iOS");
            await UniTask.CompletedTask;
            return false;
#endif
        }

        /// <summary>
        /// Deprecated: Use SignInWithGameCenterAsync for iOS.
        /// This method is kept for backward compatibility but redirects to Game Center.
        /// </summary>
        [Obsolete("Use SignInWithGameCenterAsync instead. Apple Sign-In is not implemented.")]
        public async UniTask<bool> SignInWithAppleAsync()
        {
            // Redirect to Game Center on iOS
#if UNITY_IOS
            return await SignInWithGameCenterAsync();
#else
            _logger.LogWarning("[Firebase] Apple sign-in is not available. Use Game Center on iOS.");
            await UniTask.CompletedTask;
            return false;
#endif
        }

        public async UniTask SignOutAsync()
        {
            if (_auth != null)
            {
                _auth.SignOut();
                _logger.Log("[Firebase] Signed out");
            }
            await UniTask.CompletedTask;
        }

        public async UniTask<bool> LinkWithGoogleAsync()
        {
            if (!_isInitialized || _auth?.CurrentUser == null)
            {
                _logger.LogError("[Firebase] Not initialized or not signed in");
                return false;
            }

#if UNITY_ANDROID
            try
            {
                _logger.Log("[Firebase] Linking with Google...");

                var tcs = new UniTaskCompletionSource<string>();

                PlayGamesPlatform.Instance.RequestServerSideAccess(false, (authCode) =>
                {
                    tcs.TrySetResult(authCode);
                });

                var authCode = await tcs.Task;

                var credential = GoogleAuthProvider.GetCredential(null, authCode);
                await _auth.CurrentUser.LinkWithCredentialAsync(credential);

                _logger.Log("[Firebase] Link with Google successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Link with Google failed: {ex.Message}");
                return false;
            }
#else
            _logger.LogWarning("[Firebase] Google linking is only supported on Android");
            return false;
#endif
        }

        public async UniTask<bool> DeleteAccountAsync()
        {
            if (!_isInitialized || _auth?.CurrentUser == null)
            {
                _logger.LogError("[Firebase] Not initialized or not signed in");
                return false;
            }

            try
            {
                _logger.Log("[Firebase] Deleting account...");
                await _auth.CurrentUser.DeleteAsync();
                _logger.Log("[Firebase] Account deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Account deletion failed: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
