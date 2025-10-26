using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Channels (Assign in Inspector)")]
    [SerializeField] private VoidEventChannelSO requestStartGameplay;
    [SerializeField] private VoidEventChannelSO requestReturnToMeta;
    [SerializeField] private PhaseEventChannelSO phaseChanged;

    [Header("State (Read-Only)")]
    [SerializeField] public GamePhase current = GamePhase.Boot;
    public AudioManager audioManager;
    public static GameManager Instance;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (audioManager != null)
            audioManager.Initialize();
    }

    private void OnEnable()
    {
        if (requestStartGameplay != null) requestStartGameplay.OnEvent += OnRequestStartGameplay;
        if (requestReturnToMeta != null) requestReturnToMeta.OnEvent += OnRequestReturnToMeta;
    }

    private void OnDisable()
    {
        if (requestStartGameplay != null) requestStartGameplay.OnEvent -= OnRequestStartGameplay;
        if (requestReturnToMeta != null) requestReturnToMeta.OnEvent -= OnRequestReturnToMeta;
    }

    private void Start()
    {
        // Boot tamamlanınca Meta’ya geç.
        SetPhase(GamePhase.Meta);
        StartCoroutine(FetchAndDebugItems());
    }

    private void OnRequestStartGameplay()
    {
        OnPlayButtonPressed();
    }

    private void OnRequestReturnToMeta()
    {
        SetPhase(GamePhase.Meta);
    }

    private void SetPhase(GamePhase next)
    {
        if (current == next) return;
        current = next;
        phaseChanged.Raise(current);
    }

    [SerializeField] private UISessionGate sessionGate; // Inspector’dan ver
    [SerializeField] private GameplayManager gameplayManager; // zaten vardır
    private float _gateShownAt;

    public void OnPlayButtonPressed()
    {
        // 1) UI kapıyı HEMEN göster ve zaman damgasını al
        if (sessionGate)
        {
            sessionGate.ShowRequesting();
            _gateShownAt = Time.realtimeSinceStartup;
        }

        // 2) Ardından isteği başlat
        StartCoroutine(RequestAndStart());
    }

    private System.Collections.IEnumerator RequestAndStart()
    {
        // UI'nın gerçekten bir frame çizmesine izin ver
        if (sessionGate) yield return null;

        // requestSession server call
        var task = SessionRemoteService.RequestSessionAsync();
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted || task.IsCanceled)
        {
            // ağ hatası vs.
            if (sessionGate) yield return sessionGate.ShowInsufficientToast();
            yield break;
        }

        var resp = task.Result;
        if (resp.ok && !string.IsNullOrEmpty(resp.sessionId))
        {
            // 'Please wait…' en az kısa bir süre görünsün (ör. 0.2s)
            const float minVisible = 0.2f;
            if (sessionGate)
            {
                float elapsed = Time.realtimeSinceStartup - _gateShownAt;
                if (elapsed < minVisible)
                    yield return new WaitForSecondsRealtime(minVisible - elapsed);
            }

            // 0.5s granted tostu
            if (sessionGate) yield return sessionGate.ShowGrantedToast();

            // Eski mimariyi koru: grant sonrası fazı Gameplay'e al, dinleyenler çalışsın
            SetPhase(GamePhase.Gameplay);
            gameplayManager?.BeginSessionWithServerGrant(resp.sessionId);
        }
        else
        {
            // yeterli enerji yok
            if (sessionGate) yield return sessionGate.ShowInsufficientToast();
        }
    }
    private System.Collections.IEnumerator FetchAndDebugItems()
    {
        var initTask = ItemDatabaseManager.InitializeAsync();
        while (!initTask.IsCompleted) yield return null;
        foreach (var item in ItemDatabaseManager.GetAllItems())
        {
            Debug.Log(item);
        }
    }
}