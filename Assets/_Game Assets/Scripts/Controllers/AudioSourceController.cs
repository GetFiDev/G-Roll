using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioSourceController : MonoBehaviour
{
    private AudioSource _audioSource;

    private float _defaultVolume;

    private void OnEnable()
    {
        _audioSource ??= GetComponent<AudioSource>();
        _defaultVolume = _audioSource.volume;

        // Subscribe to sound state changes
        DataManager.OnSoundStateChanged += OnSoundStateChanged;

        _audioSource.Play();
        _audioSource.volume = DataManager.Sound ? _defaultVolume : 0f;
    }

    private void OnDisable()
    {
        // Unsubscribe from sound state changes
        DataManager.OnSoundStateChanged -= OnSoundStateChanged;
        _audioSource.Stop();
    }

    private void OnSoundStateChanged(bool isOn)
    {
        if (_audioSource != null)
        {
            _audioSource.volume = isOn ? _defaultVolume : 0f;
        }
    }
}