using System;
using Cysharp.Threading.Tasks;
using GRoll.Infrastructure.Firebase.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

/// <summary>
/// ElitePass ile ilgili TÜM akışların geçtiği tek kapı:
/// - Satın alma: StartPurchase()
/// - Kontrol + koşullu aksiyon: RequireEliteThenRun(...)
/// IElitePassRemoteService kullanır.
/// </summary>
public class ElitePassValidator : MonoBehaviour
{
    [Header("Purchase UI (optional)")]
    public GameObject purchasingPanel;        // "Processing..." overlay
    public GameObject purchaseCompletedPanel; // "Completed" panel
    public TMP_Text   purchaseExpiresTMP;     // bitiş tarihi göstermek istersen
    public Button     purchaseButton;         // satın alma tuşu (busy iken disable)

    [Header("Gate UI (optional)")]
    public GameObject notEligiblePanel;       // ElitePass yoksa göstereceğin panel (örn. "Elite gerekli")

    private IElitePassRemoteService _eliteService;

    public bool IsBusy { get; private set; }

    // --- Events ---
    public event Action OnPurchaseStarted;
    public event Action<DateTime?> OnPurchaseSucceeded; // expiresAtUtc
    public event Action<string> OnPurchaseFailed;

    public event Action<DateTime?> OnCheckPassed;
    public event Action<string> OnCheckFailed;

    [Inject]
    public void Construct(IElitePassRemoteService eliteService)
    {
        _eliteService = eliteService;
    }

    void Awake()
    {
        // Service will be injected via VContainer
    }

    // UI Button → Satın alma başlat
    public void OnPurchaseButton()
    {
        _ = StartPurchase();
    }

    /// <summary>
    /// Satın alma akışı: UI panellerini yönetir, sunucudan 30 gün ekletir.
    /// </summary>
    public async UniTask<bool> StartPurchase(string purchaseId = null)
    {
        if (_eliteService == null || IsBusy) return false;

        try
        {
            SetBusy(true);
            Show(purchasingPanel, true);
            Show(purchaseCompletedPanel, false);

            OnPurchaseStarted?.Invoke();

            // idempotency için rastgele id (UI'dan aynı tuşa iki kez basılırsa dahi güvenli)
            if (string.IsNullOrEmpty(purchaseId))
                purchaseId = Guid.NewGuid().ToString("N");

            var response = await _eliteService.PurchaseAsync(purchaseId);

            Show(purchasingPanel, false);

            if (!response.Success)
            {
                Debug.LogError($"[ElitePassValidator] Purchase failed: {response.ErrorMessage}");
                OnPurchaseFailed?.Invoke(response.ErrorMessage);
                return false;
            }

            Show(purchaseCompletedPanel, true);

            if (purchaseExpiresTMP && response.ExpiresAtUtc.HasValue)
                purchaseExpiresTMP.text = response.ExpiresAtUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");

            OnPurchaseSucceeded?.Invoke(response.ExpiresAtUtc);
            return response.IsActive;
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
    public async UniTask<bool> EnsureEliteAsync(bool showNotEligibleUI = true)
    {
        if (_eliteService == null || IsBusy) return false;

        try
        {
            SetBusy(true);
            var response = await _eliteService.CheckAsync();

            if (!response.Success)
            {
                Debug.LogError($"[ElitePassValidator] Check failed: {response.ErrorMessage}");
                if (showNotEligibleUI) Show(notEligiblePanel, true);
                OnCheckFailed?.Invoke(response.ErrorMessage);
                return false;
            }

            if (response.IsActive)
            {
                OnCheckPassed?.Invoke(response.ExpiresAtUtc);
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
    public async UniTask<bool> RequireEliteThenRun(Func<UniTask> actionIfAllowed, bool showNotEligibleUI = true)
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
