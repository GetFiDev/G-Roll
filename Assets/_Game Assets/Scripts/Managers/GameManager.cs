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

    public void OnPlayButtonPressed()
    {
        // UI kapı: requesting
        sessionGate?.ShowRequesting();
        StartCoroutine(RequestAndStart());
    }

    private System.Collections.IEnumerator RequestAndStart()
    {
        // requestSession server call
        var task = SessionRemoteService.RequestSessionAsync();
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted || task.IsCanceled)
        {
            // ağ hatası vs.
            yield return sessionGate?.ShowInsufficientToast();
            yield break;
        }

        var resp = task.Result;
        if (resp.ok && !string.IsNullOrEmpty(resp.sessionId))
        {
            yield return sessionGate?.ShowGrantedToast();

            // Eski mimariyi koru: grant sonrası fazı Gameplay'e al, dinleyenler çalışsın
            SetPhase(GamePhase.Gameplay);
            gameplayManager?.BeginSessionWithServerGrant(resp.sessionId);
        }
        else
        {
            // yeterli enerji yok
            yield return sessionGate?.ShowInsufficientToast();
        }
    }
}