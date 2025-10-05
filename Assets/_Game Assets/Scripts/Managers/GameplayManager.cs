using System.Collections;
using UnityEngine;

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

    private IEnumerator BeginSessionWhenInGameplay()
    {
        // Faz GamePlay olana kadar bekle (GameManager set edecek)
        while (!hasControl) yield return null;

        UIManager.Instance?.ShowGameplayLoading();

        // Hazırlık
        mapLoader?.Load();
        yield return new WaitUntil(() => mapLoader != null && mapLoader.IsReady);
        playerGO = playerSpawner?.Spawn();
        var playerCtrl = playerGO ? playerGO.GetComponent<PlayerController>() : null;
        var playerMov  = playerGO ? playerGO.GetComponent<PlayerMovement>()  : null;
        if (playerMov != null && logicApplier != null)
        {
            playerMov.BindToGameplay(logicApplier, gameplayTouch);
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
            // Stat handler şu anda faktör (1x, 2x ...) veriyor; yüzdeye çevir: (factor-1)*100
            int comboPowerPercent = Mathf.Max(0, (_comboPowerFactor - 1) * 100);
            logicApplier.SetComboPowerPercent(comboPowerPercent);
        }

        // Bir frame bekle: olası geç-yerleştirme/reset akışları (spawn point vb.) bittikten sonra boyutu son kez uygula
        yield return null;
        var mv2 = playerGO ? playerGO.GetComponent<PlayerMovement>() : null;
        if (mv2 != null)
        {
            mv2.SetPlayerSize(_playerSizePct);
        }

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
    public void EndSession(bool success)
    {
        if (!sessionActive) return;
        sessionActive = false;

        // Süreyi hesapla ve istatistikleri doldur
        if (stats == null) stats = new GameplayStats();
        stats.levelSuccess = success;
        stats.playtimeSeconds = Mathf.Max(0f, Time.time - sessionStartTime);

        // Let player stats cleanup (if any)
        var statEnd = playerGO ? playerGO.GetComponent<PlayerStatHandler>() : null;
        statEnd?.OnRunEnd();

        // Koşuyu durdur ve temizle
        visualApplier?.Unbind();
        logicApplier?.StopRun();
        logicApplier?.ResetCombo();
        logicApplier?.SetGameplaySpeedMultiplier(1f);
        logicApplier?.SetPlayerSpeedMultiplier(1f);
        mapLoader?.Unload();
        playerSpawner?.DespawnAll();
        if (playerGO != null) { Destroy(playerGO); playerGO = null; }

        _coinBonusPercent = 0;
        _comboPowerFactor = 1;
        _coinBonusPercent = 0;
        _magnetBonusPercent = 0;
        _gameplaySpeedPct = 0;
        _playerAccelPer60 = 0.1f;
        _playerSpeedAdd   = 0f;
        _playerSizePct    = 0;

        // (Gerekirse burada async raporlama yapabilirsiniz)
        stats = null;

        // Fazı Meta'ya geri aldır
        requestReturnToMeta.Raise();
    }

    private void TearDownIfAny()
    {
        // Önce görsel ve mantık bağlantılarını kapat
        var statTear = playerGO ? playerGO.GetComponent<PlayerStatHandler>() : null;
        statTear?.OnRunEnd();

        visualApplier?.Unbind();
        logicApplier?.StopRun();
        logicApplier?.ResetCombo();
        logicApplier?.SetGameplaySpeedMultiplier(1f);
        logicApplier?.SetPlayerSpeedMultiplier(1f);
        mapLoader?.Unload();
        playerSpawner?.DespawnAll();
        if (playerGO != null) { Destroy(playerGO); playerGO = null; }
        _coinBonusPercent = 0;
        _comboPowerFactor = 1;
        _coinBonusPercent = 0;
        _magnetBonusPercent = 0;
        _gameplaySpeedPct = 0;
        _playerAccelPer60 = 0.1f;
        _playerSpeedAdd   = 0f;
        _playerSizePct    = 0;
        stats = null;
        sessionActive = false;
        sessionStartTime = 0f;
    }


    // ---- Delegates to LogicApplier (optional but useful) ----
    public void AddCoins(float delta, Vector3? worldFxAt = null, int fxCount = 1)
    {
        if (delta <= 0f) return;
        var factor = 1f + (_coinBonusPercent / 100f);
        var effective = delta * factor;
        logicApplier?.AddCoins(effective, worldFxAt, fxCount);
    }
    public void BoosterUse() => logicApplier?.BoosterUse();
    public void ApplyGameplaySpeedPercent(float delta01)
    {
        if (logicApplier == null) return;
        logicApplier.ApplyGameplaySpeedPercent(delta01);
    }
    public void ApplyPlayerSpeedPercent(float delta01)
    {
        if (logicApplier == null) return;
        logicApplier.ApplyPlayerSpeedPercent(delta01);
    }
    public void SetMaxSpeed(float m) => logicApplier?.SetMaxSpeed(m);
    public void InstantlyFillTheBooster() => logicApplier?.FillBoosterToMax();

    public void RefreshCachedStatsFromPlayer()
    {
        var stat = playerGO ? playerGO.GetComponent<PlayerStatHandler>() : null;
        if (stat != null)
        {
            _comboPowerFactor = stat.FinalComboPowerFactor;
            _coinBonusPercent = stat.FinalCoinMultiplierPercent;
            _magnetBonusPercent = stat.FinalMagnetPowerPct;
            _gameplaySpeedPct = stat.FinalGameplaySpeedPct;
            _playerAccelPer60 = stat.FinalPlayerAcceleration;
            _playerSpeedAdd   = stat.FinalPlayerSpeedAdd;
            _playerSizePct    = stat.FinalPlayerSizePct;
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
    }
}