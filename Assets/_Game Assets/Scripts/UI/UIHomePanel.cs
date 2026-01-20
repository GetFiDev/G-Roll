using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Threading;

public class UIHomePanel : MonoBehaviour
{
    [Header("Autopilot Preview")]
    [SerializeField] private Image autopilotFillImage;   // progress bar fill
    [SerializeField] private TextMeshProUGUI autopilotTimerText; // optional UI text
    [Header("Referral Earnings")]
    [SerializeField] private UIReferralEarningsPanel referralEarningsPanel;

    [Header("Play Button Overlay")]
    [SerializeField] private GameObject playButtonLoadingOverlay; // Legacy fallback
    [SerializeField] private GameObject endlessLoadingOverlay;
    [SerializeField] private GameObject chapterLoadingOverlay;

    private CancellationTokenSource _timerCts;
    private double _capSecondsCache = 0.0;
    private bool _isRequestingSession = false; // Fail-safe lock

    private void OnEnable()
    {
        UIAutoPilot.OnClosed += OnAutopilotClosed;
        
        // Safety check: Only initialize if authenticated.
        // This prevents the "Auth Required" errors if the panel is mistakenly active during boot.
        if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.IsAuthenticated())
        {
            Initialize();
        }
    }

    [Header("Game Modes")]
    [SerializeField] private Button endlessButton;
    [SerializeField] private Button chapterButton;
    [SerializeField] private TextMeshProUGUI chapterButtonText;
    [SerializeField] private GameObject chapterComingSoonOverlay;

    public void Initialize()
    {
        // Reset lock on initialize
        _isRequestingSession = false;
        
        // 1. Refresh Top Panel
        if (UITopPanel.Instance != null)
        {
            UITopPanel.Instance.Initialize();
        }

        // 2. Refresh Player Stats
        if (UIPlayerStatsHandler.Instance != null)
        {
            UIPlayerStatsHandler.Instance.Refresh();
        }

        // 3. Refresh Energy Display (in children)
        var energyDisplay = GetComponentInChildren<UIEnergyDisplay>(true);
        if (energyDisplay != null)
        {
            energyDisplay.RefreshNow();
        }

        // 4. Refresh Build Zone Grid (in children)
        var gridDisplay = GetComponentInChildren<BuildZoneGridDisplay>(true);
        if (gridDisplay != null)
        {
            gridDisplay.Refresh();
        }

        // 5. Refresh Autopilot Preview (local async)
        _ = RefreshAutopilotPreviewAsync();

        // 6. Update Chapter Button state
        _ = UpdateChapterButtonStateAsync();

        // 7. Check for Pending Referrals
        _ = CheckPendingReferralsAsync();
    }

    private async Task CheckPendingReferralsAsync()
    {
        if (referralEarningsPanel == null)
        {
             Debug.LogWarning("[UIHomePanel] referralEarningsPanel reference is MISSING in Inspector!");
             return;
        }

        var res = await ReferralRemoteService.GetPendingReferrals();
        Debug.Log($"[UIHomePanel] CheckPending: hasPending={res.hasPending}, total={res.total}");

        if (res.hasPending && res.total > 0)
        {
            referralEarningsPanel.Open((float)res.total, async () => 
            {
                if (UserDatabaseManager.Instance != null)
                {
                    await UserDatabaseManager.Instance.ClaimReferralEarningsAsync();
                    
                    // Refresh Top Panel to show new currency balance
                    if (UITopPanel.Instance != null)
                    {
                        UITopPanel.Instance.Initialize();
                    }
                }
            });
        }
    }

    private async Task UpdateChapterButtonStateAsync()
    {
        if (chapterButton == null) return;

        // Default safe state
        chapterButton.interactable = false;
        if (chapterComingSoonOverlay) chapterComingSoonOverlay.SetActive(false);

        if (UserDatabaseManager.Instance == null) return;

        // Ensure user data is loaded
        var userData = await UserDatabaseManager.Instance.LoadUserData();
        if (userData == null)
        {
            return;
        }

        int currentChapter = userData.chapterProgress;
        bool exists = await UserDatabaseManager.Instance.CheckIfChapterExists(currentChapter);

        if (exists)
        {
            chapterButton.interactable = true;
            if (chapterComingSoonOverlay) chapterComingSoonOverlay.SetActive(false);
        }
        else
        {
            chapterButton.interactable = false;
            if (chapterComingSoonOverlay) chapterComingSoonOverlay.SetActive(true);
        }
    }

    // Retained for backward compatibility or direct linking if needed, but redirects to endless
    public void OnPlayButtonClicked() => OnEndlessButtonClicked();

    public async void OnEndlessButtonClicked()
    {
        // Fail-safe: prevent double clicks
        if (_isRequestingSession)
        {
            Debug.LogWarning("[UIHomePanel] Session request already in progress.");
            return;
        }
        
        _isRequestingSession = true;
        
        // Disable buttons while loading
        if (endlessButton) endlessButton.interactable = false;
        if (chapterButton) chapterButton.interactable = false;
        
        // Show overlay (prefer specific, fallback to legacy)
        var overlay = endlessLoadingOverlay != null ? endlessLoadingOverlay : playButtonLoadingOverlay;
        if (overlay) overlay.SetActive(true);
        
        try
        {
            if (GameManager.Instance)
            {
                await GameManager.Instance.RequestSessionAndStartAsync(GameMode.Endless);
            }
        }
        finally
        {
            // Cleanup - always execute even if exception
            if (overlay) overlay.SetActive(false);
            if (endlessButton) endlessButton.interactable = true;
            // Chapter button will be refreshed by Initialize on panel re-enable
            _isRequestingSession = false;
        }
    }

    public async void OnChapterButtonClicked()
    {
        // Fail-safe: prevent double clicks
        if (_isRequestingSession)
        {
            Debug.LogWarning("[UIHomePanel] Session request already in progress.");
            return;
        }
        
        _isRequestingSession = true;
        
        // Disable buttons while loading
        if (endlessButton) endlessButton.interactable = false;
        if (chapterButton) chapterButton.interactable = false;
        
        // Show overlay (prefer specific, fallback to legacy)
        var overlay = chapterLoadingOverlay != null ? chapterLoadingOverlay : playButtonLoadingOverlay;
        if (overlay) overlay.SetActive(true);
        
        try
        {
            if (GameManager.Instance)
            {
                await GameManager.Instance.RequestSessionAndStartAsync(GameMode.Chapter);
            }
        }
        finally
        {
            // Cleanup - always execute even if exception
            if (overlay) overlay.SetActive(false);
            if (endlessButton) endlessButton.interactable = true;
            // Chapter button will be refreshed by Initialize on panel re-enable
            _isRequestingSession = false;
        }
    }


    private void OnDisable()
    {
        UIAutoPilot.OnClosed -= OnAutopilotClosed;
        if (_timerCts != null)
        {
            _timerCts.Cancel();
            _timerCts.Dispose();
            _timerCts = null;
        }
    }

    private void OnAutopilotClosed()
    {
        // Refresh things that might have changed after interacting with Autopilot Panel
        Initialize(); 
    }

    private async Task RefreshAutopilotPreviewAsync()
    {
        if (autopilotFillImage == null)
            return;

        try
        {
            var status = await AutopilotService.GetStatusAsync();

            // Safety check: if no activation date, force off
            if (status.autopilotActivationDateMillis == null || status.autopilotActivationDateMillis <= 0)
            {
                status.isAutopilotOn = false;
                status.timeToCapSeconds = null;
            }

            // Elite pass badge logic removed as requested


            double perHour = status.isElite ? status.eliteUserEarningPerHour : status.normalUserEarningPerHour;
            double maxHours = status.normalUserMaxAutopilotDurationInHours;
            double capSeconds = maxHours * 3600.0;
            _capSecondsCache = capSeconds;

            // Progress calculation
            float progress01 = 0f;
            if (status.isElite)
            {
                // Elite = Always full bar
                progress01 = 1f;
            }
            else if (!status.isAutopilotOn)
            {
                // Not started
                progress01 = 0f;
            }
            else
            {
                if (status.timeToCapSeconds.HasValue)
                {
                    double remaining = status.timeToCapSeconds.Value;
                    double elapsed = Math.Max(0, capSeconds - remaining);
                    progress01 = (float)Mathf.Clamp01((float)(elapsed / capSeconds));
                }
            }

            // Cancel any previous countdown
            if (_timerCts != null)
            {
                _timerCts.Cancel();
                _timerCts.Dispose();
                _timerCts = null;
            }

            autopilotFillImage.fillAmount = progress01;

            if (autopilotTimerText)
            {
                // Varsayılan olarak boş metin (aktif süreç yoksa hiçbir şey yazmayacağız)
                autopilotTimerText.text = string.Empty;

                if (status.isClaimReady)
                {
                    // Claim edilebilir durumda her zaman text göster
                    autopilotTimerText.text = "";
                }
                else if (status.isAutopilotOn && status.timeToCapSeconds.HasValue && _capSecondsCache > 0.0)
                {
                    // Sadece aktif bir autopilot süreci varsa geri sayım başlat
                    _timerCts = new CancellationTokenSource();
                    _ = StartCountdownAsync(status.timeToCapSeconds.Value, _timerCts.Token);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIHomePanel] Failed to refresh autopilot preview: {e.Message}");
        }
    }

    private async Task StartCountdownAsync(double startRemainingSeconds, CancellationToken ct)
    {
        double remaining = Math.Max(0.0, startRemainingSeconds);
        const int tickMs = 250;
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        while (remaining > 0.0 && !ct.IsCancellationRequested)
        {
            // Update timer text
            if (autopilotTimerText)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                autopilotTimerText.text = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            }

            // Update fill based on elapsed vs cap
            if (autopilotFillImage && _capSecondsCache > 0.0)
            {
                double elapsed = Math.Max(0.0, _capSecondsCache - remaining);
                float progress01 = (float)Mathf.Clamp01((float)(elapsed / _capSecondsCache));
                autopilotFillImage.fillAmount = progress01;
            }

            try { await Task.Delay(tickMs, ct); } catch { break; }

            remaining = Math.Max(0.0, startRemainingSeconds - sw.Elapsed.TotalSeconds);
        }

        // Finalize state at zero (if not cancelled)
        if (!ct.IsCancellationRequested)
        {
            if (autopilotFillImage && _capSecondsCache > 0.0)
                autopilotFillImage.fillAmount = 1f;

            if (autopilotTimerText)
                autopilotTimerText.text = "Ready to claim";
        }
    }


}
