using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementLevelRow : MonoBehaviour
{
    public TMP_Text levelTMP;     // "Level 1"
    public TMP_Text rewardTMP;    // "+5 GET"
    public Button   claimButton;  // aktif/pasif hale gelecek
    public TMP_Text claimButtonTMP;
    private bool _busy = false;

    public void Bind(int level, int reward, bool reachable, bool alreadyClaimed, System.Action onClaim)
    {
        levelTMP.text = $"Phase {level}";
        rewardTMP.text = $"{reward}";

        claimButton.onClick.RemoveAllListeners();

        if (!reachable)
        {
            claimButton.interactable = false;
            claimButtonTMP.text = "Locked";
        }
        else if (alreadyClaimed)
        {
            claimButton.interactable = false;
            claimButtonTMP.text = "Claimed";
        }
        else
        {
            claimButton.interactable = true;
            claimButtonTMP.text = "Claim";
            claimButton.onClick.AddListener(() => {
                if (_busy) return;
                _busy = true;
                claimButton.interactable = false;
                claimButtonTMP.text = "…"; // simple loading indicator
                onClaim?.Invoke();
            });
        }
    }

    public void SetIdle(bool reachable, bool alreadyClaimed)
    {
        _busy = false;
        if (!reachable)
        {
            claimButton.interactable = false;
            claimButtonTMP.text = "Locked";
            return;
        }
        if (alreadyClaimed)
        {
            claimButton.interactable = false;
            claimButtonTMP.text = "Claimed";
            return;
        }
        claimButton.interactable = true;
        claimButtonTMP.text = "Claim";
    }
}