using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class UIEnergyDisplay : MonoBehaviour
{
    [Header("Refs")]

    [SerializeField] private TMP_Text energyText;   // “1/6”
    [SerializeField] private TMP_Text timerText;    // “03:59:59” veya “--:--”

    private Coroutine _tickCo;
    private UserEnergyService.EnergySnapshot _snap;
    private bool _hasSnap = false;

    private void OnEnable()
    {
        StartTickerIfNeeded();
        RefreshNow();
    }

    private void OnDisable()
    {
        if (_tickCo != null) { StopCoroutine(_tickCo); _tickCo = null; }
    }

    public async void RefreshNow()
    {
        try
        {
            var snap = await UserEnergyService.FetchSnapshotAsync();
            UpdateFromSnapshot(snap);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UIEnergyDisplay] Refresh error: {ex.Message}");
            if (energyText) energyText.text = "--/--";
            if (timerText)  timerText.text  = "--:--";
        }
    }

    private void UpdateFromSnapshot(UserEnergyService.EnergySnapshot snap)
    {
        _snap = snap;
        _hasSnap = true;

        if (energyText) energyText.text = $"{Mathf.Clamp(snap.current, 0, snap.max)}/{snap.max}";

        StartTickerIfNeeded();
    }

    private void StartTickerIfNeeded()
    {
        if (!isActiveAndEnabled) return;
        if (!_hasSnap) return;
        if (_tickCo != null) StopCoroutine(_tickCo);
        _tickCo = StartCoroutine(TickCountdown());
    }

    private IEnumerator TickCountdown()
    {
        while (true)
        {
            if (!_hasSnap || timerText == null)
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            if (_snap.current >= _snap.max || _snap.nextEnergyAtMillis <= 0)
            {
                timerText.text = "--:--";
            }
            else
            {
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long remain = _snap.nextEnergyAtMillis - nowMs;

                if (remain <= 0)
                {
                    // Bir can dolmuş demektir; manager bir sonraki snapshot’ta güncel değerleri verecek.
                    timerText.text = "00:00";
                }
                else
                {
                    var ts = TimeSpan.FromMilliseconds(remain);
                    string hh = Math.Floor(ts.TotalHours).ToString("0");
                    timerText.text = $"{hh}:{ts.Minutes:00}:{ts.Seconds:00}";
                }
            }

            yield return new WaitForSeconds(0.25f);
        }
    }
}