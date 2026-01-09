using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class BackgroundMusicManager : MonoBehaviour
{
    public static BackgroundMusicManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private List<AudioClip> musicList = new List<AudioClip>();
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("State")]
    [SerializeField, Sirenix.OdinInspector.ReadOnly] 
    private int _currentIndex = 0;

    private AudioSource _audioSource;
    private const string MusicIndexKey = "BackgroundMusicIndex";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Initialize();
    }

    private void Initialize()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.outputAudioMixerGroup = outputGroup;
        _audioSource.loop = false; // We handle looping manually to go to next track
        _audioSource.playOnAwake = false;

        // Load saved index
        _currentIndex = PlayerPrefs.GetInt(MusicIndexKey, 0);
        if (_currentIndex >= musicList.Count) _currentIndex = 0;
    }

    private void Start()
    {
        // Subscribe to events
        DataManager.OnMusicStateChanged += OnMusicStateChanged;
        
        // Initial Play
        PlayTrack(_currentIndex);
    }

    private void OnDestroy()
    {
        DataManager.OnMusicStateChanged -= OnMusicStateChanged;
    }

    private void Update()
    {
        // Only progress playlist if music is enabled and clip has finished
        if (DataManager.Music)
        {
            // Check if audio has stopped playing (and it's not because we paused it manually, 
            // though DataManager.Music check covers the manual pause case essentially)
            if (!_audioSource.isPlaying)
            {
               PlayNext();
            }
        }
    }

    private void PlayTrack(int index)
    {
        if (musicList.Count == 0) return;

        // Wrap index just in case
        if (index >= musicList.Count) index = 0;
        _currentIndex = index;

        // Save immediately
        PlayerPrefs.SetInt(MusicIndexKey, _currentIndex);
        PlayerPrefs.Save();

        var clip = musicList[_currentIndex];
        
        if (clip == null)
        {
            PlayNext(); // Skip null
            return;
        }

        _audioSource.clip = clip;

        if (DataManager.Music)
        {
            _audioSource.Play();
        }
        else
        {
            _audioSource.Stop(); 
        }
    }

    private void PlayNext()
    {
        _currentIndex++;
        if (_currentIndex >= musicList.Count)
        {
            _currentIndex = 0;
        }
        PlayTrack(_currentIndex);
    }

    private void OnMusicStateChanged(bool isOn)
    {
        if (isOn)
        {
            if (!_audioSource.isPlaying)
            {
                _audioSource.Play(); 
            }
            else
            {
                _audioSource.UnPause();
            }
        }
        else
        {
            _audioSource.Pause();
        }
    }
}
