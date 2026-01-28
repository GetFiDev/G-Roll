using GRoll.Core.Interfaces.Infrastructure;
using GRoll.Core.Interfaces.Services;
using UnityEngine;
using VContainer;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace GRoll.Infrastructure.Services
{
    /// <summary>
    /// Haptic feedback service implementation.
    /// Replaces HapticManager.
    /// </summary>
    public class HapticService : IHapticService
    {
        private readonly IGRollLogger _logger;

        private const string PREF_HAPTIC_ENABLED = "HapticEnabled";
        private bool _isEnabled = true;

        public bool IsEnabled => _isEnabled;

        [Inject]
        public HapticService(IGRollLogger logger)
        {
            _logger = logger;
            LoadSettings();
        }

        private void LoadSettings()
        {
            _isEnabled = PlayerPrefs.GetInt(PREF_HAPTIC_ENABLED, 1) == 1;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(PREF_HAPTIC_ENABLED, _isEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            SaveSettings();
            _logger.Log($"[HapticService] Haptic enabled: {enabled}");
        }

        public void Light()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Light);
        }

        public void Medium()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Medium);
        }

        public void Heavy()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Heavy);
        }

        public void Success()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Success);
        }

        public void Warning()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Warning);
        }

        public void Error()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Error);
        }

        public void Selection()
        {
            if (!_isEnabled) return;
            TriggerHaptic(HapticType.Selection);
        }

        public void Custom(int durationMs, float intensity)
        {
            if (!_isEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                    if (vibrator != null)
                    {
                        // API 26+ için VibrationEffect kullan
                        var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        int amplitude = Mathf.RoundToInt(Mathf.Clamp01(intensity) * 255);
                        var vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", (long)durationMs, amplitude);
                        vibrator.Call("vibrate", vibrationEffect);
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning($"[HapticService] Custom vibration failed: {ex.Message}");
                // Fallback to standard vibration
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS doesn't support custom duration/intensity, use closest match
            if (intensity < 0.33f)
                TriggerHaptic(HapticType.Light);
            else if (intensity < 0.66f)
                TriggerHaptic(HapticType.Medium);
            else
                TriggerHaptic(HapticType.Heavy);
#endif
        }

        private void TriggerHaptic(HapticType type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            switch (type)
            {
                case HapticType.Light:
                    Device.SetNoBackupFlag("haptic");
                    // Use Taptic Engine if available
                    break;
                case HapticType.Medium:
                    break;
                case HapticType.Heavy:
                    Handheld.Vibrate();
                    break;
                case HapticType.Success:
                case HapticType.Warning:
                case HapticType.Error:
                    break;
                case HapticType.Selection:
                    break;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                    if (vibrator != null)
                    {
                        int duration = type switch
                        {
                            HapticType.Light => 10,
                            HapticType.Medium => 20,
                            HapticType.Heavy => 40,
                            HapticType.Success => 30,
                            HapticType.Warning => 25,
                            HapticType.Error => 50,
                            HapticType.Selection => 5,
                            _ => 15
                        };

                        vibrator.Call("vibrate", (long)duration);
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning($"[HapticService] Haptic failed: {ex.Message}");
            }
#endif
        }

        private enum HapticType
        {
            Light,
            Medium,
            Heavy,
            Success,
            Warning,
            Error,
            Selection
        }
    }
}
