using System;
using System.Collections;
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

    private Coroutine _tickCo;
    private UserEnergyService.EnergySnapshot _snap;
    private bool _hasSnap = false;

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

        if (currentEnergyText) currentEnergyText.text = $"{Mathf.Clamp(snap.current, 0, snap.max)}";
        if (maxEnergyText) maxEnergyText.text = $"/{snap.max}";

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

            bool isFull = _snap.current >= _snap.max;

            // Update parent visibility
            // Enerji full ise timer kapanıcak.
            // eğer değil ise timer + lives parent ikisi de açık olacak.
            if (isFull)
            {
                 if (timerParent) timerParent.SetActive(false);
                 // livesParent ile ilgili özel bir istek yoksa, açık kalabilir veya o da logic'e dahil edilebilir.
                 // Ancak istek "eğer değil ise timer + lives parent ikisi de açık olacak" diyor.
                 // "Enerji full ise timer kapanıcak" denmiş sadece. livesParent için bir şey denmemiş full durumda.
                 // Genelde livesParent hep açık kalır, sadece timer gizlenir full ise.
                 // Ama "değil ise timer + lives parent ikisi de açık olacak" ifadesi, livesParent'in de bir şekilde kontrol edildiğini ima edebilir.
                 // Şimdilik sadece timerParent'i kapatıyorum full ise. livesParent'e dokunmuyorum (zaten açıktır varsayımıyla).
                 // Fakat kullanıcının isteği: "enerji full ise timer kapanıcak. eğer değil ise timer + lives parent ikisi de açık olacak."
                 // Bu cümle livesParent'in full iken ne olacağı konusunda net değil ama muhtemelen livesParent zaten hep açık duruyor.
                 // Emin olmak için livesParent.SetActive(true) her zaman diyebiliriz veya full iken ne olmalı sorusu var.
                 // Genelde "Lives" (Can) göstergesi hep görünür. Sadece sayaç (Timer) gizlenir.
                 // Kodda livesParent'i de garantiye alalım.
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