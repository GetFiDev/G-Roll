using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIGameplayLoading : MonoBehaviour
{
    [Header("UI References")]
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;

    [Header("Map Loader (Adapter)")]
    [Tooltip("IMapLoader implementasyonu barındıran MonoBehaviour (örn. MapLoaderRemoteJsonAdapter).")]
    [SerializeField] private MonoBehaviour mapLoaderBehaviour;
    private IMapLoader mapLoader;

    [Header("Tuning")]
    [Range(0.5f, 0.99f)] public float fakeCap = 0.9f;   // hazır olmadan ulaşacağı üst sınır
    public float fakeSpeed = 0.25f;                      // sahte ilerleme hızı
    public float completeDuration = 0.35f;               // hazır olduğunda %100’e akış süresi
    [Tooltip("TRUE ise, bu UI MapLoader.Load() çağrısını başlatır. Yeni mimaride genelde FALSE tutulur; yüklemeyi GameplayManager başlatır.")]
    public bool triggerInitializeHere = false;

    private bool _isCompleting;

    private void Awake()
    {
        mapLoader = mapLoaderBehaviour as IMapLoader;
        if (mapLoader == null && mapLoaderBehaviour != null)
            Debug.LogError("[UIGameplayLoading] mapLoaderBehaviour IMapLoader değil. Lütfen MapLoaderRemoteJsonAdapter verin.");
    }

    private void OnEnable()
    {
        if (loadingBar) loadingBar.value = 0f;
        
        // Reset text only if monitoring (otherwise SetSubmissionMode handles it)
        if (_monitorMapLoader && loadingText) loadingText.text = "Loading...";

        if (_monitorMapLoader && mapLoader != null)
            mapLoader.OnReady += HandleMapsReady;
    }

    private void OnDisable()
    {
        if (mapLoader != null)
            mapLoader.OnReady -= HandleMapsReady;
        _isCompleting = false;
        _monitorMapLoader = true; // Reset for next usage (e.g. next Start Game)
    }

    private bool _monitorMapLoader = true;

    public void SetSubmissionMode(bool isSubmission)
    {
        _monitorMapLoader = !isSubmission;
        if (loadingText)
        {
            loadingText.text = isSubmission ? "Saving..." : "Loading...";
        }
        if (loadingBar && isSubmission)
        {
             // For saving, maybe max out or indeterminate? Let's keep it at current or 100?
             // Or just let it stay at 0 or wherever it is. 
             // Ideally saving is a spinner, but bar is fine.
             loadingBar.value = 1f; 
        }
    }

    private void Update()
    {
        if (mapLoader == null || !_monitorMapLoader) return;

        // Hazır değilse sahte ilerleme ile fakeCap’e yürüsün
        if (!mapLoader.IsReady && !_isCompleting && loadingBar)
        {
            loadingBar.value = Mathf.MoveTowards(loadingBar.value, fakeCap, fakeSpeed * Time.deltaTime);
            if (loadingText)
            {
                int dots = Mathf.FloorToInt(Time.time % 3f) + 1;
                loadingText.text = "Loading" + new string('.', dots);
            }
        }

        // Eğer polling ile hazır olduysa (event bir şekilde kaçarsa) tamamla
        if (mapLoader.IsReady && !_isCompleting)
        {
            HandleMapsReady();
        }
    }

    private void HandleMapsReady()
    {
        if (!isActiveAndEnabled || _isCompleting) return;
        StartCoroutine(CompleteThenHide());
    }

    private System.Collections.IEnumerator CompleteThenHide()
    {
        _isCompleting = true;

        // %100’e akış
        if (loadingBar)
        {
            float t = 0f;
            float start = loadingBar.value;
            while (t < completeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / completeDuration);
                loadingBar.value = Mathf.Lerp(start, 1f, EaseOutCubic(k));
                yield return null;
            }
            loadingBar.value = 1f;
        }
        if (loadingText) loadingText.text = "Ready";

        // Yeni mimaride faz zaten Gameplay; bu UI sadece yükleme overlay'idir.
        // Yükleme biter bitmez kendini kapat.
        gameObject.SetActive(false);
    }

    private static float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    // İstersen buton ile tetiklemek için (çoğu durumda kullanma: GameplayManager yüklemeyi başlatır)
    public void LoadTheGameplay()
    {
        if (!triggerInitializeHere) return;
        if (mapLoader == null) return;
        if (!mapLoader.IsReady)
        {
            mapLoader.Load();
        }
    }
}