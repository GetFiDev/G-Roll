using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIReferralEarningsPanel : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText; // Optional: "Pending Earnings"
    public TextMeshProUGUI amountText; // The main number
    public Button claimButton;
    // Removed closeButton as per request

    private Action _onClaimCallback; // Changed to match method signature easier, but we need Async now

    // We change the signature to accept a Task-returning Func for async waiting
    private Func<Task> _onClaimAsync;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // Updated Open signature to accept async callback
    public void Open(float amount, Func<Task> onClaimAsync)
    {
        gameObject.SetActive(true);
        if (amountText) amountText.text = amount.ToString("0.###");
        
        if (claimButton) 
        {
            claimButton.interactable = true;
        }

        _onClaimAsync = onClaimAsync;

        // Reset Alpha and Fade In
        _canvasGroup.alpha = 0f;
        StopAllCoroutines();
        StartCoroutine(Co_Fade(0f, 1f, 0.5f));
    }

    private void OnEnable()
    {
        if (claimButton) claimButton.onClick.AddListener(OnClaimClick);
    }

    private void OnDisable()
    {
        if (claimButton) claimButton.onClick.RemoveListener(OnClaimClick);
    }

    private async void OnClaimClick()
    {
        if (claimButton) claimButton.interactable = false;

        // Trigger the claim action and WAIT
        if (_onClaimAsync != null)
        {
            await _onClaimAsync.Invoke();
        }

        // Fade Out and Close
        StartCoroutine(Co_FadeOutAndClose());
    }

    private System.Collections.IEnumerator Co_Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }

    private System.Collections.IEnumerator Co_FadeOutAndClose()
    {
        yield return Co_Fade(1f, 0f, 0.5f);
        
        gameObject.SetActive(false);
        _canvasGroup.alpha = 1f; // Reset alpha for next usage if pooled (though we implement Destroy below usually)
        
        // Spec says "Destroy" in previous logic, but user says "Hide" implies reuse or just clean exit.
        // User said "direk gameobjectini kapatırsın". So SetActive(false).
        // If instantiated as prefab, maybe Destroy is better to avoid clutter?
        // Let's stick to SetActive(false) as requested, but if it was instantiated by UIReferralPanel, 
        // UIReferralPanel keeps a ref `_currentPopup`. 
        // If we just hide, `_currentPopup` remains not null, so next time it reuses it?
        // UIReferralPanel logic: if (_currentPopup == null) Instantiate...
        // So we should DESTROY it to clear the reference in UIReferralPanel? 
        // OR UIReferralPanel should check if active.
        // Let's Destroy to be safe and consistent with "Popup" transient nature. 
        // BUT User said "alfayı yine 1 yapman lazım... gameobjecti kapattıktan sonra". 
        // This implies reuse. Let's use Destroy for now to avoid logic changes in Parent, unless Parent supports reuse.
        // Checking Parent: `if (_currentPopup == null && earningsPanelPrefab != null)`
        // If we don't destroy, _currentPopup stays valid but hidden.
        // Next refresh: loops, sees `_currentPopup` is not null.
        // `_currentPopup.Open(...)` -> re-opens it. 
        // This supports reuse! So I will NOT Destroy.
    }
}
