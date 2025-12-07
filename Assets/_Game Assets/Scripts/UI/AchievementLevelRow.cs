using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class AchievementLevelRow : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text targetAmount;          // Hedef miktar (ör. "1000")
    public TMP_Text rewardTMP;         // Ödül miktarı (Horizontal layout içinde)
    public Button   claimButton;       // Tıklanacak buton
    public TMP_Text claimButtonTMP;    // Buton içi metin (Artık sadece "Claim")

    [Header("Claim Button Sprites")]
    [Tooltip("Butonun Image bileşeni; sprite burada değişecek")]
    public Image claimButtonImage;
    public Sprite claimActiveSprite;   // Alınabilir durum
    // public Sprite claimDeactiveSprite; // Artık kullanılmıyor çünkü buton kapanıyor

    [Header("Fetching Panel")]
    [SerializeField] private GameObject fetchingPanel;

    public bool IsClaimable { get; private set; }

    private bool _busy = false;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        if (fetchingPanel) fetchingPanel.SetActive(false);
    }

    public void Bind(float targetValue, float reward, bool reachable, bool alreadyClaimed, System.Func<System.Threading.Tasks.Task> onClaimAsync)
    {
        // 1) Target Amount (Her zaman sadece miktar)
        if (targetAmount)
        {
            targetAmount.text = $"{targetValue}";
            // Renk değiştirme veya text ekleme yok
        }

        // 2) Reward Text (Her zaman görünür)
        if (rewardTMP)
            rewardTMP.text = $"{reward}";

        // 3) Claim Button Logic
        // "Claim edilemez durumdaysa veya zaten claim edildiyse buton kapatılacak"
        bool canClaim = reachable && !alreadyClaimed;
        IsClaimable = canClaim;

        if (claimButton)
        {
            // Buton görünürlüğü
            claimButton.gameObject.SetActive(canClaim);

            if (canClaim)
            {
                // Statik metin
                if (claimButtonTMP) claimButtonTMP.text = reward.ToString();

                // Sprite
                if (claimButtonImage && claimActiveSprite)
                    claimButtonImage.sprite = claimActiveSprite;

                claimButton.interactable = true;
                claimButton.onClick.RemoveAllListeners();
                claimButton.onClick.AddListener(async () =>
                {
                    if (_busy) return;
                    _busy = true;

                    if (fetchingPanel) fetchingPanel.SetActive(true);
                    claimButton.interactable = false;

                    try
                    {
                        if (onClaimAsync != null)
                            await onClaimAsync();
                    }
                    finally
                    {
                        if (fetchingPanel) fetchingPanel.SetActive(false);
                        _busy = false;
                    }
                });
            }
        }

        // 4) Canvas Group Alpha (Claim edildiyse 0.35)
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (_canvasGroup)
        {
            _canvasGroup.alpha = alreadyClaimed ? 0.2f : 1f;
            // Optional: Disable interaction if claimed (though button is already hidden)
            _canvasGroup.interactable = !alreadyClaimed;
            _canvasGroup.blocksRaycasts = !alreadyClaimed;
        }
    }
    
    public void SetProcessing(bool on)
    {
        if (fetchingPanel) fetchingPanel.SetActive(on);
        if (claimButton)
            claimButton.interactable = !on && IsClaimable && claimButton.gameObject.activeInHierarchy;
    }
}