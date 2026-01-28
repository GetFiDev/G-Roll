using System;
using UnityEngine;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Gameplay kamera yonetimi servisi.
    /// Kamera pozisyonu, transition ve tracking islemlerini yonetir.
    /// Eski GameplayCameraManager'in yerini alir.
    /// </summary>
    public interface ICameraService
    {
        #region Configuration Properties

        /// <summary>Gameplay offset (kamera pozisyonu player'a gore)</summary>
        Vector3 GameplayOffset { get; set; }

        /// <summary>Gameplay rotasyonu</summary>
        Vector3 GameplayRotation { get; set; }

        /// <summary>Gameplay FOV</summary>
        float GameplayFOV { get; set; }

        /// <summary>Yatay deadzone (X ekseni)</summary>
        float HorizontalDeadZone { get; set; }

        /// <summary>Smooth follow hizi</summary>
        float FollowSmoothSpeed { get; set; }

        #endregion

        #region State Properties

        /// <summary>Kamera player'i takip ediyor mu?</summary>
        bool IsFollowing { get; }

        /// <summary>Transition devam ediyor mu?</summary>
        bool IsTransitioning { get; }

        /// <summary>Z-axis bagimsiz tracking aktif mi?</summary>
        bool IsZTrackingActive { get; }

        #endregion

        #region Events

        /// <summary>Transition tamamlandiginda</summary>
        event Action OnTransitionCompleted;

        /// <summary>Z-tracking basladiginda</summary>
        event Action OnZTrackingStarted;

        #endregion

        #region Methods

        /// <summary>
        /// Kamerayi player transform ile initialize et
        /// </summary>
        void Initialize(Transform playerTransform);

        /// <summary>
        /// Kamerayi belirtilen kamera ile initialize et
        /// </summary>
        void Initialize(Transform playerTransform, Camera camera);

        /// <summary>
        /// Acilis transition'i oynat (start -> gameplay config)
        /// </summary>
        void PlayOpeningTransition(Action onComplete = null);

        /// <summary>
        /// Z-axis tracking'i baslat
        /// </summary>
        void StartZTracking();

        /// <summary>
        /// Z-axis tracking'i durdur
        /// </summary>
        void StopZTracking();

        /// <summary>
        /// Kamerayi initial state'e resetle
        /// </summary>
        void Reset();

        /// <summary>
        /// Her frame cagrilmali (kamera pozisyon update)
        /// </summary>
        void Tick(float deltaTime);

        #endregion
    }
}
