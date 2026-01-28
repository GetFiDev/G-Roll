using System;
using Cysharp.Threading.Tasks;
using GRoll.Core.Events;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.SceneManagement.LoadingScene;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace GRoll.Core.SceneManagement
{
    /// <summary>
    /// Tum sahne gecislerini yoneten merkezi manager.
    /// Singleton olarak DontDestroyOnLoad'da yasayan RootLifetimeScope'a register edilir.
    /// </summary>
    public class SceneFlowManager : ISceneFlowManager
    {
        private readonly IGRollLogger _logger;
        private readonly IMessageBus _messageBus;

        private SceneType _currentScene = SceneType.Boot;
        private bool _isTransitioning;
        private bool _isLoadingVisible;

        public SceneType CurrentScene => _currentScene;
        public bool IsTransitioning => _isTransitioning;
        public bool IsLoadingVisible => _isLoadingVisible;

        public event Action<SceneType, SceneType> OnSceneTransitionStarted;
        public event Action<SceneType> OnSceneTransitionCompleted;
        public event Action<bool> OnLoadingStateChanged;

        [Inject]
        public SceneFlowManager(IGRollLogger logger, IMessageBus messageBus)
        {
            _logger = logger;
            _messageBus = messageBus;

            // Mevcut sahneyi tespit et
            DetectCurrentScene();
        }

        private void DetectCurrentScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            var sceneType = SceneRegistry.GetSceneType(activeScene.name);

            if (sceneType.HasValue)
            {
                _currentScene = sceneType.Value;
                _logger.Log($"[SceneFlow] Current scene detected: {_currentScene}");
            }
        }

        public async UniTask TransitionToAsync(SceneType targetScene, LoadingContext context = null)
        {
            if (_isTransitioning)
            {
                _logger.LogWarning("[SceneFlow] Transition already in progress, ignoring request");
                return;
            }

            if (targetScene == _currentScene)
            {
                _logger.LogWarning($"[SceneFlow] Already on scene {targetScene}, ignoring request");
                return;
            }

            _isTransitioning = true;
            context ??= LoadingContext.Default;

            var previousScene = _currentScene;
            _logger.Log($"[SceneFlow] Starting transition: {previousScene} -> {targetScene}");

            OnSceneTransitionStarted?.Invoke(previousScene, targetScene);
            _messageBus.Publish(new SceneTransitionMessage(previousScene, targetScene, true));

            try
            {
                // 1. Loading Scene'i additive olarak yukle
                await LoadLoadingSceneAsync();

                // 2. Loading ekranini goster
                await ShowLoadingInternalAsync(context.Type, context.CustomMessage);

                // 3. Hedef sahneyi async yukle
                var targetSceneName = SceneRegistry.GetSceneName(targetScene);
                var loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);

                if (loadOp == null)
                {
                    _logger.LogError($"[SceneFlow] Failed to load scene: {targetSceneName}");
                    await HideLoadingAsync();
                    _isTransitioning = false;
                    return;
                }

                loadOp.allowSceneActivation = false;

                // 4. Progress guncelle
                while (loadOp.progress < 0.9f)
                {
                    float progress = Mathf.Clamp01(loadOp.progress / 0.9f);
                    UpdateLoadingProgress(progress);

                    if (context.ProgressProvider != null)
                    {
                        progress = context.ProgressProvider();
                    }

                    await UniTask.Yield();
                }

                // 5. Sahneyi aktive et
                loadOp.allowSceneActivation = true;

                // Sahne tamamen yüklenene kadar bekle
                await UniTask.WaitUntil(() => loadOp.isDone);

                // 6. Kısa bekleme (sahne objelerinin initialize olması için)
                await UniTask.Delay(100);

                _currentScene = targetScene;

                // 7. Loading ekranini gizle
                await HideLoadingAsync();

                _logger.Log($"[SceneFlow] Transition completed: {targetScene}");
                OnSceneTransitionCompleted?.Invoke(targetScene);
                _messageBus.Publish(new SceneTransitionMessage(previousScene, targetScene, false));
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SceneFlow] Transition failed: {ex.Message}");
                await HideLoadingAsync();
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async UniTask ShowLoadingAsync(LoadingType type = LoadingType.NetworkOperation, string customMessage = null)
        {
            if (_isLoadingVisible)
            {
                // Sadece mesaji guncelle
                UpdateLoadingProgress(0f, customMessage);
                return;
            }

            await LoadLoadingSceneAsync();
            await ShowLoadingInternalAsync(type, customMessage);
        }

        public async UniTask HideLoadingAsync()
        {
            if (!_isLoadingVisible)
                return;

            var controller = LoadingSceneController.Instance;
            if (controller != null)
            {
                await controller.HideAsync();
            }

            // Loading Scene'i unload et
            await UnloadLoadingSceneAsync();

            _isLoadingVisible = false;
            OnLoadingStateChanged?.Invoke(false);
        }

        public void UpdateLoadingProgress(float progress, string message = null)
        {
            var controller = LoadingSceneController.Instance;
            if (controller != null)
            {
                controller.SetProgress(progress);

                if (!string.IsNullOrEmpty(message))
                {
                    controller.SetStatusMessage(message);
                }
            }
        }

        private async UniTask LoadLoadingSceneAsync()
        {
            // Loading Scene zaten yuklu mu kontrol et
            var loadingScene = SceneManager.GetSceneByName(SceneRegistry.LoadingScene);
            if (loadingScene.isLoaded)
                return;

            var loadOp = SceneManager.LoadSceneAsync(SceneRegistry.LoadingScene, LoadSceneMode.Additive);
            if (loadOp != null)
            {
                await UniTask.WaitUntil(() => loadOp.isDone);
            }
        }

        private async UniTask UnloadLoadingSceneAsync()
        {
            var loadingScene = SceneManager.GetSceneByName(SceneRegistry.LoadingScene);
            if (!loadingScene.isLoaded)
                return;

            var unloadOp = SceneManager.UnloadSceneAsync(loadingScene);
            if (unloadOp != null)
            {
                await UniTask.WaitUntil(() => unloadOp.isDone);
            }
        }

        private async UniTask ShowLoadingInternalAsync(LoadingType type, string customMessage)
        {
            // Controller hazır olana kadar bekle
            await UniTask.WaitUntil(() => LoadingSceneController.Instance != null);

            var controller = LoadingSceneController.Instance;
            await controller.ShowAsync(type, customMessage);

            _isLoadingVisible = true;
            OnLoadingStateChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// Sahne gecisi mesaji
    /// </summary>
    public readonly struct SceneTransitionMessage : IMessage
    {
        public SceneType FromScene { get; }
        public SceneType ToScene { get; }
        public bool IsStarting { get; }

        public SceneTransitionMessage(SceneType from, SceneType to, bool isStarting)
        {
            FromScene = from;
            ToScene = to;
            IsStarting = isStarting;
        }
    }
}
