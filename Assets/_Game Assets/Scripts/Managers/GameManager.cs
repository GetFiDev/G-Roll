using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    /// <summary>GameState değiştiğinde ateşlenir.</summary>
    public Action OnGameStateChanged;

    /// <summary>Level bittiğinde (Complete/Fail) ateşlenir. Parametre: isSuccess.</summary>
    public Action<bool> OnLevelFinished;

    private GameState _gameState;
    public GameplayManager gameplayManager;

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
        if (GameState != GameState.MetaState)
        {
            Debug.Log("You're not in the meta scene", this);
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

    public void EnterGameplayRun()
    {
        GameState = GameState.GameplayRun;
        OnGameStateChanged?.Invoke(); // (setter zaten çağırıyor ama mevcut mimarini bozmayayım)
        gameplayManager.StartRun();
    }

    /// <summary>
    /// Level başarıyla tamamlandı. (Örn: GameplayManager.StopRun() sonrasında çağrılır)
    /// </summary>
    public void EnterLevelComplete()
    {
        LevelFinish(false);
    }

    public void LevelFinish(bool isSuccess)
    {
        if (GameState != GameState.GameplayRun)
        {
            Debug.LogError("Game State is not GameplayRun", this);
            return;
        }

        GameState = isSuccess ? GameState.Complete : GameState.Fail;

        // başarıda level index ilerlet
        if (isSuccess)
        {
            DataManager.CurrentLevelIndex++;
        }

        // event: UI/analitik bağlamak istersen
        OnLevelFinished?.Invoke(isSuccess);
    }

    private void OnValidate()
    {
        touchManager = GetComponentInChildren<TouchManager>(true);
        audioManager = GetComponentInChildren<AudioManager>(true);
        objectPoolingManager = GetComponentInChildren<ObjectPoolingManager>(true);
    }
}