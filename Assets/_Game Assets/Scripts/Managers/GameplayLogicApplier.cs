using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// Mantık: hız, skor, coin/booster hesapları, booster süreci
public class GameplayLogicApplier : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float startSpeed = 5f;
    [SerializeField] private float maxSpeed   = 20f;
    [SerializeField] private float accelerationPerSecond = 0.75f;

    [Header("Start Mode")]
    [SerializeField, Tooltip("Kamera hareketi ilk oyuncu hamlesine kadar başlamasın.")]
    private bool startOnFirstPlayerMove = true;

    // runtime flags for deferred start
    private bool armedForFirstMove = false;

    [Header("Booster")]
    [SerializeField] private float boosterFillPerCollectedCoin = 1f;
    [SerializeField] private float boosterFillMin = 0f;
    [SerializeField] private float boosterFillMax = 100f;
    [SerializeField] private float boosterDurationSeconds = 5f;
    [SerializeField] private float boosterSpeedMultiplier = 1.35f;
    [SerializeField] private float boosterMagnetMultiplier = 50f;

    // ... (existing code) ...


    [Header("Speed Multipliers (percent-based)")]
    [SerializeField, Tooltip("Gameplay(camera) speed multiplier. 1 = 100% (default).")]
    private float gameplaySpeedMultiplier = 1f;
    [SerializeField, Tooltip("Player (ball) movement speed multiplier. 1 = 100% (default).")]
    private float playerSpeedMultiplier = 1f;

    // Expose as read-only
    public float GameplaySpeedMultiplier => gameplaySpeedMultiplier;
    public float PlayerSpeedMultiplier   => playerSpeedMultiplier;

    // Optional: notify listeners (e.g., PlayerController) for external application
    public event Action<float> OnGameplaySpeedMultiplierChanged; // new value
    public event Action<float> OnPlayerSpeedMultiplierChanged;   // new value

    [Header("Optional Player Ref (for booster flag)")]
    [SerializeField] private PlayerController player; // isteğe bağlı; yoksa sadece event yayınlanır
    private bool _spawnCaptured = false;
    private Vector3 _startCamPosition;
    private Quaternion _startCamRotation;

    // Runtime state
    // ---- Session Tracking (for server submit) ----
    private float _sessionStartTime;
    private float _sessionMaxComboMultiplier = 1f;
    private int _sessionPowerUpsCollected = 0;
    // Effective camera speed after multiplier
    public float CurrentSpeed { get; private set; }
    // Internal accelerating base (before multiplier)
    private float baseSpeed;
    public float Score        { get; private set; }
    public float Coins        { get; private set; } // Currency total (uses incoming delta per coin)
    public float BoosterFill  { get; private set; }
    public bool  BoosterActive { get; private set; }

    // Bağlam
    private Transform targetCamera;
    private bool isRunning;
    private float lastCamZ;
    private Coroutine boosterRoutine;

    // Olaylar (Visual Applier buraya bağlanır)
    public event Action OnGameReady; // Triggers when session is theoretically ready (camera transition)
    public event Action OnRunStarted; // Triggers when physics/gameplay actually starts
    public event Action OnRunStopped;
    public event Action<float> OnScoreChanged;                  // yeni Score
    public event Action<float,float> OnCoinsChanged;            // total, delta
    public event Action<float,float,float> OnBoosterChanged;    // fill, min, max
    public event Action<bool> OnBoosterStateChanged;            // active?
    public event Action<Vector3,int> OnCoinPickupFXRequest;     // worldPos, count (genelde 1)
    public event Action OnBoosterUsed;
    public event Action<int> OnPowerUpCollectedCountChanged; // emits total collected in this run

    // Session submit callbacks (remote)
    public event Action<bool, double, double> OnSessionResultSubmitted; // (alreadyProcessed, currencyTotal, maxScore)
    public event Action<string> OnSessionResultFailed;                  // error message

    public event Action<int> OnComboMultiplierChanged; // emits current Combo Power (reused event name but sends power value)
    public event Action OnComboReset;                    // fires when combo is reset to baseline
    
        // --- Combo Score System ---
    [SerializeField] private float scorePerSecond = 0f;    // Legacy unused, overridden by ComboPower per minute logic
    [SerializeField] private float comboTimeout = 10f;     // coin toplanmazsa şu sürede sıfırla (sn)

    public int CurrentComboPower { get; private set; }
    private int _baseComboPower = 25; // Default base if not set via Stats

    private float timeSinceLastCoin = 0f;

    /// <summary>
    /// Reset both gameplay & player speed multipliers to their defaults (1x) and refresh CurrentSpeed.
    /// Deterministic session baseline; call before applying run-start stats.
    /// </summary>
    public void ResetMultipliersToDefault()
    {
        gameplaySpeedMultiplier = 1f;
        playerSpeedMultiplier = 1f;
        CurrentSpeed = baseSpeed * gameplaySpeedMultiplier;
        OnGameplaySpeedMultiplierChanged?.Invoke(gameplaySpeedMultiplier);
        OnPlayerSpeedMultiplierChanged?.Invoke(playerSpeedMultiplier);
    }

    void Awake()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;
        if (targetCamera != null && !_spawnCaptured) {
            _startCamPosition = targetCamera.position;
            _startCamRotation = targetCamera.rotation;
            _spawnCaptured = true;
        }
    }

    // ---- Public control API (GameplayManager buradan çağırır) ----
    public void InitializeSession(Transform cameraTransform, PlayerController optionalPlayer = null)
    {
        targetCamera = cameraTransform;
        if (optionalPlayer) player = optionalPlayer;

        armedForFirstMove = startOnFirstPlayerMove;
        ResetMultipliersToDefault();

        ResetSessionValues(hardResetCameraSnapshot: true);
        ResetSessionTrackers();

        ResetCombo(); // also notifies UI via events
    }

    public void StartRun()
    {
        if (targetCamera == null) return;

        if (_spawnCaptured && targetCamera != null)
            targetCamera.SetPositionAndRotation(_startCamPosition, _startCamRotation);

        // Prepare base speed/effective speed
        baseSpeed   = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        CurrentSpeed = baseSpeed * Mathf.Max(0f, gameplaySpeedMultiplier);
        lastCamZ     = targetCamera.position.z;

        // NEW: Signal that game is ready (Transition can start)
        OnGameReady?.Invoke();

        if (armedForFirstMove)
        {
            isRunning = false;
        }
        else
        {
            isRunning = true;
            _sessionStartTime = Time.time; // stamp the exact run start moment
            OnRunStarted?.Invoke();
        }
    }

    /// <summary>
    /// Oyuncu ilk kez bir hareket girişi yaptığında çağrılmalı.
    /// PlayerMovement.BindToGameplay(...) ile referans aldığı bu nesneye,
    /// ilk swipe/jump vb. gerçekleştiğinde haber verir.
    /// </summary>
    public void NotifyFirstPlayerMove()
    {
        if (!armedForFirstMove || isRunning || targetCamera == null)
            return;

        armedForFirstMove = false;
        isRunning = true;
        lastCamZ = targetCamera.position.z; // güvence: skor integrasyonu doğru başlasın
        _sessionStartTime = Time.time; // stamp when the player truly starts the run
        OnRunStarted?.Invoke();
    }

    public void StopRun()
    {
        if (!isRunning) return;
        isRunning = false;
        baseSpeed = 0f;
        CurrentSpeed = 0f;
        ResetMultipliersToDefault();

        // booster kapat
        if (boosterRoutine != null) { StopCoroutine(boosterRoutine); boosterRoutine = null; }
        if (BoosterActive)
        {
            SetBoosterActive(false);
            SetBoosterFill(boosterFillMin);
        }

        OnRunStopped?.Invoke();
    }

    /// <summary>
    /// Prepares the game state for resuming after a revive.
    /// Score and combo are preserved, but the run is paused waiting for first player input.
    /// </summary>
    public void PrepareForRevive()
    {
        Debug.Log("[GameplayLogicApplier] Preparing for revive...");
        
        // Stop running but don't reset everything
        isRunning = false;
        
        // Reset speeds but preserve score/combo
        baseSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        CurrentSpeed = baseSpeed * Mathf.Max(0f, gameplaySpeedMultiplier);
        
        // Arm for first move (same as initial start)
        armedForFirstMove = true;
    }

    // SubmitSessionResultAsync removed. Logic moved to UserDatabaseManager.SubmitGameplaySessionAsync.


    /// <summary>
    /// Reset per-run trackers used when submitting session result to server.
    /// </summary>
    public void ResetSessionTrackers()
    {
        // Start time will be set at the exact moment the run begins (StartRun/NotifyFirstPlayerMove)
        _sessionStartTime = 0f;
        _sessionMaxComboMultiplier = 1f;
        _sessionPowerUpsCollected = 0;
        OnPowerUpCollectedCountChanged?.Invoke(_sessionPowerUpsCollected);
    }

    /// <summary>
    /// Call this when a power-up is collected during the run.
    /// </summary>
    public void RegisterPowerUpPickup()
    {
        _sessionPowerUpsCollected++;
        OnPowerUpCollectedCountChanged?.Invoke(_sessionPowerUpsCollected);
    }

    /// <summary>
    /// Returns the highest combo multiplier reached in this run as a decimal (no rounding).
    /// </summary>
    public double GetMaxComboInSession()
    {
        // Hiç yuvarlama yapmıyoruz; 1.24x -> 1.24 olarak gider
        return (double)_sessionMaxComboMultiplier;
    }

    /// <summary>
    /// Returns the playtime for this run in whole seconds.
    /// </summary>
    public int GetPlaytimeSec()
    {
        if (_sessionStartTime <= 0f) return 0;
        return Mathf.Max(0, Mathf.FloorToInt(Time.time - _sessionStartTime));
    }

    /// <summary>
    /// Returns how many power-ups were collected during this run.
    /// </summary>
    public int GetPowerUpsCollectedInSession()
    {
        return _sessionPowerUpsCollected;
    }

    public void ResetRun()
    {
        ResetSessionValues(hardResetCameraSnapshot: false);
    }

    public void SetMaxSpeed(float newMax)
    {
        maxSpeed = Mathf.Max(0f, newMax);
        CurrentSpeed = Mathf.Min(CurrentSpeed, maxSpeed);
    }

    public void BoosterFillToMaxInstant()
    {
        SetBoosterFill(boosterFillMax);
    }

    /// <summary>
    /// Booster barını anında maksimuma doldurur (alias).
    /// </summary>
    public void FillBoosterToMax()
    {
        SetBoosterFill(boosterFillMax);
    }

    /// <summary>
    /// Gameplay(camera) speed: apply percent delta. +0.10 => +10%, -0.10 => -10%.
    /// Multiplier is clamped to [0, +inf). No negatives.
    /// </summary>
    public void ApplyGameplaySpeedPercent(float delta01)
    {
        var factor = 1f + delta01;
        // forbid negative/zero; allow full stop at 0
        if (factor < 0f) factor = 0f;
        gameplaySpeedMultiplier = Mathf.Max(0f, gameplaySpeedMultiplier * factor);
        OnGameplaySpeedMultiplierChanged?.Invoke(gameplaySpeedMultiplier);
        // refresh effective speed immediately if running
        CurrentSpeed = baseSpeed * gameplaySpeedMultiplier;
    }

    /// <summary>
    /// Set gameplay(camera) speed multiplier absolutely. 1 = 100% (no change).
    /// </summary>
    public void SetGameplaySpeedMultiplier(float multiplier)
    {
        gameplaySpeedMultiplier = Mathf.Max(0f, multiplier);
        OnGameplaySpeedMultiplierChanged?.Invoke(gameplaySpeedMultiplier);
        CurrentSpeed = baseSpeed * gameplaySpeedMultiplier;
    }

    /// <summary>
    /// Player(ball) movement speed: apply percent delta. +0.10 => +10%, -0.10 => -10%.
    /// Multiplier is clamped to [0, +inf).
    /// </summary>
    public void ApplyPlayerSpeedPercent(float delta01)
    {
        var factor = 1f + delta01;
        if (factor < 0f) factor = 0f;
        playerSpeedMultiplier = Mathf.Max(0f, playerSpeedMultiplier * factor);
        OnPlayerSpeedMultiplierChanged?.Invoke(playerSpeedMultiplier);
        // If you later want to pipe this to PlayerController, subscribe externally to the event.
    }

    /// <summary>
    /// Set player(ball) movement speed multiplier absolutely. 1 = 100% (no change).
    /// </summary>
    public void SetPlayerSpeedMultiplier(float multiplier)
    {
        playerSpeedMultiplier = Mathf.Max(0f, multiplier);
        OnPlayerSpeedMultiplierChanged?.Invoke(playerSpeedMultiplier);
    }

    /// <summary>
    /// Tetikleyici: Booster doluysa kullan ve akışı başlat.
    /// </summary>
    public void BoosterUse()
    {
        if (BoosterActive) return;
        if (BoosterFill < boosterFillMax) return;
        if (boosterDurationSeconds <= 0f) return;

        OnBoosterUsed?.Invoke();

        if (boosterRoutine != null) StopCoroutine(boosterRoutine);
        boosterRoutine = StartCoroutine(Co_Booster());
    }

    public void AddCoins(float delta, Vector3? worldFxAt = null, int fxCount = 1)
    {
        if (delta > 0f)
        {
            Coins += delta;
            OnCoinsChanged?.Invoke(Coins, delta);
        }
        else
        {
            OnCoinsChanged?.Invoke(Coins, 0f);
        }

        // 2) Booster fills once per coin pickup (independent of value)
        if (boosterFillPerCollectedCoin > 0f)
            SetBoosterFill(BoosterFill + boosterFillPerCollectedCoin);

        // 3) Combo Logic: 
        // a) Increase Combo Power by Base Amount (simulated as delta if needed, but per specs: "Her coin toplamada combo power +25 artacak (if base 25)")
        // The spec said: "oyuncu 50 combo power ile girseydi 50 artacaktı." -> Increase by BaseComboPower.
        CurrentComboPower += _baseComboPower;
        
        // b) Score Gain: "Her coin toplamada o anki combo power kadar score kazanılacak."
        Score += CurrentComboPower;

        // Reset timeout
        timeSinceLastCoin = 0f;
        OnComboMultiplierChanged?.Invoke(CurrentComboPower); // Update UI

        // 4) Optional FX (default 1 burst)
        if (worldFxAt.HasValue)
            OnCoinPickupFXRequest?.Invoke(worldFxAt.Value, 1);
    }

    // ---- Update loop ----
    private void Update()
    {
        if (!isRunning || targetCamera == null) return;

        // 1) Score Time-based: "oyuncu o anki combo power kadar dakikalık score kazanıcak."
        // Score per second = CurrentComboPower / 60f;
        Score += (CurrentComboPower / 60f) * Time.deltaTime;

        // 2) Combo Timeout
        timeSinceLastCoin += Time.deltaTime;
        if (timeSinceLastCoin >= comboTimeout && CurrentComboPower > _baseComboPower)
        {
            ResetCombo(); // Reset to base power
        }
        OnScoreChanged?.Invoke(Score);


        // hız artışı (base)
        if (baseSpeed < maxSpeed)
            baseSpeed = Mathf.Min(maxSpeed, baseSpeed + accelerationPerSecond * Time.deltaTime);

        // Effective speed after gameplay multiplier
        CurrentSpeed = baseSpeed * Mathf.Max(0f, gameplaySpeedMultiplier);

        // kamera Z ileri
        // Camera movement is now handled by GameplayCameraManager
        // Vector3 pos = targetCamera.position;
        // pos.z += CurrentSpeed * Time.deltaTime;
        // targetCamera.position = pos;

        lastCamZ = targetCamera.position.z;
    }

    // ---- Internals ----
    private void ResetSessionValues(bool hardResetCameraSnapshot)
    {
        baseSpeed = 0f;
        CurrentSpeed = 0f;
        Score = 0f;
        Coins = 0f;

        if (boosterRoutine != null) { StopCoroutine(boosterRoutine); boosterRoutine = null; }
        SetBoosterActive(false);
        SetBoosterFill(boosterFillMin);

        if (targetCamera != null)
            lastCamZ = targetCamera.position.z;

        if (hardResetCameraSnapshot && targetCamera != null)
        {
            // Kamerayı aynı yerde bırakıyoruz; pozisyon resetini orkestra (GameplayManager) isterse yapsın.
        }
    }

    private void SetBoosterFill(float value)
    {
        BoosterFill = Mathf.Clamp(value, boosterFillMin, boosterFillMax);
        OnBoosterChanged?.Invoke(BoosterFill, boosterFillMin, boosterFillMax);
    }



    private void SetBoosterActive(bool active)
    {
        BoosterActive = active;
        OnBoosterStateChanged?.Invoke(active);

        if (player != null)
        {
            player.isBoosterEnabled = active;
            if (active) player.OnBoosterStart(); else player.OnBoosterEnd();
            
            // Apply Magnet Boost
            var magnet = player.GetComponentInChildren<PlayerMagnet>();
            if (magnet != null)
            {
                magnet.SetBoosterMultiplier(active ? boosterMagnetMultiplier : 1f);
            }
        }

        if (active)
        {
            // Apply boost (additive percentage: 1.35x means +35%)
            ApplyPlayerSpeedPercent(boosterSpeedMultiplier - 1f);
        }
        else
        {
            // Revert to normal (100%)
            SetPlayerSpeedMultiplier(1f);
        }
    }

    private IEnumerator Co_Booster()
    {
        SetBoosterActive(true);

        float startFill = BoosterFill;
        float endFill = boosterFillMin;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, boosterDurationSeconds);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float current = Mathf.Lerp(startFill, endFill, t);
            SetBoosterFill(current);
            yield return null;
        }

        SetBoosterFill(boosterFillMin);
        SetBoosterActive(false);
        boosterRoutine = null;
    }

    public void SetBaseComboPower(int power)
    {
        _baseComboPower = Mathf.Max(1, power);
        // If we are resetting or just starting, sync current power
        if (!isRunning || CurrentComboPower < _baseComboPower)
            CurrentComboPower = _baseComboPower;
    }

    public void ResetCombo()
    {
        CurrentComboPower = _baseComboPower;
        timeSinceLastCoin = 0f;
        OnComboReset?.Invoke();
        OnComboMultiplierChanged?.Invoke(CurrentComboPower);
        
        // Track stats for session if needed (max power reached?)
        if (_sessionMaxComboMultiplier < CurrentComboPower)
            _sessionMaxComboMultiplier = CurrentComboPower;
    }

    // Unused helper removed: RecomputeComboMultiplier
    // Unused helper removed: RegisterCoinForCombo
}