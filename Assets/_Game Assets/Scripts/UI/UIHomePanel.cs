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

    private CancellationTokenSource _timerCts;
    private double _capSecondsCache = 0.0;

    private void OnEnable()
    {
        _ = RefreshAutopilotPreviewAsync();
    }

    private void OnDisable()
    {
        if (_timerCts != null)
        {
            _timerCts.Cancel();
            _timerCts.Dispose();
            _timerCts = null;
        }
    }

    private async Task RefreshAutopilotPreviewAsync()
    {
        if (autopilotFillImage == null)
            return;

        try
        {
            var status = await AutopilotService.GetStatusAsync();

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
                if (status.isElite)
                {
                    autopilotTimerText.text = "Working...";
                }
                else if (status.isClaimReady || !status.timeToCapSeconds.HasValue || _capSecondsCache <= 0.0)
                {
                    // either ready or we don't have a countdown value
                    autopilotTimerText.text = status.isClaimReady ? "Ready to claim" : "--:--:--";
                }
                else
                {
                    // Start live countdown
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
