using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementLevelRow : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text targetAmount;          // Hedef miktar (ör. "1000")
    public TMP_Text rewardTMP;         // İstersen boş bırakabilirsin; bilgilendirme alanı
    public Button   claimButton;       // Tıklanacak buton
    public TMP_Text claimButtonTMP;    // Buton içi metin (ödül yazacak)

    [Header("Claim Button Sprites")]
    [Tooltip("Butonun Image bileşeni; sprite burada değişecek")]
    public Image claimButtonImage;
    public Sprite claimActiveSprite;   // Alınabilir durum
    public Sprite claimDeactiveSprite; // Alınamaz durum

    [Header("Fetching Panel")]
    [SerializeField] private GameObject fetchingPanel;

    public bool IsClaimable { get; private set; }

    private bool _busy = false;

    private void Awake()
    {
        if (fetchingPanel) fetchingPanel.SetActive(false);
    }

    public void Bind(float targetValue, float reward, bool reachable, bool alreadyClaimed, System.Func<System.Threading.Tasks.Task> onClaimAsync)
    {
        // 1) targetValue text = hedef miktar
        if (targetAmount)
            targetAmount.text = $"{targetValue}";

        // 2) Buton içi metin = ödül metni
        string rewardLabel = $"{reward}";
        if (claimButtonTMP) claimButtonTMP.text = rewardLabel;

        if (alreadyClaimed)
        {
            if (targetAmount)
            {           
                targetAmount.text = $"{targetValue}     (Already Claimed)";
                targetAmount.color = Color.green;
            }
            if (claimButton)
                claimButton.gameObject.SetActive(false);
        }
        else
        {
            if (rewardTMP)
                rewardTMP.text = $"{reward}";
            if (claimButton)
                claimButton.gameObject.SetActive(true);
        }

        bool canClaim = reachable && !alreadyClaimed;
        IsClaimable = canClaim;

        if (claimButtonImage)
            claimButtonImage.sprite = canClaim ? claimActiveSprite : claimDeactiveSprite;

        if (claimButton)
        {
            claimButton.interactable = canClaim;
            claimButton.onClick.RemoveAllListeners();

            if (canClaim)
            {
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
    }
    
    public void SetProcessing(bool on)
    {
        if (fetchingPanel) fetchingPanel.SetActive(on);
        if (claimButton)
            claimButton.interactable = !on && IsClaimable && claimButton.gameObject.activeInHierarchy;
    }
}