using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    public Action OnGameStateChanged;

    private GameState _gameState;

    [ShowInInspector, ReadOnly]
    public GameState GameState
    {
        get => _gameState;
        private set
        {
            _gameState = value;
            OnGameStateChanged?.Invoke();
        }
    }

    [Space(10)] [Header("Singleton Container")] [ReadOnly, Required]
    public TouchManager touchManager;

    [ReadOnly, Required] public AudioManager audioManager;
    [ReadOnly, Required] public CameraManager cameraManager;
    [ReadOnly, Required] public ObjectPoolingManager objectPoolingManager;

    protected override void Init()
    {
        base.Init();

        GameState = GameState.MetaState;

        Application.targetFrameRate = 120;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        HapticManager.SetHapticsActive(DataManager.Vibration);

        OnGameStateChanged += () => Debug.Log($"Game State is : {GameState}");

        objectPoolingManager = GetComponentInChildren<ObjectPoolingManager>(true).Initialize();
        cameraManager = GetComponentInChildren<CameraManager>(true).Initialize();
        touchManager = GetComponentInChildren<TouchManager>();
        audioManager = GetComponentInChildren<AudioManager>(true).Initialize(); //This depends on CameraManager
        
        UIManager.Instance.Initialize();
    }

    private void Start()
    {
        GameState = GameState.MetaState;
    }

    public void StartTheGameplay()
    {
        if (GameState != GameState.GameplayRun)
        {
            Debug.Log("Game State is not Ready", this);
            return;
        }

        GameState = GameState.GameplayLoading;

    }

    private GameState _previousGameState;
    
    public void PauseGame()
    {
        _previousGameState = GameState;
        
        GameState = GameState.Paused;
    }

    public void ResumeGame()
    {
        GameState = _previousGameState;
    }

    public void LevelFinish(bool isSuccess)
    {
        if (GameState != GameState.GameplayRun)
        {
            Debug.LogError("Game State is not GameplayRun", this);
            return;
        }

        GameState = isSuccess ? GameState.Complete : GameState.Fail;

        if (isSuccess)
        {
            DataManager.CurrentLevelIndex++;
        }
    }

    private void OnValidate()
    {
        touchManager = GetComponentInChildren<TouchManager>(true);
        audioManager = GetComponentInChildren<AudioManager>(true);
        cameraManager = GetComponentInChildren<CameraManager>(true);
        objectPoolingManager = GetComponentInChildren<ObjectPoolingManager>(true);
    }
}