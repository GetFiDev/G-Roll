using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Basit bir koşu (runner) kamera yöneticisi.
/// - Singleton: GameplayManager.Instance
/// - Kamera Z ekseninde ileri doğru hareket eder.
/// - Hız, startSpeed'den başlar ve accelerationPerSecond ile maxSpeed'e kadar artar.
/// - Harici metodlarla anlık hız artırma/azaltma yapılabilir.
/// </summary>
public class GameplayManager : MonoBehaviour
{
    // ---------------- Singleton ----------------
    public static GameplayManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Sahne değişse de koşu devam edecekse açık bırak. İstemiyorsan kaldır.
        DontDestroyOnLoad(gameObject);
    }

    // ---------------- References ----------------
    [Header("References")]
    [Tooltip("İleri hareket ettirilecek kamera (Transform). Boşsa Camera.main kullanılır.")]
    public Transform targetCamera;

    // ---------------- Speed Settings ----------------
    [Header("Speed Settings")]
    [Tooltip("Oyunun başında kameranın koşu hızı (m/sn).")]
    public float startSpeed = 5f;

    [Tooltip("Kameranın çıkabileceği maksimum hız (m/sn).")]
    public float maxSpeed = 20f;

    [Tooltip("Saniye başına hız artışı. Hız, maxSpeed'e ulaşana kadar bu değerle artar.")]
    public float accelerationPerSecond = 0.75f;

    [Tooltip("Script aktif olduğunda otomatik koşuya başlansın mı?")]
    public bool autoStart = true;

    // ---------------- Runtime State ----------------
    public float CurrentSpeed { get; private set; }
    private bool _isRunning;
    public float currencyCollectedInSession = 0f;
    private void Update()
    {
        if (!_isRunning || targetCamera == null)
            return;

        // Hızı maxSpeed'e doğru ilerlet
        if (CurrentSpeed < maxSpeed)
        {
            CurrentSpeed = Mathf.Min(maxSpeed, CurrentSpeed + accelerationPerSecond * Time.deltaTime);
        }

        // Dünya Z ekseninde ileri hareket
        Vector3 pos = targetCamera.position;
        pos.z += CurrentSpeed * Time.deltaTime;
        targetCamera.position = pos;
    }

    // ---------------- Public API ----------------
    /// <summary>Koşuyu başlatır.</summary>
    [Button("StartRun")]
    public void StartRun()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;

        CurrentSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        _isRunning = true;
        currencyCollectedInSession = 0f;
    }

    /// <summary>Koşuyu durdurur (kamerayı sabitler).</summary>
    public void StopRun()
    {
        _isRunning = false;
    }

    /// <summary>Hızı anlık olarak arttır (ör. boost pickup). Değer m/sn cinsinden eklenir.</summary>
    public void IncreaseSpeed(float delta)
    {
        if (delta <= 0f) return;
        CurrentSpeed = Mathf.Clamp(CurrentSpeed + delta, 0f, maxSpeed);
    }

    /// <summary>Hızı anlık olarak düşür (ör. ceza/engel). Değer m/sn cinsinden çıkarılır.</summary>
    public void DecreaseSpeed(float delta)
    {
        if (delta <= 0f) return;
        CurrentSpeed = Mathf.Clamp(CurrentSpeed - delta, 0f, maxSpeed);
    }

    /// <summary>Hızı doğrudan belirtilen değere çeker.</summary>
    public void SetSpeed(float value)
    {
        CurrentSpeed = Mathf.Clamp(value, 0f, maxSpeed);
    }

    /// <summary>Maksimum hız limitini değiştirir (runtime ayarı). Mevcut hız limiti aşarsa clamp edilir.</summary>
    public void SetMaxSpeed(float newMax)
    {
        maxSpeed = Mathf.Max(0f, newMax);
        CurrentSpeed = Mathf.Min(CurrentSpeed, maxSpeed);
    }
}
