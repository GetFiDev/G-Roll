using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.SceneManagement
{
    /// <summary>
    /// Sahne gecislerini yoneten servis interface'i
    /// </summary>
    public interface ISceneFlowManager
    {
        /// <summary>Mevcut sahne tipi</summary>
        SceneType CurrentScene { get; }

        /// <summary>Gecis yapiliyor mu?</summary>
        bool IsTransitioning { get; }

        /// <summary>Loading ekrani acik mi?</summary>
        bool IsLoadingVisible { get; }

        /// <summary>
        /// Hedef sahneye gecis yapar
        /// </summary>
        /// <param name="targetScene">Hedef sahne</param>
        /// <param name="context">Loading context (null ise varsayilan)</param>
        UniTask TransitionToAsync(SceneType targetScene, LoadingContext context = null);

        /// <summary>
        /// Loading ekranini gosterir (sahne gecisi olmadan)
        /// </summary>
        /// <param name="type">Loading tipi</param>
        /// <param name="customMessage">Ozel mesaj</param>
        UniTask ShowLoadingAsync(LoadingType type = LoadingType.NetworkOperation, string customMessage = null);

        /// <summary>
        /// Loading ekranini gizler
        /// </summary>
        UniTask HideLoadingAsync();

        /// <summary>
        /// Loading progress'ini gunceller
        /// </summary>
        /// <param name="progress">0-1 arasi progress degeri</param>
        /// <param name="message">Opsiyonel mesaj</param>
        void UpdateLoadingProgress(float progress, string message = null);

        /// <summary>Sahne gecisi basladiginda tetiklenir</summary>
        event Action<SceneType, SceneType> OnSceneTransitionStarted;

        /// <summary>Sahne gecisi tamamlandiginda tetiklenir</summary>
        event Action<SceneType> OnSceneTransitionCompleted;

        /// <summary>Loading durumu degistiginde tetiklenir</summary>
        event Action<bool> OnLoadingStateChanged;
    }
}
