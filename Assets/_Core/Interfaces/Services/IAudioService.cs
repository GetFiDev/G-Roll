using System;
using Cysharp.Threading.Tasks;

namespace GRoll.Core.Interfaces.Services
{
    /// <summary>
    /// Ses yönetimi için service interface.
    /// Müzik ve ses efektlerini yönetir.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Müzik açık mı?
        /// </summary>
        bool IsMusicEnabled { get; }

        /// <summary>
        /// Ses efektleri açık mı?
        /// </summary>
        bool IsSfxEnabled { get; }

        /// <summary>
        /// Müzik ses seviyesi (0-1)
        /// </summary>
        float MusicVolume { get; }

        /// <summary>
        /// Ses efekti ses seviyesi (0-1)
        /// </summary>
        float SfxVolume { get; }

        /// <summary>
        /// Müziği açar/kapatır.
        /// </summary>
        void SetMusicEnabled(bool enabled);

        /// <summary>
        /// Ses efektlerini açar/kapatır.
        /// </summary>
        void SetSfxEnabled(bool enabled);

        /// <summary>
        /// Müzik ses seviyesini ayarlar.
        /// </summary>
        void SetMusicVolume(float volume);

        /// <summary>
        /// Ses efekti ses seviyesini ayarlar.
        /// </summary>
        void SetSfxVolume(float volume);

        /// <summary>
        /// Belirtilen müziği çalar.
        /// </summary>
        /// <param name="trackId">Müzik track ID'si</param>
        /// <param name="fadeIn">Fade in süresi (saniye)</param>
        UniTask PlayMusicAsync(string trackId, float fadeIn = 0.5f);

        /// <summary>
        /// Mevcut müziği durdurur.
        /// </summary>
        /// <param name="fadeOut">Fade out süresi (saniye)</param>
        UniTask StopMusicAsync(float fadeOut = 0.5f);

        /// <summary>
        /// Müziği duraklatır.
        /// </summary>
        void PauseMusic();

        /// <summary>
        /// Müziği devam ettirir.
        /// </summary>
        void ResumeMusic();

        /// <summary>
        /// Ses efekti çalar.
        /// </summary>
        /// <param name="sfxId">Ses efekti ID'si</param>
        void PlaySfx(string sfxId);

        /// <summary>
        /// Ses efekti çalar (belirli pozisyonda).
        /// </summary>
        void PlaySfxAtPosition(string sfxId, UnityEngine.Vector3 position);

        /// <summary>
        /// UI ses efekti çalar.
        /// </summary>
        void PlayUISound(UISoundType type);

        /// <summary>
        /// Müzik değiştiğinde tetiklenen event.
        /// </summary>
        event Action<string> OnMusicChanged;
    }

    /// <summary>
    /// UI ses tipleri
    /// </summary>
    public enum UISoundType
    {
        ButtonClick,
        ButtonHover,
        PanelOpen,
        PanelClose,
        Success,
        Error,
        Warning,
        Notification,
        Purchase,
        Reward
    }
}
