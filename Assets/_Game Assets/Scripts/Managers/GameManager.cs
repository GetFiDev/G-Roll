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
        _ = RequestSessionAndStartAsync(CurrentMode);
    }

    private void OnRequestReturnToMeta()
    {
        SetPhase(GamePhase.Meta);
        UITopPanel.Instance.Initialize();
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
        Debug.LogWarning("[GameManager] OnPlayButtonPressed is DEPRECATED. Use RequestSessionAndStartAsync() or ensure UIHomePanel handles the flow.");
        // Forwarding to new logic for safety, but UIHomePanel should be the caller.
        _ = RequestSessionAndStartAsync(CurrentMode);
    }

    /// <summary>
    /// Async method for external UI controllers (like UIHomePanel) to request start.
    /// Returns true if session granted and game starting, false otherwise.
    /// Does NOT trigger sessionGate "Requesting" screen, assumes caller handles loading UI.
    /// </summary>
    /// <summary>
    /// Async method for external UI controllers (like UIHomePanel) to request start.
    /// STRICT FLOW: Request Session -> Check Result -> (If OK) Show Loading & Start Game -> (If Fail) Show Energy Panel.
    /// </summary>
    public GameMode CurrentMode { get; private set; } = GameMode.Endless;

    public async System.Threading.Tasks.Task<bool> RequestSessionAndStartAsync(GameMode mode)
    {
        CurrentMode = mode;

        // 1. Request Session
        var task = SessionRemoteService.RequestSessionAsync(mode); // Updated to pass mode
        try
        {
            await task;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameManager] Session request exception: {ex.Message}");
        }

        if (task.IsFaulted || task.IsCanceled)
        {
            // Fail Scenario A: Network error or obscure fail
            ShowInsufficientEnergyPanel();
            return false;
        }

        var resp = task.Result;
        if (resp.ok && !string.IsNullOrEmpty(resp.sessionId))
        {
             // Success Scenario B: Energy OK -> Game Start

             // 1. Show Loading Screen First (so user sees transition)
             UIManager.Instance?.ShowGameplayLoading();

             // 2. Grant Session to GameplayManager (still in boot/meta phase technically until next line)
             gameplayManager?.BeginSessionWithServerGrant(resp.sessionId, mode); // Updated to pass mode

             // 3. Switch Phase to Gameplay (Triggers GameplayManager logic)
             SetPhase(GamePhase.Gameplay);
             
             return true;
        }
        else
        {
            // Fail Scenario A: Not enough energy (or other logical fail)
            ShowInsufficientEnergyPanel();
            return false;
        }
    }

    private void ShowInsufficientEnergyPanel()
    {
        if (UIManager.Instance && UIManager.Instance.insufficientEnergyPanel)
        {
            UIManager.Instance.insufficientEnergyPanel.gameObject.SetActive(true);
            UIManager.Instance.insufficientEnergyPanel.RefreshSnapshotAsync(); // ensure up to date
        }
        else
        {
            // Fallback if panel is missing (should not happen in prod)
            Debug.LogError("[GameManager] InsufficientEnergyPanel not assigned in UIManager!");
            if (sessionGate) StartCoroutine(sessionGate.ShowInsufficientToast()); // Legacy backup
        }
    }

    // Legacy internal wrapper removed to prevent accidental usage of old flow.
    // private System.Collections.IEnumerator RequestAndStartInternal(bool useGate) { ... }
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