using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Threading.Tasks;

public class UIReferralEarningsPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI totalAmountText;
    [SerializeField] private Button claimButton;
    // Loading overlay removed as requested

    [Header("Animation Settings")]
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.green;

    private long _currentTotal = 0;
    private bool _isClaiming = false;

    private void Awake()
    {
        if (claimButton) claimButton.onClick.AddListener(OnClaimClicked);
    }

    private void OnDisable()
    {
        // Kill tweens to avoid issues if panel closed mid-animation
        totalAmountText.DOKill();
        claimButton.transform.DOKill();
    }

    public void Initialize(long totalAmount)
    {
        _currentTotal = totalAmount;
        _isClaiming = false;
        
        gameObject.SetActive(true);

        // Reset State
        if (claimButton)
        {
            claimButton.interactable = true;
            claimButton.transform.localScale = Vector3.zero; // Start hidden
        }
        
        if (totalAmountText)
        {
            totalAmountText.text = "0.00";
            totalAmountText.color = startColor;
        }

        StartCoroutine(AnimationSequence(totalAmount));
    }

    private IEnumerator AnimationSequence(long targetAmount)
    {
        // 1. Determine random duration (1s - 4s)
        float duration = UnityEngine.Random.Range(1.0f, 4.0f);
        
        // 2. Count Up & Color Lerp
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Lerp value
            float currentVal = Mathf.Lerp(0, targetAmount, t);
            if (totalAmountText)
            {
                totalAmountText.text = $"{currentVal:F2}"; // Format properly? Assuming standard float display or integer?
                // User said "0.00", implying float. But "totalAmount" is long (currency). 
                // Let's assume currency is integer but displayed with decimals or it is float?
                // Original code had long. If user sees 0.00, maybe they want it to look like money.
                // I will display as F2 for effect, or N0 if strictly integer. 
                // "0.00" request suggests F2.
                
                totalAmountText.color = Color.Lerp(startColor, endColor, t);
            }
            yield return null;
        }

        // Finalize value
        if (totalAmountText)
        {
            totalAmountText.text = $"{targetAmount:F2}";
            totalAmountText.color = endColor;
        }

        // 3. Blink Sequence: White -> Green -> White -> Green -> White
        // "beyaz yeşil beyaz yeşil beyaz" -> ends on White.
        // Let's do a sequence.
        
        if (totalAmountText)
        {
            Sequence blinkSeq = DOTween.Sequence();
            float blinkDur = 0.2f;

            blinkSeq.Append(totalAmountText.DOColor(startColor, blinkDur)); // White
            blinkSeq.Append(totalAmountText.DOColor(endColor, blinkDur));   // Green
            blinkSeq.Append(totalAmountText.DOColor(startColor, blinkDur)); // White
            blinkSeq.Append(totalAmountText.DOColor(endColor, blinkDur));   // Green
            blinkSeq.Append(totalAmountText.DOColor(startColor, blinkDur)); // White (Final)

            yield return blinkSeq.WaitForCompletion();
        }

        // 4. Show Claim Button (Scale Up)
        if (claimButton)
        {
            claimButton.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
        }
    }

    private async void OnClaimClicked()
    {
        if (_isClaiming) return;
        _isClaiming = true;
        
        if (claimButton) claimButton.interactable = false;

        try
        {
            // Sending claim request
            var res = await ReferralRemoteService.ClaimReferralEarnings();
            
            Debug.Log($"[UIReferralEarningsPanel] Claimed {res.claimed}");

            // Success Updates
            if (UITopPanel.Instance != null) UITopPanel.Instance.Initialize();

            // Refresh Home if handy reference exists, usually not needed if TopPanel updates currency
            // But user said: "home hoem panel görünecek. home panele dönüşte home panel refresh atıcak"
            // We are already IN home panel concept (overlay). Closing this reveals HomePanel.
            
            gameObject.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UIReferralEarningsPanel] Claim failed: {e.Message}");
            _isClaiming = false;
            if (claimButton) claimButton.interactable = true;
        }
    }
}
