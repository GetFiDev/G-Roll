using System;
using System.Collections;
using UnityEngine;

/// Mantık: hız, skor, coin/booster hesapları, booster süreci
public class GameplayLogicApplier : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float startSpeed = 5f;
    [SerializeField] private float maxSpeed   = 20f;
    [SerializeField] private float accelerationPerSecond = 0.75f;

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

    // Runtime state
    // Effective camera speed after multiplier
    public float CurrentSpeed { get; private set; }
    // Internal accelerating base (before multiplier)
    private float baseSpeed;
    public float Score        { get; private set; }
    public float Coins        { get; private set; }
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

    // ---- Public control API (GameplayManager buradan çağırır) ----
    public void InitializeSession(Transform cameraTransform, PlayerController optionalPlayer = null)
    {
        targetCamera = cameraTransform;
        if (optionalPlayer) player = optionalPlayer;

        ResetSessionValues(hardResetCameraSnapshot: true);
    }

    public void StartRun()
    {
        if (targetCamera == null) return;

        isRunning = true;
        // was: CurrentSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        baseSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        CurrentSpeed = baseSpeed * Mathf.Max(0f, gameplaySpeedMultiplier);
        lastCamZ = targetCamera.position.z;
        OnRunStarted?.Invoke();
    }

    public void StopRun()
    {
        if (!isRunning) return;
        isRunning = false;

        // booster kapat
        if (boosterRoutine != null) { StopCoroutine(boosterRoutine); boosterRoutine = null; }
        if (BoosterActive)
        {
            SetBoosterActive(false);
            SetBoosterFill(boosterFillMin);
        }

        OnRunStopped?.Invoke();
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
        if (delta <= 0f) return;

        Coins += delta;
        if (boosterFillPerCollectedCoin > 0f)
            SetBoosterFill(BoosterFill + boosterFillPerCollectedCoin);

        OnCoinsChanged?.Invoke(Coins, delta);

        if (worldFxAt.HasValue)
            OnCoinPickupFXRequest?.Invoke(worldFxAt.Value, Mathf.Max(1, fxCount));
    }

    // ---- Update loop ----
    private void Update()
    {
        if (!isRunning || targetCamera == null) return;

        // hız artışı (base)
        if (baseSpeed < maxSpeed)
            baseSpeed = Mathf.Min(maxSpeed, baseSpeed + accelerationPerSecond * Time.deltaTime);

        // Effective speed after gameplay multiplier
        CurrentSpeed = baseSpeed * Mathf.Max(0f, gameplaySpeedMultiplier);

        // kamera Z ileri
        Vector3 pos = targetCamera.position;
        pos.z += CurrentSpeed * Time.deltaTime;
        targetCamera.position = pos;

        // skor
        float dz = targetCamera.position.z - lastCamZ;
        if (dz > 0f)
        {
            Score += dz;
            OnScoreChanged?.Invoke(Score);
        }
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

    public float GetGameplaySpeedPercent() => gameplaySpeedMultiplier;
    public float GetPlayerSpeedPercent()   => playerSpeedMultiplier;
}