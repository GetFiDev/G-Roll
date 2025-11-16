using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UILeaderboardDisplay : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI rankTMP;
    [SerializeField] private TextMeshProUGUI usernameTMP;
    [SerializeField] private TextMeshProUGUI scoreTMP;

    [Header("Frame (row background)")]
    [SerializeField] private Image backgroundImage; // sıra arka plan çerçevesi

    [Header("Frame Sprites")]
    [SerializeField] private Sprite spriteTop3Elite;
    [SerializeField] private Sprite spriteTop3NonElite;
    [SerializeField] private Sprite spriteElite;
    [SerializeField] private Sprite spriteDefault;

    [Header("Rank Plate (behind rank text)")]
    [SerializeField] private Image rankPlateImage;
    [SerializeField] private Sprite rankPlateElite;
    [SerializeField] private Sprite rankPlateDefault;

    [Header("Options")]
    [SerializeField] private bool hideRankIfEmpty = true; // rank boşsa gizle
    [SerializeField] private bool lockFrameSprite = false; // self header için: arka plan çerçevesi değişmez

    /// <summary>Self header gibi özel yerlerde arka plan çerçevesini sabitle.</summary>
    public void SetLockFrame(bool on) => lockFrameSprite = on;

    /// <summary>
    /// Self header'ın Inspector'ında rank plate sprite'ları boş bırakıldıysa,
    /// row prefab'ından gelen sprite'ları buraya enjekte edebilmek için.
    /// </summary>
    public void EnsureRankPlateSprites(Sprite defaultPlate, Sprite elitePlate)
    {
        if (rankPlateDefault == null) rankPlateDefault = defaultPlate;
        if (rankPlateElite   == null) rankPlateElite   = elitePlate;
    }

    /// <summary>Row prefab’ındaki plate sprite’larına dışarıdan erişim (panel runtime inject için).</summary>
    public Sprite GetRankPlateDefault() => rankPlateDefault;
    public Sprite GetRankPlateElite()   => rankPlateElite;

    /// <summary>
    /// Bütün görsel bağlama burada.
    /// Frame seçimi (backgroundImage):
    ///   - isTop3 && hasElite     -> spriteTop3Elite
    ///   - isTop3 && !hasElite    -> spriteTop3NonElite
    ///   - !isTop3 && hasElite    -> spriteElite
    ///   - !isTop3 && !hasElite   -> spriteDefault
    ///
    /// Rank plate (rankPlateImage) her zaman hasElite’e göre:
    ///   - hasElite -> rankPlateElite
    ///   - else     -> rankPlateDefault
    /// </summary>
    public void SetData(string rankText, string username, int score, bool isTop3, bool hasElite)
    {
        // Metinler
        if (usernameTMP)
            usernameTMP.text = string.IsNullOrWhiteSpace(username) ? "Guest" : username;

        if (scoreTMP)
            scoreTMP.text = score.ToString();

        if (rankTMP)
        {
            if (string.IsNullOrEmpty(rankText) && hideRankIfEmpty)
            {
                rankTMP.gameObject.SetActive(false);
                rankTMP.text = string.Empty;
            }
            else
            {
                rankTMP.gameObject.SetActive(true);
                rankTMP.text = rankText;
            }
        }

        ApplyFrame(isTop3, hasElite);
        ApplyRankPlate(hasElite);
    }

    private void ApplyFrame(bool isTop3, bool hasElite)
    {
        if (backgroundImage == null) return;
        if (lockFrameSprite) return; // self header: arka plan sabit

        Sprite frame = null;
        if (isTop3)
            frame = hasElite ? spriteTop3Elite : spriteTop3NonElite;
        else
            frame = hasElite ? spriteElite : spriteDefault;

        backgroundImage.sprite = frame;
        backgroundImage.enabled = frame != null;
    }

    private void ApplyRankPlate(bool hasElite)
    {
        if (rankPlateImage == null) return;

        var plate = hasElite ? rankPlateElite : rankPlateDefault;
        rankPlateImage.sprite = plate;
        rankPlateImage.enabled = (plate != null);
    }
}