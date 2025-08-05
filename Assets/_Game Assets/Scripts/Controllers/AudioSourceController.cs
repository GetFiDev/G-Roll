using System;
using System.Collections;
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

        DataManager.OnSoundStateChanged += OnSoundSettingsChanged;

        _audioSource.Play();
        _audioSource.volume = DataManager.Sound ? _defaultVolume : 0f;
    }

    private void OnDisable()
    {
        DataManager.OnSoundStateChanged -= OnSoundSettingsChanged;

        _audioSource.Stop();
    }

    private Coroutine _activeCoroutine;
    private void OnSoundSettingsChanged(bool isSound)
    {
        if (_activeCoroutine is not null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        _activeCoroutine = StartCoroutine(DoVolume(isSound ? _defaultVolume : 0));
    }

    private IEnumerator DoVolume(float targetLevel)
    {
        while (Math.Abs(_audioSource.volume - targetLevel) > .0001f)
        {
            yield return null;

            _audioSource.volume = Mathf.Lerp(_audioSource.volume, targetLevel, 10 * Time.deltaTime);
        }
    }
}