using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RemoteApp;

public class UIGameplayLoading : MonoBehaviour
{
    [Header("UI References")]
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;
    public MapManager mapManager;

    [Header("Tuning")]
    [Range(0.5f, 0.99f)] public float fakeCap = 0.9f;   // hazır olmadan ulaşacağı üst sınır
    public float fakeSpeed = 0.25f;                      // sahte ilerleme hızı
    public float completeDuration = 0.35f;               // hazır olduğunda %100’e akış süresi
    public bool triggerInitializeHere = true;            // MapManager.Initialize’ı buradan başlat

    private bool _isCompleting;

    private void OnEnable()
    {
        if (loadingBar) loadingBar.value = 0f;
        if (loadingText) loadingText.text = "Loading…";

        if (mapManager != null)
            mapManager.OnReady += HandleMapsReady;
    }

    private void OnDisable()
    {
        if (mapManager != null)
            mapManager.OnReady -= HandleMapsReady;
        _isCompleting = false;
    }

    private void Update()
    {
        // Hazır değilse sahte ilerleme ile fakeCap’e doğru yürüsün
        if (mapManager != null && !mapManager.isReady && !_isCompleting && loadingBar)
        {
            loadingBar.value = Mathf.MoveTowards(loadingBar.value, fakeCap, fakeSpeed * Time.deltaTime);
            if (loadingText)
            {
                int dots = Mathf.FloorToInt(Time.time % 3f) + 1;
                loadingText.text = "Loading" + new string('.', dots);
            }
        }

        // Eğer polling ile hazır olduysa (event bir şekilde kaçarsa) tamamla
        if (mapManager != null && mapManager.isReady && !_isCompleting)
        {
            HandleMapsReady();
        }
    }

    private void HandleMapsReady()
    {
        if (!isActiveAndEnabled || _isCompleting) return;
        StartCoroutine(CompleteThenEnterGameplay());
    }

    private System.Collections.IEnumerator CompleteThenEnterGameplay()
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

        // KRİTİK: UI’yı kapatma! Sadece GameState’i “GameplayRun” yap.
        // UIManager zaten state değişimini dinliyor ve loading UI’ını gizleyecek.
        GameManager.Instance.EnterGameplayRun(); // ↓ Not: #3’e bak

        // Burada hiçbir SetActive(false) veya fade yapmıyoruz!
    }

    private static float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    // İstersen buton ile tetiklemek için:
    public void LoadTheGameplay()
    {
        if (triggerInitializeHere && mapManager != null && !mapManager.isReady)
        {
            _ = mapManager.Initialize();
        }
    }
}