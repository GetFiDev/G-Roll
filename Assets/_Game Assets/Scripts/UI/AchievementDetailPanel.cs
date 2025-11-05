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
    public TMP_Text progressTMP;
    public TMP_Text statusTMP;
    public Button closeButton;

    public Button claimAllButton;
    public Transform levelsRoot;
    public AchievementLevelRow levelRowPrefab;

    private AchDef _def;
    private AchState _state;
    private System.Action _onAnyClaim;

    public async void Open(AchDef def, AchState state, System.Action onAnyClaim)
    {
        _def = def; _state = state; _onAnyClaim = onAnyClaim;
        if (statusTMP) statusTMP.text = string.Empty;

        iconImage.sprite = null;
        var sp = await AchievementIconCache.LoadSpriteAsync(def.iconUrl);
        if (sp) iconImage.sprite = sp;

        titleTMP.text = def.displayName;
        descTMP.text  = def.description;
        progressTMP.text = $"{state.level}/{def.maxLevel}";

        foreach (Transform c in levelsRoot) Destroy(c.gameObject);

        var claimed = new HashSet<int>(state.claimedLevels ?? new List<int>());
        for (int lv = 1; lv <= def.maxLevel; lv++)
        {
            var row = Instantiate(levelRowPrefab, levelsRoot);
            bool reachable = lv <= state.level;
            bool isClaimed = claimed.Contains(lv);
            int reward = (lv-1) < def.rewards.Count ? def.rewards[lv-1] : 0;

            row.Bind(lv, reward, reachable, isClaimed, async () => {
                if (statusTMP) statusTMP.text = "Claiming…";
                int n = await AchievementService.ClaimAllEligibleAsync(def, state);
                if (statusTMP) statusTMP.text = n > 0 ? "Claimed!" : "Refreshing…";
                _onAnyClaim?.Invoke();
            });
        }

        claimAllButton.onClick.RemoveAllListeners();
        claimAllButton.onClick.AddListener(async () => {
            if (statusTMP) statusTMP.text = "Claiming all…";
            claimAllButton.interactable = false;
            int n = await AchievementService.ClaimAllEligibleAsync(def, state);
            if (statusTMP) statusTMP.text = n > 0 ? "Claimed!" : "Refreshing…";
            _onAnyClaim?.Invoke();
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