using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using Firebase.Functions;
using GRoll.Core.Interfaces.Infrastructure;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

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

                _functions = FirebaseFunctions.DefaultInstance;
                _isInitialized = true;

                _logger.Log("[Firebase] Initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Firebase] Initialization failed: {ex.Message}");
                throw;
            }
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
    }
}
