using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ElitePass ile ilgili TÜM akışların geçtiği tek kapı:
/// - Satın alma: StartPurchase()
/// - Kontrol + koşullu aksiyon: RequireEliteThenRun(...)
/// Arka planda ElitePassService (Cloud Functions) kullanır.
/// </summary>
public class ElitePassValidator : MonoBehaviour
{
    [Header("Services")]
    public ElitePassService eliteService;     // Firebase Functions wrapper (PurchaseAsync / CheckAsync)

    [Header("Purchase UI (optional)")]
    public GameObject purchasingPanel;        // "Processing..." overlay
    public GameObject purchaseCompletedPanel; // "Completed" panel
    public TMP_Text   purchaseExpiresTMP;     // bitiş tarihi göstermek istersen
    public Button     purchaseButton;         // satın alma tuşu (busy iken disable)

    [Header("Gate UI (optional)")]
    public GameObject notEligiblePanel;       // ElitePass yoksa göstereceğin panel (örn. "Elite gerekli")

    public bool IsBusy { get; private set; }

    // --- Events ---
    public event Action OnPurchaseStarted;
    public event Action<DateTime?> OnPurchaseSucceeded; // expiresAtUtc
    public event Action<string> OnPurchaseFailed;

    public event Action<DateTime?> OnCheckPassed;
    public event Action<string> OnCheckFailed;

    void Awake()
    {
        if (!eliteService)
            Debug.LogWarning("[ElitePassValidator] eliteService missing.");
    }

    // UI Button → Satın alma başlat
    public void OnPurchaseButton()
    {
        _ = StartPurchase();
    }

    /// <summary>
    /// Satın alma akışı: UI panellerini yönetir, sunucudan 30 gün ekletir.
    /// </summary>
    public async Task<bool> StartPurchase(string purchaseId = null)
    {
        if (eliteService == null || IsBusy) return false;

        try
        {
            SetBusy(true);
            Show(purchasingPanel, true);
            Show(purchaseCompletedPanel, false);

            OnPurchaseStarted?.Invoke();

            // idempotency için rastgele id (UI’dan aynı tuşa iki kez basılırsa dahi güvenli)
            if (string.IsNullOrEmpty(purchaseId))
                purchaseId = Guid.NewGuid().ToString("N");

            var (active, expiresAtUtc) = await eliteService.PurchaseAsync(purchaseId);

            Show(purchasingPanel, false);
            Show(purchaseCompletedPanel, true);

            if (purchaseExpiresTMP && expiresAtUtc.HasValue)
                purchaseExpiresTMP.text = expiresAtUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");

            OnPurchaseSucceeded?.Invoke(expiresAtUtc);
            return active;
        }
        catch (Exception e)
        {
            Show(purchasingPanel, false);
            Show(purchaseCompletedPanel, false);

            Debug.LogError("[ElitePassValidator] Purchase failed: " + e.Message);
            OnPurchaseFailed?.Invoke(e.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// ElitePass kontrolü: true dönerse kullanıcı aktiftir.
    /// false ise notEligiblePanel'i gösterebilir (bağlıysa).
    /// </summary>
    public async Task<bool> EnsureEliteAsync(bool showNotEligibleUI = true)
    {
        if (eliteService == null || IsBusy) return false;

        try
        {
            SetBusy(true);
            var (active, expiresAtUtc) = await eliteService.CheckAsync();

            if (active)
            {
                OnCheckPassed?.Invoke(expiresAtUtc);
                return true;
            }
            else
            {
                if (showNotEligibleUI) Show(notEligiblePanel, true);
                OnCheckFailed?.Invoke("not_active");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ElitePassValidator] Check failed: " + e.Message);
            if (showNotEligibleUI) Show(notEligiblePanel, true);
            OnCheckFailed?.Invoke(e.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// ElitePass varsa verilen aksiyonu çalıştırır; yoksa UI ile engeller.
    /// Kullanım: await validator.RequireEliteThenRun(async () => { ... });
    /// </summary>
    public async Task<bool> RequireEliteThenRun(Func<Task> actionIfAllowed, bool showNotEligibleUI = true)
    {
        if (actionIfAllowed == null) return false;

        var ok = await EnsureEliteAsync(showNotEligibleUI);
        if (!ok) return false;

        await actionIfAllowed();
        return true;
    }

    // ------------ helpers ------------
    private void Show(GameObject go, bool on)
    {
        if (go) go.SetActive(on);
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        if (purchaseButton) purchaseButton.interactable = !busy;
    }
}
