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
    [Header("Play Button Overlay")]
    [SerializeField] private GameObject playButtonLoadingOverlay;

    private CancellationTokenSource _timerCts;
    private double _capSecondsCache = 0.0;

    private void OnEnable()
    {
        UIAutoPilot.OnClosed += OnAutopilotClosed;
        Initialize();
    }

    [Header("Game Modes")]
    [SerializeField] private Button endlessButton;
    [SerializeField] private Button chapterButton;
    [SerializeField] private TextMeshProUGUI chapterButtonText;

    public void Initialize()
    {
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
    }

    private async Task UpdateChapterButtonStateAsync()
    {
        if (chapterButton == null) return;
        
        // Default safe state
        chapterButton.interactable = false;
        if (chapterButtonText) chapterButtonText.text = "Loading...";

        if (UserDatabaseManager.Instance == null) return;

        // Ensure user data is loaded
        var userData = await UserDatabaseManager.Instance.LoadUserData();
        if (userData == null)
        {
            if (chapterButtonText) chapterButtonText.text = "Error";
            return;
        }

        int currentChapter = userData.chapterProgress;
        bool exists = await UserDatabaseManager.Instance.CheckIfChapterExists(currentChapter);

        if (exists)
        {
            chapterButton.interactable = true;
            if (chapterButtonText) chapterButtonText.text = $"Chapter {currentChapter}";
        }
        else
        {
            chapterButton.interactable = false;
            // "Chapter X - Coming Soon" might be too long for button, maybe just "Coming Soon" or multiline
            if (chapterButtonText) chapterButtonText.text = $"Chapter {currentChapter}\nComing Soon";
        }
    }

    // Retained for backward compatibility or direct linking if needed, but redirects to endless
    public void OnPlayButtonClicked() => OnEndlessButtonClicked();

    public async void OnEndlessButtonClicked()
    {
        if (playButtonLoadingOverlay) playButtonLoadingOverlay.SetActive(true);
        if (GameManager.Instance)
        {
            await GameManager.Instance.RequestSessionAndStartAsync(GameMode.Endless);
        }
        if (playButtonLoadingOverlay) playButtonLoadingOverlay.SetActive(false);
    }

    public async void OnChapterButtonClicked()
    {
        if (playButtonLoadingOverlay) playButtonLoadingOverlay.SetActive(true);
        if (GameManager.Instance)
        {
            await GameManager.Instance.RequestSessionAndStartAsync(GameMode.Chapter);
        }
        if (playButtonLoadingOverlay) playButtonLoadingOverlay.SetActive(false);
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
