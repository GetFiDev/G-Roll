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
    
    // Track the paused state properly
    private bool _wasPaused = false;

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
        
        // Initial Play - always set up the track
        SetupTrack(_currentIndex);
        
        // Only play if music is enabled
        if (DataManager.Music)
        {
            _audioSource.Play();
            _wasPaused = false;
        }
        else
        {
            _wasPaused = true;
        }
    }

    private void OnDestroy()
    {
        DataManager.OnMusicStateChanged -= OnMusicStateChanged;
    }

    private void Update()
    {
        // Only progress playlist if music is enabled and clip has finished naturally
        if (DataManager.Music && !_wasPaused)
        {
            // Check if audio has stopped playing naturally (track ended)
            if (!_audioSource.isPlaying && _audioSource.clip != null && _audioSource.time == 0f)
            {
               PlayNext();
            }
        }
    }

    private void SetupTrack(int index)
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
            // Skip null and try next
            _currentIndex++;
            if (_currentIndex >= musicList.Count) _currentIndex = 0;
            SetupTrack(_currentIndex);
            return;
        }

        _audioSource.clip = clip;
    }

    private void PlayNext()
    {
        _currentIndex++;
        if (_currentIndex >= musicList.Count)
        {
            _currentIndex = 0;
        }
        SetupTrack(_currentIndex);
        
        if (DataManager.Music)
        {
            _audioSource.Play();
        }
    }

    private void OnMusicStateChanged(bool isOn)
    {
        Debug.Log($"[BackgroundMusicManager] OnMusicStateChanged called. isOn={isOn}, audioSource.time={_audioSource.time}");
        
        if (isOn)
        {
            _wasPaused = false;
            
            // If audio was paused, unpause it
            // If audio was never started or stopped, play from beginning
            if (_audioSource.time > 0f)
            {
                // Was paused mid-track, resume
                _audioSource.UnPause();
                Debug.Log("[BackgroundMusicManager] UnPaused music");
            }
            else
            {
                // Track hasn't started or ended, play from start
                _audioSource.Play();
                Debug.Log("[BackgroundMusicManager] Started music from beginning");
            }
        }
        else
        {
            _wasPaused = true;
            _audioSource.Pause();
            Debug.Log("[BackgroundMusicManager] Paused music");
        }
    }
}

