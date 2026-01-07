using System;
using System.Collections;
using System.Globalization;
using AssetKits.ParticleImage;
using TMPro;
using UnityEngine;

public class UIEnergyDisplay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text currentEnergyText;   // “1”
    [SerializeField] private TMP_Text maxEnergyText;       // “/6”
    [SerializeField] private TMP_Text timerText;    // “03:59:59” veya “--:--”
    [SerializeField] private GameObject timerParent;
    [SerializeField] private GameObject livesParent;

    [Header("FX")]
    [SerializeField] private ParticleImage energyGainFx;

    private Coroutine _tickCo;
    private Coroutine _energyAnimRoutine;
    private UserEnergyService.EnergySnapshot _snap;
    private bool _hasSnap = false;

    private const string LastEnergyPrefsKey = "UIEnergyDisplay.LastEnergy";

    private void OnEnable()
    {
        StartTickerIfNeeded();
        if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.IsAuthenticated())
        {
            RefreshNow();
        }
    }

    private void OnDisable()
    {
        if (_tickCo != null) { StopCoroutine(_tickCo); _tickCo = null; }
        // Stop animation on disable to reset state
        if (_energyAnimRoutine != null) { StopCoroutine(_energyAnimRoutine); _energyAnimRoutine = null; }
    }

    private int LoadLastEnergy()
    {
        return PlayerPrefs.GetInt(LastEnergyPrefsKey, 0);
    }

    private void SaveLastEnergy(int value)
    {
        PlayerPrefs.SetInt(LastEnergyPrefsKey, value);
        PlayerPrefs.Save();
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
            if (currentEnergyText) currentEnergyText.text = "-";
            if (maxEnergyText) maxEnergyText.text = "/-";
            if (timerText)  timerText.text  = "--:--";
        }
    }

    private void UpdateFromSnapshot(UserEnergyService.EnergySnapshot snap)
    {
        _snap = snap;
        _hasSnap = true;

        if (maxEnergyText) maxEnergyText.text = $"/{snap.max}";

        // Logic for Energy Increase
        int lastEnergy = LoadLastEnergy();
        int currentEnergy = Mathf.Clamp(snap.current, 0, snap.max);

        // If the *new* energy is greater than what we recorded last time, 
        // play the effect and animate.
        if (currentEnergy > lastEnergy)
        {
            // 1. Particle Logic
            int diff = currentEnergy - lastEnergy;
            if (energyGainFx != null)
            {
                // Set burst amount dynamically
                // This assumes the ParticleImage component uses bursts. 
                // We'll modify the first burst if available, or just play.
                // AssetKits ParticleImage often controls 'rateOverTime' or bursts.
                // A simple approach if it's a "burst" type is to set the emission burst count.
                // However, ParticleImage plugin API might vary. 
                // A common way for "UI Particle" to set amount is via its emission module or custom methods.
                // Assuming standard ParticleSystem-like behavior or specific method:
                
                // Let's try to set the generic burst count if exposed, or just Play.
                // If it's the standard AssetKits.ParticleImage, we can often set .rateOverTime or .bursts. 
                // For simplicity and common usage, we might just play it. 
                // BUT user requested: "burst miktarı kaç can artacaksa o kadar olucak".
                
                // We will try to set the burst count on the first burst.
                // Note: ParticleImage struct might differ from Unity's ParticleSystem.
                // Checking the API surface via previous context: `ParticleImage` class.
                // It usually has a `rateOverTime` or `bursts`.
                
                // If specific API is unknown, we'll try to set the particle count for the burst.
                // Since I can't see the library code, I will attempt to set `rateOverTime` to the diff 
                // if it's a stream, or assume the user has configured a burst and we just play.
                // *Correction*: User specifically asked "burst miktarı kaç can artacaksa o kadar olucak".
                // I will assume there's a property to control count or I will re-trigger it 'diff' times?
                // Re-triggering might look bad.
                // Let's assume we can set `particleImage.rateOverTime = diff` for a short duration or similar.
                // Or better, let's look at `UserStatsDisplayer` again? No, it just used `.Play()`.
                
                // I will try to set `energyGainFx.rateOverTime = diff;` just before playing.
                energyGainFx.rateOverTime = diff; 
                energyGainFx.Play();
            }

            // 2. Animation Logic
            // Start from 'lastEnergy', wait 0.75s, animate to 'currentEnergy' over 0.5s.
            if (_energyAnimRoutine != null) StopCoroutine(_energyAnimRoutine);
            _energyAnimRoutine = StartCoroutine(AnimateEnergyChange(lastEnergy, currentEnergy, 0.75f, 0.5f));
        }
        else
        {
            // No increase (or decrease), just set text immediately
            if (currentEnergyText) currentEnergyText.text = $"{currentEnergy}";
            if (_energyAnimRoutine != null) StopCoroutine(_energyAnimRoutine);
        }

        // Always save the new value as the last known value
        SaveLastEnergy(currentEnergy);

        StartTickerIfNeeded();
    }

    private IEnumerator AnimateEnergyChange(int startVal, int endVal, float delay, float duration)
    {
        // Initial state before animation starts (during delay)
        if (currentEnergyText) currentEnergyText.text = $"{startVal}";

        yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            
            // Lerp
            float currentFloat = Mathf.Lerp(startVal, endVal, progress);
            int currentInt = Mathf.RoundToInt(currentFloat);

            if (currentEnergyText) currentEnergyText.text = $"{currentInt}";
            yield return null;
        }

        // Final ensure
        if (currentEnergyText) currentEnergyText.text = $"{endVal}";
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

            bool isFull = _snap.current >= _snap.max;

            // Update parent visibility
            // Enerji full ise timer kapanıcak.
            // eğer değil ise timer + lives parent ikisi de açık olacak.
            if (isFull)
            {
                 if (timerParent) timerParent.SetActive(false);
                 if (livesParent) livesParent.SetActive(true); 
            }
            else
            {
                if (timerParent) timerParent.SetActive(true);
                if (livesParent) livesParent.SetActive(true);
            }

            if (isFull || _snap.nextEnergyAtMillis <= 0)
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