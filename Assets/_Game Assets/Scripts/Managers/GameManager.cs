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
        else { Destroy(Instance.gameObject); }

        audioManager.Initialize();
    }

    private void OnEnable()
    {
        requestStartGameplay.OnEvent += OnRequestStartGameplay;
        requestReturnToMeta.OnEvent += OnRequestReturnToMeta;
    }

    private void OnDisable()
    {
        requestStartGameplay.OnEvent -= OnRequestStartGameplay;
        requestReturnToMeta.OnEvent -= OnRequestReturnToMeta;
    }

    private void Start()
    {
        // Boot tamamlanınca Meta’ya geç.
        SetPhase(GamePhase.Meta);
    }

    private void OnRequestStartGameplay()
    {
        // Gameplay’in varlığını bilmeden sadece fazı değiştirir.
        SetPhase(GamePhase.Gameplay);
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
}