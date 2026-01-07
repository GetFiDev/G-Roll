using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class UILeaderboardDisplay : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI rankTMP;
    [SerializeField] private TextMeshProUGUI usernameTMP;
    [SerializeField] private TextMeshProUGUI scoreTMP;

    [Header("Profile Visuals")]
    [SerializeField] private Image profileImage;
    [SerializeField] private Image avatarFrameImage; // Avatarın etrafındaki çerçeve/platter
    [SerializeField] private GameObject eliteBadgeObj;
    [SerializeField] private Sprite defaultProfileSprite;
    [SerializeField] private Sprite avatarFrameElite;
    [SerializeField] private Sprite avatarFrameDefault;

    [Header("Rank Visuals")]
    [SerializeField] private Image rankPlatterImage; // Arkadaki tabak (gold/silver/bronze)
    [SerializeField] private Sprite rankPlatterGold;
    [SerializeField] private Sprite rankPlatterSilver;
    [SerializeField] private Sprite rankPlatterBronze;

    [Header("Frame (row background)")]
    [SerializeField] private Image backgroundImage; 
    [SerializeField] private Sprite bgElite;
    [SerializeField] private Sprite bgDefault;

    // Deprecated fields removed/ignored
    // [Header("Frame Sprites")] ...

    [Header("Options")]
    [SerializeField] private bool hideRankIfEmpty = true; // rank boşsa gizle
    [SerializeField] private bool lockFrameSprite = false; // self header için: arka plan çerçevesi değişmez

    /// <summary>Self header gibi özel yerlerde arka plan çerçevesini sabitle.</summary>
    public void SetLockFrame(bool on) => lockFrameSprite = on;



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
    public void SetData(int rank, string username, int score, bool hasElite, string photoUrl)
    {
        // 1. Texts
        if (usernameTMP) usernameTMP.text = string.IsNullOrWhiteSpace(username) ? "Guest" : username;
        if (scoreTMP) scoreTMP.text = score.ToString();

        // 2. Rank Logic
        if (rank > 0)
        {
            // Always show Text
            if (rankTMP) 
            {
                rankTMP.gameObject.SetActive(true);
                rankTMP.text = rank.ToString();
            }

            // Platter Logic
            if (rankPlatterImage)
            {
                rankPlatterImage.gameObject.SetActive(true);
                
                if (rank <= 3)
                {
                    // Top 3: Show Gold/Silver/Bronze
                    var c = rankPlatterImage.color;
                    c.a = 1f;
                    rankPlatterImage.color = c;

                    if (rank == 1) rankPlatterImage.sprite = rankPlatterGold;
                    else if (rank == 2) rankPlatterImage.sprite = rankPlatterSilver;
                    else if (rank == 3) rankPlatterImage.sprite = rankPlatterBronze;
                }
                else
                {
                    // Rank > 3: Hide Platter
                    var c = rankPlatterImage.color;
                    c.a = 0f;
                    rankPlatterImage.color = c;
                }
            }
        }
        else
        {
            // Rank unavailable
            if (rankTMP) rankTMP.text = "-";
            if (rankPlatterImage) rankPlatterImage.gameObject.SetActive(false);
        }

        // 3. Elite Visuals (Background & Avatar Frame & Badge)
        if (backgroundImage) backgroundImage.sprite = hasElite ? bgElite : bgDefault;
        if (avatarFrameImage) avatarFrameImage.sprite = hasElite ? avatarFrameElite : avatarFrameDefault;
        if (eliteBadgeObj) eliteBadgeObj.SetActive(hasElite);

        // 4. Locked Frame Logic (Self Header)
        if (lockFrameSprite && backgroundImage) backgroundImage.sprite = bgElite; // Example override if needed

        // 5. Profile Picture
        if (profileImage)
        {
            profileImage.sprite = defaultProfileSprite; // Reset first
            if (!string.IsNullOrEmpty(photoUrl) && gameObject.activeInHierarchy)
            {
                StartCoroutine(LoadProfileImage(photoUrl));
            }
        }
    }

    private IEnumerator LoadProfileImage(string url)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (tex && profileImage)
                {
                    profileImage.sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f, 0.5f));
                }
            }
        }
    }

}