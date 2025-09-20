using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UILeaderboardDisplay : MonoBehaviour
{
    [Header("Texts")]
    public TextMeshProUGUI rankTMP;
    public TextMeshProUGUI usernameTMP;
    public TextMeshProUGUI scoreTMP;

    [Header("Rank Background")]
    public Image backgroundImage;             // prefab’taki arka plan Image’ı
    public List<Sprite> rankSprites = new();  // 1., 2., 3., ... için sıralı sprite listesi
    public bool applyRankVisualOnlyOnce = true; // true: sadece ilk SetData’da uygula

    [Header("Options")]
    public bool hideRankIfEmpty = true;       // rank boşsa gizle

    private bool _rankVisualApplied = false;

    public void SetData(string rankText, string username, int score)
    {
        // Metinler
        if (usernameTMP) usernameTMP.text = string.IsNullOrWhiteSpace(username) ? "Guest" : username;
        if (scoreTMP)    scoreTMP.text    = score.ToString();

        if (rankTMP)
        {
            if (string.IsNullOrEmpty(rankText) && hideRankIfEmpty)
            {
                rankTMP.text = "";
                rankTMP.gameObject.SetActive(false);
            }
            else
            {
                rankTMP.gameObject.SetActive(true);
                rankTMP.text = rankText;
            }
        }

        // Rank’a göre arka plan (1-based)
        TryApplyRankBackground(rankText);
    }

    private void TryApplyRankBackground(string rankText)
    {
        if (applyRankVisualOnlyOnce && _rankVisualApplied) return;
        if (backgroundImage == null) return;
        if (rankSprites == null || rankSprites.Count == 0) return;

        // Self kartta "—" gibi placeholder gelebilir; parse edilemezse dokunma
        if (!int.TryParse(rankText, out var oneBasedRank)) return;
        if (oneBasedRank <= 0) return;

        // Index: 1 → 0, 2 → 1, ... fazla ise son sprite
        int idx = Mathf.Clamp(oneBasedRank - 1, 0, rankSprites.Count - 1);
        var spr = rankSprites[idx];

        backgroundImage.sprite = spr;
        backgroundImage.enabled = spr != null;

        _rankVisualApplied = true;
    }

    // Objeyi yeniden kullanıyorsan (pool vb.), bunu çağırıp sıfırlayabilirsin.
    public void ResetRankVisual()
    {
        _rankVisualApplied = false;
    }
}
