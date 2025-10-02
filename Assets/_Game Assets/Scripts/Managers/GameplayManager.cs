using AssetKits.ParticleImage;
using TMPro;
using Sirenix.OdinInspector;
using UnityEngine;
using System;
using DG.Tweening;

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

    [Header("UI")]
    [Tooltip("Skoru gösterecek TextMeshPro alanı (opsiyonel). Boş bırakılırsa sadece hesaplanır.")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField, Tooltip("Skor tam sayı mı gösterilsin?")]
    private bool scoreAsInteger = true;

    [Header("Currency UI")]
    [Tooltip("Toplam coin sayısını gösterecek TextMeshPro alanı (opsiyonel).")]
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField, Tooltip("Coin değeri tam sayı gösterilsin mi?")]
    private bool coinAsInteger = true;
    [Tooltip("Coin toplandığında tetiklenecek UI efekti (UIParticle vb.). Inspector'dan bağlayın.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onCoinPickupFX;
    private Vector3 _coinTextBaseScale = Vector3.one;

    [Header("Coin FX World Attach")]
    [Tooltip("Coin FX'i oyuncu üzerinde göstermek için referans alınacak Transform.")]
    [SerializeField] private Transform playerTransformForCoinFX;
    [Tooltip("Coin FX'i üretecek UI ParticleImage.")]
    [SerializeField] private ParticleImage coinPickupParticle;
    [Tooltip("FX'in ekrandaki lokal kaydırması (Canvas space, anchored).")]
    [SerializeField] private Vector2 coinFXScreenOffset = Vector2.zero;
    [Tooltip("FX'i şu kameraya göre konumlandır (ScreenSpace-Camera Canvas için gereklidir). Boşsa Camera.main kullanılır.")]
    [SerializeField] private Camera uiCameraOverride;
    [Tooltip("Player'ın world -> screen dönüşümü için kullanılacak kamera. Boşsa Camera.main kullanılır.")]
    [SerializeField] private Camera worldCameraForCoinFX;
    [Tooltip("FX çalıştırmadan önce otomatik olarak ParticleImage'i Play eder.")]
    [SerializeField] private bool autoPlayUIParticle = true;

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

    // ---------------- Coins ----------------
    /// <summary>Bu koşu sırasında toplanan toplam coin.</summary>
    public float Coins { get; private set; } = 0;

    /// <summary>Coin ekler (UI olayı yayınlar).</summary>
    public void AddCoins(float delta)
    {
        if (delta <= 0) return;
        Coins += delta;
        currencyCollectedInSession += delta;

        // UI güncelle (event yok, score mantığıyla)
        if (coinText != null)
        {
            coinText.text = coinAsInteger ? Coins.ToString() : Coins.ToString("F2");
            var t = coinText.transform;
            t.DOKill();
            if (_coinTextBaseScale == Vector3.one) _coinTextBaseScale = t.localScale; // güvence
            t.localScale = _coinTextBaseScale;
            t.DOScale(_coinTextBaseScale * 1.15f, 0.08f).SetEase(Ease.OutQuad)
             .OnComplete(() => t.DOScale(_coinTextBaseScale, 0.08f).SetEase(Ease.InQuad));
        }

        // UI Particle'ı oyuncu üzerinde konumlandırıp tek burst ateşle
        if (playerTransformForCoinFX != null)
            EmitCoinFXAtWorld(playerTransformForCoinFX.position, 1);

        onCoinPickupFX?.Invoke();
    }

    // ---------------- Score ----------------
    /// <summary>Koşu başladığında 0'dan saymaya başlar; kat edilen mesafe kadar artar (metre).</summary>
    public float Score { get; private set; } = 0f;
    private float _lastCamZ = 0f;

    // --- Spawn snapshot (captured once) ---
    private bool _spawnCaptured = false;
    private Vector3 _startCamPosition;
    private Quaternion _startCamRotation;

    private void CaptureStartIfNeeded()
    {
        if (_spawnCaptured) return;
        if (targetCamera == null) return;
        _startCamPosition = targetCamera.position;
        _startCamRotation = targetCamera.rotation;
        _spawnCaptured = true;
    }

    // Run lifecycle notifications
    public event Action OnRunStarted;
    public event Action OnRunStopped;

    [Header("Level Flow")]
    [Tooltip("StopRun çağrıldığında GameManager'a level bitti bilgisini gönder.")]
    public bool notifyGameManagerOnStop = true;

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

        // Skor: kat edilen Z mesafesi kadar artar (geri gitme skoru azaltmaz)
        float dz = targetCamera.position.z - _lastCamZ;
        if (dz > 0f) Score += dz;
        _lastCamZ = targetCamera.position.z;

        // UI güncelle
        if (scoreText != null)
        {
            if (scoreAsInteger)
                scoreText.text = Mathf.FloorToInt(Score).ToString();
            else
                scoreText.text = Score.ToString("F2");
        }
    }

    // ---------------- Public API ----------------
    /// <summary>Koşuyu başlatır.</summary>
    [Button("StartRun")]
    public void StartRun()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;

        // capture initial camera transform only once
        CaptureStartIfNeeded();

        CurrentSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        _isRunning = true;
        currencyCollectedInSession = 0f;
        // Coins reset
        Coins = 0;

        // Score'u sıfırla ve başlangıç Z'yi referans al
        Score = 0f;
        _lastCamZ = targetCamera != null ? targetCamera.position.z : 0f;

        // Coin UI'ı sıfırla
        if (coinText != null)
        {
            _coinTextBaseScale = coinText.transform.localScale;
            coinText.text = coinAsInteger ? "0" : "0.00";
        }

        OnRunStarted?.Invoke();

        if (autoStart == false)
        {
            // not: autoStart sadece başlangıç davranışı için;
            // burada ekstra bir şey yapmıyoruz. Inspector için referans.
        }
    }

    /// <summary>Koşuyu durdurur (kamerayı sabitler) ve istenirse level'ı bitirir.</summary>
    public void StopRun()
    {
        if (!_isRunning) return;
        _isRunning = false;

        // Bildirim
        OnRunStopped?.Invoke();

        // İsteğe bağlı: Level bitti bilgisini GameManager'a ilet
        if (notifyGameManagerOnStop && GameManager.Instance != null)
        {
            GameManager.Instance.EnterLevelComplete();
        }
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
    /// <summary>
    /// Kamerayı ilk kaydedilen başlangıç konum/rotasyonuna döndürür ve koşuyu sıfırlar.
    /// StopRun'dan farklı olarak level complete tetiklemez; sadece state'i temizler.
    /// </summary>
    public void ResetRun()
    {
        // Koşuyu sessizce kapat (event veya level complete yok)
        _isRunning = false;

        // Hızı ve oturum metriklerini sıfırla
        CurrentSpeed = 0f;
        currencyCollectedInSession = 0f;
        Coins = 0;

        // Skoru ve referans Z'yi sıfırla
        Score = 0f;
        _lastCamZ = targetCamera != null ? targetCamera.position.z : 0f;
        if (scoreText != null) scoreText.text = scoreAsInteger ? "0" : "0.00";

        // Coin UI'ı sıfırla
        if (coinText != null)
        {
            coinText.text = coinAsInteger ? "0" : "0.00";
            coinText.transform.localScale = _coinTextBaseScale == Vector3.one ? coinText.transform.localScale : _coinTextBaseScale;
        }

        // Kamera başlangıcı kaydedildiyse geri al
        if (_spawnCaptured && targetCamera != null)
        {
            targetCamera.SetPositionAndRotation(_startCamPosition, _startCamRotation);
        }
        else if (targetCamera != null && !_spawnCaptured)
        {
            // İlk kullanımda sahnedeki mevcut değeri başlangıç kabul et
            _startCamPosition = targetCamera.position;
            _startCamRotation = targetCamera.rotation;
            _spawnCaptured = true;
        }
    }
    private void EmitCoinFXAtWorld(Vector3 worldPos, int count = 1)
    {
        if (coinPickupParticle == null) { onCoinPickupFX?.Invoke(); return; }
        var canvas = coinPickupParticle.canvas;
        if (canvas == null) { onCoinPickupFX?.Invoke(); return; }

        // Hangi kamerayla world -> screen yapılacağı
        Camera worldCam = worldCameraForCoinFX != null ? worldCameraForCoinFX : Camera.main;
        if (worldCam == null) { onCoinPickupFX?.Invoke(); return; }

        // Canvas türüne göre ScreenPoint -> CanvasLocal dönüşümü
        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) { onCoinPickupFX?.Invoke(); return; }

        // Önce world -> screen noktası
        Vector3 screen = RectTransformUtility.WorldToScreenPoint(worldCam, worldPos);
        if (screen.z < 0f) { onCoinPickupFX?.Invoke(); return; } // kamera arkasında

        // Canvas kamera seçimi (Overlay'de null verilmelidir)
        Camera uiCam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            uiCam = uiCameraOverride != null ? uiCameraOverride : canvas.worldCamera;

        // Ekran noktasını canvas local'e çevir
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out local))
        {
            onCoinPickupFX?.Invoke();
            return;
        }

        // Hedef rect
        var rt = coinPickupParticle.rectTransform;

        // WorldSpace canvas'ta anchoredPosition yerine pozisyonu set etmek daha doğrudur.
        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            // Canvas local uzayındaki 'local' noktasını world'e çevirip konumla
            Vector3 worldOnCanvas = canvasRect.TransformPoint(local);
            rt.position = worldOnCanvas + (Vector3)coinFXScreenOffset;
        }
        else
        {
            // Overlay veya ScreenSpace-Camera'da anchoredPosition yeterli
            rt.anchoredPosition = local + coinFXScreenOffset;
        }

        // Tek seferlik burst ateşle
        coinPickupParticle.EmitBurstNow(count <= 0 ? 1 : count, autoPlayUIParticle);
    }
}