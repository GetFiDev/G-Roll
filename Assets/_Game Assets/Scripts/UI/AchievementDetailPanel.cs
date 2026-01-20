using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using static AchievementService;

public class AchievementDetailPanel : MonoBehaviour
{
    public GameObject root;
    public CanvasGroup canvasGroup;
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

        if (iconImage)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }

        var sp = await AchievementIconCache.LoadSpriteAsync(def.iconUrl);
        if (sp != null && iconImage != null)
        {
            iconImage.sprite = sp;
            iconImage.gameObject.SetActive(true);
        }

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

            // Capture level for closure
            int capturedLevel = lv;

            // Bind with target amount instead of level index (optimistic UI - no waiting)
            row.Bind(targetValue, reward, reachable, isClaimed, () => {
                // Collect levels that will be claimed BEFORE the optimistic call modifies state
                var levelsToClaimRow = new List<int>();
                var haveRow = new HashSet<int>(state.claimedLevels ?? new List<int>());
                for (int lvRow = 1; lvRow <= state.level; lvRow++)
                {
                    if (!haveRow.Contains(lvRow))
                        levelsToClaimRow.Add(lvRow);
                }

                int totalReward = AchievementService.ClaimAllEligibleOptimistic(def, state);
                _onAnyClaim?.Invoke();

                // INSTANT badge update - no server round-trip needed
                if (levelsToClaimRow.Count > 0 && NotificationBadgeManager.Instance != null)
                {
                    NotificationBadgeManager.Instance.OnAchievementClaimedMultiple(def.typeId, levelsToClaimRow);
                }

                if (totalReward > 0)
                {
                    Close();
                    UITopPanel.Instance.Initialize();
                }
                return System.Threading.Tasks.Task.CompletedTask;
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
        claimAllButton.onClick.AddListener(() => {
            // Disable the button immediately to prevent double taps
            claimAllButton.interactable = false;

            // Collect levels that will be claimed BEFORE the optimistic call modifies state
            var levelsToClaim = new List<int>();
            var have = new HashSet<int>(state.claimedLevels ?? new List<int>());
            for (int lv = 1; lv <= state.level; lv++)
            {
                if (!have.Contains(lv))
                    levelsToClaim.Add(lv);
            }

            // Optimistic UI: immediately grant rewards and close panel
            int totalReward = AchievementService.ClaimAllEligibleOptimistic(def, state);
            _onAnyClaim?.Invoke();

            // INSTANT badge update - no server round-trip needed
            if (levelsToClaim.Count > 0 && NotificationBadgeManager.Instance != null)
            {
                NotificationBadgeManager.Instance.OnAchievementClaimedMultiple(def.typeId, levelsToClaim);
            }

            if (totalReward > 0)
            {
                Close();
                UITopPanel.Instance.Initialize();
            }
            else
            {
                // Re-enable button if nothing was claimed
                claimAllButton.interactable = anyClaimable;
            }
        });

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        // Fade In Logic
        root.SetActive(true);
        if (canvasGroup == null) canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.3f).SetUpdate(true);
        }
    }

    public void Close()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => root.SetActive(false));
        }
        else
        {
            root.SetActive(false);
        }
    }
}