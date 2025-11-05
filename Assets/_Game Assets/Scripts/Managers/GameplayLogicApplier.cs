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

    // --- New Concept: Percent-based speed multipliers ---
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
    public event Action OnRunStarted;
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

    public event Action<float> OnComboMultiplierChanged; // emits current combo multiplier (e.g., 1.00, 1.15, ...)
    public event Action OnComboReset;                    // fires when combo is reset to baseline
    
        // --- Combo Score System ---
    [SerializeField] private float scorePerSecond = 1f;    // her saniye kazanılan taban skor
    [SerializeField] private float baseIncPerCoin = 0.01f; // coin başına taban artış
    [SerializeField] private float comboTimeout = 10f;     // coin toplanmazsa şu sürede sıfırla (sn)

    private int   comboStreak = 0;      // ardışık coin adedi
    private float comboMultiplier = 1f; // 1 + comboStreak * (incPerCoin)
    private float timeSinceLastCoin = 0f;

    // stat'tan gelen bonus (%). Manager set edecek.
    private int comboPowerPercent = 0; // ör: 50 => +%50, yani 0.01 -> 0.015
    private float IncPerCoin => baseIncPerCoin * (1f + comboPowerPercent / 100f);

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
    /// Sunucuya bu oturumun sonuçlarını gönderir. GameplayManager bu metodu tetikler.
    /// Varsayılan olarak logic içindeki Score ve Coins değerlerini gönderir; istenirse override verilebilir.
    /// </summary>
    public async Task<bool> SubmitSessionResultAsync(string sessionId, double? currencyOverride = null, double? scoreOverride = null)
    {
        double earnedCurrency = currencyOverride ?? Coins;
        double earnedScore    = scoreOverride ?? Score;

        try
        {
            // Extended metrics (decimal combo, playtime, power-ups)
            double maxComboInSession = GetMaxComboInSession();
            int playtimeSec = GetPlaytimeSec();
            int powerUpsCollectedInSession = GetPowerUpsCollectedInSession();
            var res = await SessionResultRemoteService.SubmitAsync(
                sessionId,
                earnedCurrency,
                earnedScore,
                maxComboInSession,
                playtimeSec,
                powerUpsCollectedInSession
            );
            OnSessionResultSubmitted?.Invoke(res.alreadyProcessed, res.currency, res.maxScore);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameplayLogicApplier] SubmitSessionResultAsync failed: {ex.Message}");
            OnSessionResultFailed?.Invoke(ex.Message);
            return false;
        }
    }

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

        // 3) Combo increments by exactly 1 per coin pickup (independent of value)
        RegisterCoinForCombo();

        // 4) Optional FX (default 1 burst)
        if (worldFxAt.HasValue)
            OnCoinPickupFXRequest?.Invoke(worldFxAt.Value, 1);
    }

    // ---- Update loop ----
    private void Update()
    {
        if (!isRunning || targetCamera == null) return;

        // 1) Skor birikimi: saniye * comboMultiplier
        Score += scorePerSecond * comboMultiplier * Time.deltaTime;

        // 2) Combo zaman aşımı
        timeSinceLastCoin += Time.deltaTime;
        if (timeSinceLastCoin >= comboTimeout && comboStreak > 0)
        {
            ResetCombo(); // 10 sn coin yoksa 1x'e sıfırla
        }
        OnScoreChanged?.Invoke(Score);


        // hız artışı (base)
        if (baseSpeed < maxSpeed)
            baseSpeed = Mathf.Min(maxSpeed, baseSpeed + accelerationPerSecond * Time.deltaTime);

        // Effective speed after gameplay multiplier
        CurrentSpeed = baseSpeed * Mathf.Max(0f, gameplaySpeedMultiplier);

        // kamera Z ileri
        Vector3 pos = targetCamera.position;
        pos.z += CurrentSpeed * Time.deltaTime;
        targetCamera.position = pos;

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

    public void SetComboPowerPercent(int percent)
    {
        comboPowerPercent = percent;
        RecomputeComboMultiplier(); // mevcut comboStreak için anlık güncelle
    }

    public void ResetCombo()
    {
        comboStreak = 0;
        comboMultiplier = 1f;
        timeSinceLastCoin = 0f;
        OnComboReset?.Invoke();
        OnComboMultiplierChanged?.Invoke(comboMultiplier);
    }
    private void RecomputeComboMultiplier()
    {
        float prev = comboMultiplier;
        comboMultiplier = 1f + comboStreak * IncPerCoin;
        if (comboMultiplier < 0.0001f) comboMultiplier = 0.0001f; // güvence
        if (!Mathf.Approximately(prev, comboMultiplier))
            OnComboMultiplierChanged?.Invoke(comboMultiplier);
        // Track session max combo for submit
        if (_sessionMaxComboMultiplier < comboMultiplier)
            _sessionMaxComboMultiplier = comboMultiplier;
    }
    private void RegisterCoinForCombo()
    {
        comboStreak += 1;
        timeSinceLastCoin = 0f;
        RecomputeComboMultiplier();
    }
}