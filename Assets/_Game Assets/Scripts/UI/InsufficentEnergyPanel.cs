using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enerji yetersiz olduğunda açılan panel.
/// UserEnergyService üzerinden snapshot çeker, geri sayımı gösterir
/// ve "Get" butonuyla grantBonusEnergy çağırarak +1 enerji vermeyi dener.
/// </summary>
public class InsufficientEnergyPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text currentEnergyText;     // Örn: "0 / 6"
    [SerializeField] private TMP_Text nextLifeTimeText;      // Örn: "01:22:33"
    [SerializeField] private TMP_Text infoText;              // Alt açıklama (opsiyonel)
    [SerializeField] private Button getButton;               // "Get" butonu
    [SerializeField] private Button closeButton;             // Close/X butonu

    [SerializeField] private CanvasGroup canvasGroup;        // Panelin root'u (opsiyonel)
    [SerializeField] private GameObject processingPanel;     // İşlem sırasında açılacak overlay

    [Header("Countdown Settings")]
    [SerializeField] private float countdownRefreshInterval = 1f;

    private UserEnergyService.EnergySnapshot _snapshot;
    private Coroutine _countdownRoutine;
    private bool _isBusy;

    private void OnEnable()
    {
        // Panel açılır açılmaz sunucudan en güncel snapshot'ı çek
        _ = RefreshSnapshotAsync();
    }

    private void OnDisable()
    {
        StopCountdown();
    }

    /// <summary>
    /// Dışarıdan da manuel refresh çağırmak istersen kullanabilirsin.
    /// Örn: başka bir yerden enerji harcandıktan sonra bu panel açıkken.
    /// </summary>
    public async Task RefreshSnapshotAsync()
    {
        SetBusy(true);

        try
        {
            _snapshot = await UserEnergyService.FetchSnapshotAsync();
            ApplySnapshotToUI(_snapshot);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[InsufficientEnergyPanel] FetchSnapshotAsync error: " + ex.Message);
            // Hata durumunda basit bir mesaj yazabilirsin
            if (infoText != null)
                infoText.text = "Unable to load energy status. Please try again.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplySnapshotToUI(UserEnergyService.EnergySnapshot snap)
    {
        // Enerji metni
        if (currentEnergyText != null)
        {
            currentEnergyText.text = $"{snap.current}";
        }

        // Geri sayımı başlat
        StopCountdown();
        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        while (true)
        {
            UpdateCountdownText();
            yield return new WaitForSeconds(countdownRefreshInterval);
        }
    }

    private void StopCountdown()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
    }

    private void UpdateCountdownText()
    {
        if (nextLifeTimeText == null) return;

        if (_snapshot.nextEnergyAtMillis <= 0)
        {
            // Full veya timer yoksa
            nextLifeTimeText.text = "00:00:00";
            return;
        }

        // snapshot.nextEnergyAtMillis epoch ms, cihaz saatiyle farkını al
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long diff = _snapshot.nextEnergyAtMillis - nowMs;

        if (diff <= 0)
        {
            nextLifeTimeText.text = "00:00:00";
            return;
        }

        var remaining = TimeSpan.FromMilliseconds(diff);
        int hours = (int)remaining.TotalHours;
        int minutes = remaining.Minutes;
        int seconds = remaining.Seconds;

        nextLifeTimeText.text = $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;

        if (processingPanel) processingPanel.SetActive(busy);

        if (canvasGroup != null)
        {
            canvasGroup.interactable = !busy;
            canvasGroup.blocksRaycasts = !busy;
            // canvasGroup.alpha = busy ? 0.9f : 1f; // Dimming removed per request
        }

        if (getButton != null)
            getButton.interactable = !busy;

        if (closeButton != null)
            closeButton.interactable = !busy;
    }

    // =======================
    //  BUTTON CALLBACKS
    // =======================

    /// <summary>
    /// "Get" butonu: +1 enerji vermeyi dener (grantBonusEnergy).
    /// Enerji zaten max ise granted=0 dönebilir.
    /// </summary>
    public void OnClickGet()
    {
        if (_isBusy) return;
        _ = GrantBonusEnergyFlowAsync();
    }

    private async Task GrantBonusEnergyFlowAsync()
    {
        SetBusy(true);

        try
        {
            var res = await UserEnergyService.GrantBonusEnergyAsync();
            _snapshot = res;
            ApplySnapshotToUI(_snapshot);

            Debug.Log($"[InsufficientEnergyPanel] grantBonusEnergy => granted={res.granted}, cur={res.current}/{res.max}");

            if (res.granted > 0)
            {
                // Successful grant -> Fade out then close
                await FadeOutAndCloseAsync();

                // 2. Trigger Home Panel refresh
                var homePanel = FindObjectOfType<UIHomePanel>();
                if (homePanel != null)
                {
                    homePanel.Initialize();
                }
            }
            else
            {
                // Full energy
                SetBusy(false); // Re-enable interaction
                if (infoText != null)
                    infoText.text = "Energy is already full.";
            }
        }
        catch (Exception ex)
        {
            SetBusy(false);
            Debug.LogWarning("[InsufficientEnergyPanel] GrantBonusEnergyAsync error: " + ex.Message);
            if (infoText != null)
                infoText.text = "Unable to grant energy. Please try again.";
        }
    }

    /// <summary>
    /// Close/X butonu
    /// </summary>
    public void OnClickClose()
    {
        Close();
    }

    private void Close()
    {
        StopCountdown();
        gameObject.SetActive(false);
    }

    private async Task FadeOutAndCloseAsync()
    {
        if (canvasGroup != null)
        {
            float duration = 0.3f; // UI fade duration
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                await Task.Yield();
            }
            canvasGroup.alpha = 0f;
        }
        // Finally deactivate
        Close();
        // Reset alpha for next time
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        
        // Ensure busy state is reset
        SetBusy(false);
    }
}