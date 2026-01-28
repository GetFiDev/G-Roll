using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using UnityEngine;
using VContainer;

namespace GRoll.Infrastructure.Services
{
    /// <summary>
    /// Audio service implementation.
    /// Manages music and sound effects.
    /// Replaces BackgroundMusicManager and AudioManager.
    /// </summary>
    public class AudioService : IAudioService
    {
        private readonly IGRollLogger _logger;

        // Audio sources will be set from Unity
        private AudioSource _musicSource;
        private AudioSource _sfxSource;

        // Audio clip registries
        private readonly Dictionary<string, AudioClip> _musicClips = new();
        private readonly Dictionary<string, AudioClip> _sfxClips = new();
        private readonly Dictionary<UISoundType, AudioClip> _uiSounds = new();

        // Settings
        private const string PREF_MUSIC_ENABLED = "MusicEnabled";
        private const string PREF_SFX_ENABLED = "SfxEnabled";
        private const string PREF_MUSIC_VOLUME = "MusicVolume";
        private const string PREF_SFX_VOLUME = "SfxVolume";

        private bool _isMusicEnabled = true;
        private bool _isSfxEnabled = true;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;

        private string _currentMusicTrack;

        public bool IsMusicEnabled => _isMusicEnabled;
        public bool IsSfxEnabled => _isSfxEnabled;
        public float MusicVolume => _musicVolume;
        public float SfxVolume => _sfxVolume;

        public event Action<string> OnMusicChanged;

        [Inject]
        public AudioService(IGRollLogger logger)
        {
            _logger = logger;
            LoadSettings();
        }

        #region Initialization

        /// <summary>
        /// Set audio sources from Unity scene
        /// </summary>
        public void SetAudioSources(AudioSource musicSource, AudioSource sfxSource)
        {
            _musicSource = musicSource;
            _sfxSource = sfxSource;

            if (_musicSource != null)
            {
                _musicSource.volume = _isMusicEnabled ? _musicVolume : 0f;
                _musicSource.loop = true;
            }

            if (_sfxSource != null)
            {
                _sfxSource.volume = _sfxVolume;
            }

            _logger.Log("[AudioService] Audio sources configured");
        }

        /// <summary>
        /// Register music tracks
        /// </summary>
        public void RegisterMusicTrack(string trackId, AudioClip clip)
        {
            _musicClips[trackId] = clip;
        }

        /// <summary>
        /// Register sound effects
        /// </summary>
        public void RegisterSfx(string sfxId, AudioClip clip)
        {
            _sfxClips[sfxId] = clip;
        }

        /// <summary>
        /// Register UI sounds
        /// </summary>
        public void RegisterUISound(UISoundType type, AudioClip clip)
        {
            _uiSounds[type] = clip;
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            _isMusicEnabled = PlayerPrefs.GetInt(PREF_MUSIC_ENABLED, 1) == 1;
            _isSfxEnabled = PlayerPrefs.GetInt(PREF_SFX_ENABLED, 1) == 1;
            _musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, 1f);
            _sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(PREF_MUSIC_ENABLED, _isMusicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PREF_SFX_ENABLED, _isSfxEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, _musicVolume);
            PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _sfxVolume);
            PlayerPrefs.Save();
        }

        public void SetMusicEnabled(bool enabled)
        {
            _isMusicEnabled = enabled;
            SaveSettings();

            if (_musicSource != null)
            {
                _musicSource.volume = enabled ? _musicVolume : 0f;
            }

            _logger.Log($"[AudioService] Music enabled: {enabled}");
        }

        public void SetSfxEnabled(bool enabled)
        {
            _isSfxEnabled = enabled;
            SaveSettings();
            _logger.Log($"[AudioService] SFX enabled: {enabled}");
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            SaveSettings();

            if (_musicSource != null && _isMusicEnabled)
            {
                _musicSource.volume = _musicVolume;
            }
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            SaveSettings();

            if (_sfxSource != null)
            {
                _sfxSource.volume = _sfxVolume;
            }
        }

        #endregion

        #region Music

        public async UniTask PlayMusicAsync(string trackId, float fadeIn = 0.5f)
        {
            if (_musicSource == null)
            {
                _logger.LogWarning("[AudioService] Music source not set");
                return;
            }

            if (!_musicClips.TryGetValue(trackId, out var clip))
            {
                _logger.LogWarning($"[AudioService] Music track not found: {trackId}");
                return;
            }

            if (_currentMusicTrack == trackId && _musicSource.isPlaying)
            {
                return; // Already playing this track
            }

            // Fade out current music
            if (_musicSource.isPlaying && fadeIn > 0)
            {
                await FadeOutAsync(_musicSource, fadeIn / 2);
            }

            // Start new track
            _musicSource.clip = clip;
            _musicSource.Play();
            _currentMusicTrack = trackId;

            // Fade in
            if (fadeIn > 0 && _isMusicEnabled)
            {
                await FadeInAsync(_musicSource, _musicVolume, fadeIn / 2);
            }
            else
            {
                _musicSource.volume = _isMusicEnabled ? _musicVolume : 0f;
            }

            OnMusicChanged?.Invoke(trackId);
            _logger.Log($"[AudioService] Playing music: {trackId}");
        }

        public async UniTask StopMusicAsync(float fadeOut = 0.5f)
        {
            if (_musicSource == null || !_musicSource.isPlaying)
                return;

            if (fadeOut > 0)
            {
                await FadeOutAsync(_musicSource, fadeOut);
            }

            _musicSource.Stop();
            _currentMusicTrack = null;
            _logger.Log("[AudioService] Music stopped");
        }

        public void PauseMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Pause();
            }
        }

        public void ResumeMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.UnPause();
            }
        }

        private async UniTask FadeOutAsync(AudioSource source, float duration)
        {
            var startVolume = source.volume;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                await UniTask.Yield();
            }

            source.volume = 0f;
        }

        private async UniTask FadeInAsync(AudioSource source, float targetVolume, float duration)
        {
            source.volume = 0f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                await UniTask.Yield();
            }

            source.volume = targetVolume;
        }

        #endregion

        #region Sound Effects

        public void PlaySfx(string sfxId)
        {
            if (!_isSfxEnabled) return;

            if (_sfxSource == null)
            {
                _logger.LogWarning("[AudioService] SFX source not set");
                return;
            }

            if (!_sfxClips.TryGetValue(sfxId, out var clip))
            {
                _logger.LogWarning($"[AudioService] SFX not found: {sfxId}");
                return;
            }

            _sfxSource.PlayOneShot(clip, _sfxVolume);
        }

        public void PlaySfxAtPosition(string sfxId, Vector3 position)
        {
            if (!_isSfxEnabled) return;

            if (!_sfxClips.TryGetValue(sfxId, out var clip))
            {
                _logger.LogWarning($"[AudioService] SFX not found: {sfxId}");
                return;
            }

            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume);
        }

        public void PlayUISound(UISoundType type)
        {
            if (!_isSfxEnabled) return;

            if (_sfxSource == null) return;

            if (_uiSounds.TryGetValue(type, out var clip))
            {
                _sfxSource.PlayOneShot(clip, _sfxVolume);
            }
        }

        #endregion
    }
}
