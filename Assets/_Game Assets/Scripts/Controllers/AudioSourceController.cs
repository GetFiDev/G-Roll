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

        _audioSource.Play();
        _audioSource.volume = DataManager.Sound ? _defaultVolume : 0f;
    }

    private void OnDisable()
    {
        _audioSource.Stop();
    }
}