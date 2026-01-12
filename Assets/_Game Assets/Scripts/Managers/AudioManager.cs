using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip pop;
    
    private AudioSource _bgMusic, _standard, _increasing;

    [Header("Audio Mixer"), SerializeField]
    private AudioMixer masterMixer;

    private AudioMixerGroup _musicGroup;
    private AudioMixerGroup _soundGroup;
    
    public AudioManager Initialize()
    {
        var mainCameraGameObject = Camera.main.gameObject;
        
        _bgMusic = mainCameraGameObject.AddComponent<AudioSource>();
        _standard = mainCameraGameObject.AddComponent<AudioSource>();
        _increasing = mainCameraGameObject.gameObject.AddComponent<AudioSource>();

        _musicGroup = masterMixer.FindMatchingGroups("Music")[0];
        _soundGroup = masterMixer.FindMatchingGroups("Sound Effects")[0];

        _bgMusic.outputAudioMixerGroup = _musicGroup;
        _standard.outputAudioMixerGroup = _soundGroup;
        _increasing.outputAudioMixerGroup = _soundGroup;

        // Subscribe to DataManager events for automatic updates
        DataManager.OnSoundStateChanged += OnSoundStateChanged;
        DataManager.OnMusicStateChanged += OnMusicStateChanged;

        UpdateAudioStates();
        
        return this;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        DataManager.OnSoundStateChanged -= OnSoundStateChanged;
        DataManager.OnMusicStateChanged -= OnMusicStateChanged;
    }

    private void OnSoundStateChanged(bool isOn)
    {
        SetSoundState(isOn);
    }

    private void OnMusicStateChanged(bool isOn)
    {
        SetMusicState(isOn);
    }

    public void UpdateAudioStates()
    {
        SetMusicState(DataManager.Music);
        SetSoundState(DataManager.Sound);
    }

    public void SetMusicState(bool state)
    {
        masterMixer.SetFloat("Music_Volume", state ? 0 : -80);
    }

    public void SetSoundState(bool state)
    {
        masterMixer.SetFloat("Sound_Volume", state ? 0 : -80);
    }

    public void PlayUIButtonClick()
    {
        Play(pop);
    }

    public void Play(AudioClip clip)
    {
        if (!DataManager.Sound)
            return;
        
        _standard.PlayOneShot(clip);
    }

    public void PlayWithPitch(AudioClip clip, float pitch)
    {
        if (!DataManager.Sound)
            return;
        
        _increasing.pitch = pitch;
        _increasing.PlayOneShot(clip);
    }
}