using System.Collections;
using UnityEngine;
using System;
// Re-adding correct usings if any were lost or just ensuring file start match


public class GameplayManager : MonoBehaviour
{
    [Header("Channels (Assign in Inspector)")]
    [SerializeField] private VoidEventChannelSO requestStartGameplay;
    [SerializeField] private VoidEventChannelSO requestReturnToMeta;
    [SerializeField] private PhaseEventChannelSO phaseChanged;

    [Header("Services")]
    [SerializeField] private MonoBehaviour playerSpawnerBehaviour; // must implement IPlayerSpawner
    [SerializeField] private MonoBehaviour mapLoaderBehaviour;     // must implement IMapLoader

    // GameplayManager.cs içinde:
    [SerializeField] private GameplayLogicApplier logicApplier;
    [SerializeField] private GameplayVisualApplier visualApplier;

    [Header("Camera")]
    [SerializeField] private Transform targetCamera; // boşsa Camera.main kullanılır
    [SerializeField] private TouchManager gameplayTouch;


    private IPlayerSpawner playerSpawner;
    private IMapLoader mapLoader;

    // Runtime
    private bool hasControl;              // Orkestra yetkisi bende mi?
    private GameplayStats stats;
    private GameObject playerGO;
    private bool sessionActive;
    private float sessionStartTime;
    private string _currentSessionId = null; // server-granted session id
    private double _sessionCurrencyTotal = 0d; // this session's earned currency (coins value after bonuses)
    private double _initialMaxScore = 0d;
    // --- Cached finals coming from PlayerStatHandler (per session) ---
    private int   _comboPowerFactor = 1;     // integer factor, base 1
    private int   _coinBonusPercent = 0;     // integer percent, base 0
    private int   _magnetBonusPercent = 0;   // integer percent, base 0
    private int   _gameplaySpeedPct = 0;     // integer percent, base 0 (for reference)
    private float _playerAccelPer60 = 0.1f;  // float per minute, base 0.1
    private float _playerSpeedAdd   = 0f;    // float additive speed
    private int   _playerSizePct    = 0;     // integer percent

    public static GameplayManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this.gameObject); }
        ;

        playerSpawner = playerSpawnerBehaviour as IPlayerSpawner;
        mapLoader = mapLoaderBehaviour as IMapLoader;
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;
    }

    private void OnEnable()
    {
        requestStartGameplay.OnEvent += BeginSessionRequested;
        phaseChanged.OnEvent += OnPhaseChanged;
    }

    private void OnDisable()
    {
        requestStartGameplay.OnEvent -= BeginSessionRequested;
        phaseChanged.OnEvent -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        // Faz GamePlay değilse kontrol bende olmamalı
        hasControl = (phase == GamePhase.Gameplay);
        if (!hasControl) TearDownIfAny();
    }

    private void BeginSessionRequested()
    {
        // Faz değişimini GameManager yapacak; ben sadece hazırlığa odaklanıyorum.
        // Metin UI "Play" butonu bu kanalı raise edince GameManager fazı Gameplay yapar,
        // OnPhaseChanged ile hasControl=true olur ve akış başlar.
        StartCoroutine(BeginSessionWhenInGameplay());
    }

    /// <summary>
    /// GameManager sunucudan sessionId aldığında çağırır. Faz zaten Gameplay'e alınmış olmalı.
    /// </summary>
    public GameMode CurrentMode { get; private set; } = GameMode.Endless;

    /// <summary>
    /// GameManager sunucudan sessionId aldığında çağırır. Faz zaten Gameplay'e alınmış olmalı.
    /// </summary>
    public void BeginSessionWithServerGrant(string sessionId, GameMode mode)
    {
        _currentSessionId = sessionId;
        CurrentMode = mode;
        StartCoroutine(BeginSessionWhenInGameplay());
    }



    private IEnumerator BeginSessionWhenInGameplay()
    {
        // Faz GamePlay olana kadar bekle (GameManager set edecek)
        // Faz GamePlay olana kadar bekle (GameManager set edecek)
        while (!hasControl) yield return null;
        
        // Initial Loading UI is now handled by GameManager BEFORE switching phase.
        // We focus on logic setup here.

        // Capture initial high score for comparison later
        _initialMaxScore = 0;
        if (UserDatabaseManager.Instance != null)
        {
            var task = UserDatabaseManager.Instance.LoadUserData();
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.Result != null)
            {
                _initialMaxScore = task.Result.maxScore;
            }
        }

        // Hazırlık
        mapLoader?.Load();
        yield return new WaitUntil(() => mapLoader != null && mapLoader.IsReady);
        playerGO = playerSpawner?.Spawn();
        var playerCtrl = playerGO ? playerGO.GetComponent<PlayerController>() : null;
        var playerMov  = playerGO ? playerGO.GetComponent<PlayerMovement>()  : null;
        if (playerMov != null && logicApplier != null)
        {
            playerMov.BindToGameplay(logicApplier, gameplayTouch);
            gameplayTouch?.BindPlayer(playerMov);
            // güvence: sahne başında donuk kaldıysa açalım
            playerMov._isFrozen = false;

            // --- Clean session baseline: reset multipliers to 1 before applying stats ---
            logicApplier.SetGameplaySpeedMultiplier(1f);
            logicApplier.SetPlayerSpeedMultiplier(1f);
        }

        // Logic'i hazırla ve Visual'ı bağla, ardından koşuyu başlat
        if (logicApplier != null)
        {
            var cam = targetCamera != null ? targetCamera : (Camera.main != null ? Camera.main.transform : null);
            logicApplier.InitializeSession(cam, playerCtrl);
        }
        if (visualApplier != null && logicApplier != null)
        {
            visualApplier.Bind(logicApplier);
            visualApplier?.SetCoinFXAnchor(playerGO.transform);

            // optional: listen submit feedback for logs
            logicApplier.OnSessionResultSubmitted -= OnRemoteSubmitOk;
            logicApplier.OnSessionResultSubmitted += OnRemoteSubmitOk;
            logicApplier.OnSessionResultFailed    -= OnRemoteSubmitFail;
            logicApplier.OnSessionResultFailed    += OnRemoteSubmitFail;
        }

        // TERTİP: Tüm yerleştirme/initialize bittikten SONRA stat'ları uygula (trash-in, trash-out değil; emir sırası doğru)
        PlayerStatsRemoteService.Instance?.ApplyToPlayer(playerGO, logicApplier);

        var statComp = playerGO ? playerGO.GetComponent<PlayerStatHandler>() : null;
        if (statComp != null)
        {
            _comboPowerFactor = statComp.FinalComboPowerFactor;             // int
            _coinBonusPercent = statComp.FinalCoinMultiplierPercent;        // int
            _magnetBonusPercent = statComp.FinalMagnetPowerPct;             // int
            _gameplaySpeedPct = statComp.FinalGameplaySpeedPct;             // int (bilgi amaçlı)
            _playerAccelPer60 = statComp.FinalPlayerAcceleration;           // float
            _playerSpeedAdd   = statComp.FinalPlayerSpeedAdd;               // float
            _playerSizePct    = statComp.FinalPlayerSizePct;                // int

            var magnet = playerGO.GetComponentInChildren<PlayerMagnet>(true);
            if (magnet != null)
            {
                magnet.Initialize(playerGO.transform);
                magnet.ApplySizeAndMagnet(_magnetBonusPercent);
            }
        }
        else
        {
            _comboPowerFactor = 1;
            _coinBonusPercent = 0;
            _magnetBonusPercent = 0;
            _gameplaySpeedPct = 0;
            _playerAccelPer60 = 0.1f;
            _playerSpeedAdd   = 0f;
            _playerSizePct    = 0;
        }

        // Combo sistemi: run başlarken deterministik temiz başla ve combo gücünü uygula
        if (logicApplier != null)
        {
            logicApplier.ResetCombo();
        }

        _sessionCurrencyTotal = 0d; // reset session currency accumulator

        logicApplier?.StartRun();

        // Session runtime state
        stats = new GameplayStats { playtimeSeconds = 0f };
        sessionStartTime = Time.time;
        sessionActive = true;

        // Bundan sonrası açık uç: bitişi oyuncu hamleleri belirleyecek.
        // Dışarıdan EndSession(...) çağrıldığında kapanış yapılacak.
        yield break;
    }

    private void HandleLevelEndCompleted()
    {
        // Unsubscribe (safety against duplicate calls)
        if (UIManager.Instance != null && UIManager.Instance.levelEnd != null)
            UIManager.Instance.levelEnd.OnSequenceCompleted -= HandleLevelEndCompleted;

        EndSession(false);
    }

    /// <summary>
    /// Standart FAIL akışı: LevelEnd UI sekansını başlat. Sekans bittiğinde EndSession(false) çağrılır.
    /// </summary>
    public void StartFailFlow()
    {
        if (!sessionActive) return;

        // 1) Kamerayı ve koşuyu anında durdur
        logicApplier?.StopRun();

        // 2) Level End sekansını göster; bitince teardown (EndSession) zaten çalışacak
        if (UIManager.Instance != null && UIManager.Instance.levelEnd != null)
        {
            UIManager.Instance.levelEnd.OnSequenceCompleted -= HandleLevelEndCompleted;
            UIManager.Instance.levelEnd.OnSequenceCompleted += HandleLevelEndCompleted;
        }
        
        var score = logicApplier != null ? logicApplier.Score : 0f;
        var coins = logicApplier != null ? logicApplier.Coins : 0f;
        UIManager.Instance.levelEnd?.SetResultValues(score,coins);
        UIManager.Instance?.ShowLevelEnd(false);
    }

    /// <summary>
    /// Gameplay bitişini dışarıdan tetiklemek için çağır.
    /// success: level kazanıldı mı?
    /// </summary>
    public async void EndSession(bool success)
    {
        if (!sessionActive) return;
        sessionActive = false;

        // Süreyi hesapla ve istatistikleri doldur
        if (stats == null) stats = new GameplayStats();
        stats.levelSuccess = success;
        stats.playtimeSeconds = Mathf.Max(0f, Time.time - sessionStartTime);

        // Remote reporting (awaiting server response)
        var earnedScore = logicApplier != null ? (double)logicApplier.Score : 0d;
        var earnedCurrency = _sessionCurrencyTotal;
        
        Debug.Log($"[GameplayManager] EndSession. EarnedScore: {earnedScore}, InitialMaxScore: {_initialMaxScore}");

        // Stop updates immediately so user doesn't see game continuing while we wait
        visualApplier?.Unbind();
        logicApplier?.StopRun();
        
        // Show loading or wait? The user said "SubmitSessionResult" function tamamlanmadan dönme.
        // Usually we might want a spinner, but blocking return to meta is the key.
        
        await SubmitSessionToServer(earnedCurrency, earnedScore, success);

        // Check for New High Score AFTER server submission (just to be safe, though local check is fine)
        if (earnedScore > _initialMaxScore)
        {
            Debug.Log($"[GameplayManager] New High Score! {earnedScore} > {_initialMaxScore}");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNewHighScore(earnedScore, () => 
                {
                    // Fazı Meta'ya geri aldır (Panel kapandıktan sonra)
                    requestReturnToMeta.Raise();
                });
            }
            else
            {
                 requestReturnToMeta.Raise();
            }
        }
        else
        {
            Debug.Log($"[GameplayManager] No New High Score. {earnedScore} <= {_initialMaxScore}");
            // Fazı Meta'ya geri aldır
            requestReturnToMeta.Raise();
        }

        // Cleanup
        var statEnd = playerGO ? playerGO.GetComponent<PlayerStatHandler>() : null;
        statEnd?.OnRunEnd();

        if (logicApplier != null)
        {
            logicApplier.OnSessionResultSubmitted -= OnRemoteSubmitOk;
            logicApplier.OnSessionResultFailed    -= OnRemoteSubmitFail;
        }

        gameplayTouch?.UnbindPlayer();
        logicApplier?.ResetCombo();
        logicApplier?.SetGameplaySpeedMultiplier(1f);
        logicApplier?.SetPlayerSpeedMultiplier(1f);
        mapLoader?.Unload();
        playerSpawner?.DespawnAll();
        if (playerGO != null) { Destroy(playerGO); playerGO = null; }

        _coinBonusPercent = 0;
        _comboPowerFactor = 1;
        _magnetBonusPercent = 0;
        _gameplaySpeedPct = 0;
        _playerAccelPer60 = 0.1f;
        _playerSpeedAdd   = 0f;
        _playerSizePct    = 0;

        stats = null;
    }

    private async System.Threading.Tasks.Task SubmitSessionToServer(double earnedCurrency, double earnedScore, bool success)
    {
        if (logicApplier == null) return;
        if (string.IsNullOrEmpty(_currentSessionId))
        {
            Debug.LogWarning("[GameplayManager] No sessionId set; skipping submit.");
            return;
        }
        try
        {
            // Collect extended session metrics for visibility
            double maxComboInSession = 0;
            int playtimeSec = 0;
            int powerUpsCollectedInSession = 0;

            if (logicApplier != null)
            {
                maxComboInSession = logicApplier.GetMaxComboInSession();
                playtimeSec = logicApplier.GetPlaytimeSec();
                powerUpsCollectedInSession = logicApplier.GetPowerUpsCollectedInSession();
                Debug.Log($"[GameplayManager] Submit extras: combo={maxComboInSession:F2} playtimeSec={playtimeSec} powerUps={powerUpsCollectedInSession}");
            }

            // Use PERMANENT UserDatabaseManager submission instead of logicApplier
            if (UserDatabaseManager.Instance != null)
            {
                await UserDatabaseManager.Instance.SubmitGameplaySessionAsync(_currentSessionId, earnedCurrency, earnedScore, maxComboInSession, playtimeSec, powerUpsCollectedInSession, CurrentMode, success);
            }
            else
            {
                 Debug.LogError("[GameplayManager] UserDatabaseManager missing! Cannot submit session.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameplayManager] SubmitSessionToServer failed: {ex.Message}");
        }
        finally
        {
            _currentSessionId = null; // consume once
        }
    }

    private void OnRemoteSubmitOk(bool alreadyProcessed, double totalCurrency, double maxScore)
    {
        Debug.Log($"[GameplayManager] Session submit ok. alreadyProcessed={alreadyProcessed} currency={totalCurrency} maxScore={maxScore}");
    }
    private void OnRemoteSubmitFail(string msg)
    {
        Debug.LogWarning($"[GameplayManager] Session submit failed: {msg}");
    }

    // --- restored missing methods ---

    private void TearDownIfAny()
    {
        // Internal cleanup helper
        if (sessionActive)
        {
            EndSession(false);
        }
    }

    public void AddCoins(double amount, Vector3? worldPos = null, int fxCount = 1)
    {
        if (logicApplier != null)
        {
            // Delegate completely to logic applier (which fires events for visuals)
            logicApplier.AddCoins((float)amount, worldPos, fxCount);
            
            _sessionCurrencyTotal += amount;
        }
    }
    // Overload for simple usage
    public void AddCoins(float amount) => AddCoins((double)amount);


    public void ApplyGameplaySpeedPercent(float pct)
    {
        // logicApplier expects delta (e.g. 0.25 for +25%)
        if (logicApplier != null)
        {
            logicApplier.ApplyGameplaySpeedPercent(pct);
        }
    }

    public void ApplyPlayerSpeedPercent(float pct)
    {
        if (logicApplier != null)
        {
             logicApplier.ApplyPlayerSpeedPercent(pct);
        }
    }

    public void InstantlyFillTheBooster()
    {
         // Delegate to logicApplier if it has a booster concept, 
         // or if this is about the player's special ability bar.
         // Assuming logicApplier has a method for this, otherwise we might need to access PlayerController directly.
         if(logicApplier != null) 
         {
             logicApplier.FillBoosterToMax();
         }
    }

}