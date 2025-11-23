using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static AchievementService;

public class AchievementDetailPanel : MonoBehaviour
{
    public GameObject root;
    public Image   iconImage;
    public TMP_Text titleTMP;
    public TMP_Text descTMP;
    public Button closeButton;

    public Button claimAllButton;
    public TMP_Text claimAllLabel;
    public Sprite claimAllActiveSprite;
    public Sprite claimAllInactiveSprite;
    public Transform levelsRoot;
    public AchievementLevelRow levelRowPrefab;

    private AchDef _def;
    private AchState _state;
    private System.Action _onAnyClaim;

    public async void Open(AchDef def, AchState state, System.Action onAnyClaim)
    {
        _def = def; _state = state; _onAnyClaim = onAnyClaim;

        iconImage.sprite = null;
        var sp = await AchievementIconCache.LoadSpriteAsync(def.iconUrl);
        if (sp) iconImage.sprite = sp;

        titleTMP.text = def.displayName;
        descTMP.text  = def.description;

        foreach (Transform c in levelsRoot) Destroy(c.gameObject);

        bool anyClaimable = false;
        for (int lv = 1; lv <= def.maxLevel; lv++)
        {
            var row = Instantiate(levelRowPrefab, levelsRoot);

            // Target amount for this level (fallback to level index if no list provided)
            float targetValue = lv;
            if (def.thresholds != null && (lv - 1) >= 0 && (lv - 1) < def.thresholds.Count)
                targetValue = (float)def.thresholds[lv - 1];

            bool reachable = lv <= state.level;
            bool isClaimed = (state.claimedLevels != null) && state.claimedLevels.Contains(lv);
            float reward = (lv - 1) < def.rewards.Count ? def.rewards[lv - 1] : 0;

            // Bind with target amount instead of level index
            row.Bind(targetValue, reward, reachable, isClaimed, async () => {
                int n = await AchievementService.ClaimAllEligibleAsync(def, state);
                _onAnyClaim?.Invoke();
            });

            if (reachable && !isClaimed)
                anyClaimable = true;
        }

        if (claimAllLabel)
            claimAllLabel.text = anyClaimable ? "Claim All" : "Nothing to Claim";

        var img = claimAllButton.GetComponent<Image>();
        if (img)
            img.sprite = anyClaimable ? claimAllActiveSprite : claimAllInactiveSprite;

        claimAllButton.interactable = anyClaimable;

        claimAllButton.onClick.RemoveAllListeners();
        claimAllButton.onClick.AddListener(async () => {
            // Disable the button immediately to prevent double taps
            claimAllButton.interactable = false;

            // Turn on fetching overlay on every claimable row
            var rows = levelsRoot.GetComponentsInChildren<AchievementLevelRow>(true);
            foreach (var r in rows)
            {
                if (r != null && r.IsClaimable)
                    r.SetProcessing(true);
            }

            try
            {
                int n = await AchievementService.ClaimAllEligibleAsync(def, state);
                _onAnyClaim?.Invoke();
                using var _ = NotificationBadgeManager.Instance.RefreshAchievementBadges();
            }
            finally
            {
                // If the panel isn't immediately re-bound, make sure to turn overlays off
                foreach (var r in rows)
                {
                    if (r != null)
                        r.SetProcessing(false);
                }
            }
        });

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        root.SetActive(true);
    }

    public void Close() => root.SetActive(false);
}