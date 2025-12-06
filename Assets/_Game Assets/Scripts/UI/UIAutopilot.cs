#nullable enable
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIAutoPilot : MonoBehaviour
{
    [Header("Root / Panels")]
    [SerializeField] private GameObject fetchingPanel = null!;                 // fetch sırasında aç
    [SerializeField] private GameObject eliteBuyPanel = null!;                 // elite değilse açık

    [Header("Progress UI")]
    [SerializeField] private Slider progressSlider = null!;                    // 0..1
    [SerializeField] private Image  progressFillImage = null!;                 // Slider/Fill Image
    [SerializeField] private TextMeshProUGUI progressLabel = null!;            // bar üstü text
    [SerializeField] private GameObject elitePassProcessBarAnimationObject = null!; // elite çalışırken açık

    [Header("Progress Background")]
    [SerializeField] private Image currentProgressBackgroundImage = null!;   // arka plan image
    [SerializeField] private Sprite normalProgressBackgroundSprite = null!;  // normal arka plan sprite
    [SerializeField] private Sprite eliteProgressBackgroundSprite = null!;   // elite arka plan sprite

    [Header("Action Button")]
    [SerializeField] private Button actionButton = null!;                      // Start / Claim / Disabled
    [SerializeField] private TextMeshProUGUI actionButtonText = null!;
    [SerializeField] private Image actionButtonImage = null!;                  // sprite swap için

    [Header("Claim UI")]
    [SerializeField] private TextMeshProUGUI claimAmountText = null!;        // Claim butonu üzerindeki birikmiş miktar
    [SerializeField] private GameObject claimAmountIcon = null!;             // Claim amount ikonu (UI sprite GO)

    [Header("Main Menu Navigation")]
    [SerializeField] private UIMainMenu mainMenu = null!;                     // Claim sonrası ana menüye dönmek için

    [Header("Sprites")]
    [SerializeField] private Sprite normalProgressFillSprite = null!;          // yeşil
    [SerializeField] private Sprite eliteProgressFillSprite = null!;           // mor
    [SerializeField] private Sprite startButtonSprite = null!;
    [SerializeField] private Sprite claimActiveSprite = null!;
    [SerializeField] private Sprite claimDisabledSprite = null!;
    [SerializeField] private Sprite inProgressSprite = null!;                  // “In Progress…” görseli (opsiyonel)

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI processHeaderText = null!;        // renk + text (elite/normal)
    [SerializeField] private TextMeshProUGUI subheaderText = null!;            // sadece text (elite/normal)
    [SerializeField] private TextMeshProUGUI ratePerDayText = null!;           // "X GET / day"
    [SerializeField] private TextMeshProUGUI promoRatePerDayText = null!;      // Pro tanıtımındaki günlük kazanç metni

    [Header("Header Colors")]
    [SerializeField] private Color normalHeaderColor = new Color(0.24f, 0.73f, 0.36f); // yeşil ton
    [SerializeField] private Color eliteHeaderColor  = new Color(0.56f, 0.32f, 0.80f); // mor ton

    [Header("Text Templates")]
    [SerializeField] private string normalHeaderString   = "Autopilot";
    [SerializeField] private string eliteHeaderString    = "Elite Autopilot";
    [SerializeField] private string normalSubheaderString = "Earn passively up to cap";
    [SerializeField] private string eliteSubheaderString  = "Claim anytime while working";

    // ---- internal state ----
    private CancellationTokenSource? _cts;
    private Coroutine? _countdownCo;
    private AutopilotService.Status? _status;
    private Mode _mode = Mode.None;

    private enum Mode
    {
        None,
        Normal_NotStarted,
        Normal_InProgress,
        Normal_Ready,
        Elite_NotStarted,
        Elite_Working
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        _ = RefreshAsync(); // fire-and-forget
    }

    private void OnDisable()
    {
        CancelCountdown();
        if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
    }

    // ================== Public (UI Hooks) ==================
    public void OnActionButtonClicked()
    {
        Debug.Log($"[UIAutoPilot] Action clicked in mode={_mode}");
        switch (_mode)
        {
            case Mode.Normal_NotStarted:
            case Mode.Elite_NotStarted:
                _ = StartAutopilotAsync();
                break;

            case Mode.Normal_Ready:
            case Mode.Elite_Working:
                _ = ClaimAsync();
                break;

            case Mode.Normal_InProgress:
            default:
                // no-op (disabled zaten)
                break;
        }
    }

    // ================== Core ==================
    private async Task RefreshAsync()
    {
        SetFetching(true);
        CancelCountdown();
        try
        {
            var token = _cts?.Token ?? CancellationToken.None;
            _status = await AutopilotService.GetStatusAsync(token);
            ApplyStatus(_status);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIAutoPilot] Refresh failed: {e}");
        }
        finally
        {
            SetFetching(false);
        }
    }

    private void ApplyStatus(AutopilotService.Status s)
    {
        UpdateHeaderAndSubheader(s);

        // Varsayılan: claim amount gizli (boş)
        SetClaimAmountText(string.Empty);

        // Elite satın alma paneli
        if (eliteBuyPanel) eliteBuyPanel.SetActive(!s.isElite);

        // Progress fill sprite (elite/normal)
        if (progressFillImage)
            progressFillImage.sprite = s.isElite ? eliteProgressFillSprite : normalProgressFillSprite;

        // Progress background sprite (elite/normal)
        if (currentProgressBackgroundImage)
            currentProgressBackgroundImage.sprite = s.isElite ? eliteProgressBackgroundSprite : normalProgressBackgroundSprite;

        // Elite anim objesi default kapalı
        if (elitePassProcessBarAnimationObject)
            elitePassProcessBarAnimationObject.SetActive(false);

        // Mode belirle
        if (!s.isElite)
        {
            if (!s.isAutopilotOn)
                _mode = Mode.Normal_NotStarted;
            else if (s.isClaimReady)
                _mode = Mode.Normal_Ready;
            else
                _mode = Mode.Normal_InProgress;
        }
        else
        {
            // Elite: Start şartı var (isAutopilotOn=false ise normal start ekranı)
            _mode = s.isAutopilotOn ? Mode.Elite_Working : Mode.Elite_NotStarted;
        }

        // UI yaz
        switch (_mode)
        {
            case Mode.Normal_NotStarted:
                SetProgress(0f);
                SetProgressLabel("Waiting to start...");
                SetupButton(startButtonSprite, true, "Start");
                break;

            case Mode.Normal_InProgress:
                {
                    // kalan süreyi göster: HH:MM:SS
                    var remaining = Math.Max(0, (int)(s.timeToCapSeconds ?? 0));
                    var totalCapSec = (int)(s.normalUserMaxAutopilotDurationInHours * 3600);
                    var progress = totalCapSec > 0
                        ? Mathf.Clamp01(1f - (remaining / (float)totalCapSec))
                        : 0f;

                    SetProgress(progress);
                    SetProgressLabel(FormatHms(remaining));
                    SetupButton(inProgressSprite ? inProgressSprite : claimDisabledSprite, false, "In Progress...");

                    // canlı geri sayım
                    _countdownCo = StartCoroutine(CoCountdownAndAutoRefresh(remaining, totalCapSec));
                }
                break;

            case Mode.Normal_Ready:
                SetProgress(1f);
                SetProgressLabel("Ready to claim");
                SetupButton(claimActiveSprite, true, "Claim");
                SetClaimAmountWithIcon(s.autopilotWallet);
                break;

            case Mode.Elite_NotStarted:
                SetProgress(0f);
                SetProgressLabel("Waiting to start...");
                SetupButton(startButtonSprite, true, "Start");
                break;

            case Mode.Elite_Working:
                SetProgress(1f);
                SetProgressLabel("Working...");
                if (elitePassProcessBarAnimationObject)
                    elitePassProcessBarAnimationObject.SetActive(true);
                SetupButton(claimActiveSprite, true, "Claim");
                SetClaimAmountWithIcon(s.autopilotWallet);
                break;
        }
        Debug.Log($"[UIAutoPilot] ApplyStatus => isElite={s.isElite}, isOn={s.isAutopilotOn}, ready={s.isClaimReady}, mode={_mode}");
    }

    // ================== Actions ==================
    private async Task StartAutopilotAsync()
    {
        SetFetching(true);
        try
        {
            var token = _cts?.Token ?? CancellationToken.None;
            var res = await AutopilotService.ToggleAsync(true, token);

            // Log full result for diagnosis
            Debug.Log($"[UIAutoPilot] Toggle ON result => ok={res.ok}");

            if (res.ok)
            {
                CancelCountdown();
                // Optimistic UI: anında çalışıyor gibi göster, ardından teyit için refresh
                if (_status != null)
                {
                    _status.isAutopilotOn = true; // local state
                    _status.isClaimReady = false;
                    // Eğer normal kullanıcıysa ve daha önce başlamamışsa, optimistik bir başlangıç geri sayımı göster
                    if (!_status.isElite)
                    {
                        var totalCapSec = (int)Mathf.Max(0f, (float)(_status.normalUserMaxAutopilotDurationInHours * 3600.0));
                        if (!_status.timeToCapSeconds.HasValue || _status.timeToCapSeconds.Value <= 0)
                            _status.timeToCapSeconds = totalCapSec;
                    }
                    ApplyStatus(_status);
                }

                // Bazı backend'lerde status güncellemesi eventual olabilir; kısa bir gecikme sonra yenile
                await Task.Delay(500, token);
                await RefreshAsync();
            }
            else
            {
                // Başlatılamadıysa kullanıcıya görsel geri bildirim ver ve mevcut durumu tazele
                SetupButton(claimDisabledSprite, true, "Retry Start");
                SetClaimAmountText(string.Empty);
                Debug.LogWarning("[UIAutoPilot] Toggle ON returned ok=false; refreshing status.");
                await RefreshAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIAutoPilot] Start failed: {e}");
            SetFetching(false);
        }
    }

    private async Task ClaimAsync()
    {
        SetFetching(true);
        try
        {
            var token = _cts?.Token ?? CancellationToken.None;
            var res = await AutopilotService.ClaimAsync(token);
            // İstersen burada “+res.claimed” animasyonu tetikle

            // Sunucudan güncel durumu çek (cüzdan, autopilot state vs.)
            await RefreshAsync();

            // Claim başarıyla tamamlandıktan sonra ana menü akışını tetikle
            NavigateBackToMainMenuAfterClaim();
        }
        catch (AutopilotService.NotReadyToClaimException)
        {
            // Normal kullanıcı 12 saat dolmadan bastı → sadece yenile, paneli kapatma
            await RefreshAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIAutoPilot] Claim failed: {e}");
            SetFetching(false);
        }
    }

    // ================== Header/Subheader/Rate ==================
    private void UpdateHeaderAndSubheader(AutopilotService.Status s)
    {
        if (processHeaderText)
        {
            processHeaderText.color = s.isElite ? eliteHeaderColor : normalHeaderColor;
            processHeaderText.text  = s.isElite ? eliteHeaderString : normalHeaderString;
        }

        if (subheaderText)
        {
            subheaderText.text = s.isElite ? eliteSubheaderString : normalSubheaderString;
        }

        // Server rate'leri saatlik; UI'da "GET / day" istendiği için 24 ile çarpıyoruz.
        double perHour = s.isElite ? s.eliteUserEarningPerHour : s.normalUserEarningPerHour;
        double perDay = Math.Max(0.0, perHour * 24.0);
        double eliteEarningPerDay = Math.Max(0.0, s.eliteUserEarningPerHour * 24.0);
        if (ratePerDayText)
        {
            ratePerDayText.text = $"{FormatCompactNumber(perDay)} GET / day";
        }
        if (promoRatePerDayText)
        {
            promoRatePerDayText.text = $"{FormatCompactNumber(eliteEarningPerDay)} GET / day";
        }
    }

    // Basit sayı formatlayıcı: tam sayı ise .0 at; değilse en fazla 2 ondalık.
    private string FormatCompactNumber(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
        double rounded2 = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded2 - Math.Round(rounded2)) < 1e-9)
            return Math.Round(rounded2).ToString();
        return rounded2.ToString("0.##");
    }

    // ================== Helpers ==================
    private void NavigateBackToMainMenuAfterClaim()
    {
        // Önce ana menüdeki autopilot info akışını tetikle (panel kapanır)
        if (mainMenu != null)
        {
            try
            {
                mainMenu.OnAutoPilotInfoButtonClick();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIAutoPilot] Error calling mainMenu.OnAutoPilotInfoButtonClick: {e}");
            }
        }

        // Ardından üst bar (currency vs.) için refresh çağır
        try
        {
            if (UITopPanel.Instance != null)
            {
                UITopPanel.Instance.Initialize();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIAutoPilot] Error refreshing UITopPanel: {e}");
        }
    }

    private void SetFetching(bool v)
    {
        if (fetchingPanel) fetchingPanel.SetActive(v);
        // Buton etkileşimini burada değiştirmiyoruz; her durumda ApplyStatus/SetupButton belirleyecek.
    }

    private void SetProgress(float t01)
    {
        if (progressSlider)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = Mathf.Clamp01(t01);
        }
    }

    private void SetProgressLabel(string text)
    {
        if (progressLabel) progressLabel.text = text ?? "";
    }

    private void SetupButton(Sprite sprite, bool interactable, string label)
    {
        if (actionButtonImage && sprite) actionButtonImage.sprite = sprite;
        if (actionButton) actionButton.interactable = interactable;
        if (actionButtonText)
        {
            actionButtonText.text = label ?? "";

            // Claim edilebilir durumda text'i yukarı kaydır
            float targetY = (interactable && label == "Claim") ? 21.468f : 1f;

            var rt = actionButtonText.rectTransform;
            Vector3 pos = rt.localPosition;
            pos.y = targetY;
            rt.localPosition = pos;
        }
    }

    private void SetClaimAmountText(string text)
    {
        string safe = text ?? string.Empty;
        if (claimAmountText)
            claimAmountText.text = safe;

        // Icon sadece görünür bir amount varken açık olsun
        if (claimAmountIcon)
            claimAmountIcon.SetActive(!string.IsNullOrEmpty(safe));
    }

    private void SetClaimAmountWithIcon(object amount)
    {
        // amount null ise tamamen gizle
        if (amount == null)
        {
            SetClaimAmountText(string.Empty);
            return;
        }

        // Her durumda (0.0, 0.3, 1.0, 12.5 vs.) tek ondalık gösterelim.
        double value;
        string raw = amount.ToString();

        // Önce invariant kültür, sonra sistem kültürü ile parse etmeyi dene
        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
            {
                // Parse edemezsek fallback olarak raw string'i yaz
                SetClaimAmountText(raw);
                return;
            }
        }

        // Çift ondalık format (0.00 şeklinde) — 0.00 da dahil hepsini göstereceğiz
        string formatted = value.ToString("0.00", CultureInfo.InvariantCulture);
        SetClaimAmountText(formatted);
    }

    private string FormatHms(int totalSeconds)
    {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return $"{h:00}:{m:00}:{s:00}";
    }

    private void CancelCountdown()
    {
        if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }
    }

    private IEnumerator CoCountdownAndAutoRefresh(int startRemainingSec, int totalCapSec)
    {
        int remaining = Mathf.Max(0, startRemainingSec);
        while (remaining > 0)
        {
            yield return new WaitForSeconds(1f);
            remaining = Mathf.Max(0, remaining - 1);

            // label güncelle
            SetProgressLabel(FormatHms(remaining));

            // slider (yaklaşık) güncelle
            if (totalCapSec > 0)
            {
                float p = 1f - (remaining / (float)totalCapSec);
                SetProgress(p);
            }
        }

        // Süre doldu → yeniden çek (Ready state’e geçsin)
        _countdownCo = null;
        if (isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            _ = RefreshAsync();
        }
    }
}