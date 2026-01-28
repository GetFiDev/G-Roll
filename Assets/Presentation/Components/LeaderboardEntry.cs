using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GRoll.Presentation.Components
{
    /// <summary>
    /// Leaderboard entry component displaying a single ranking row.
    /// Supports special styling for top 3 and current user highlighting.
    /// </summary>
    public class LeaderboardEntry : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image avatarFrameImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image rankPlatterImage;

        [Header("Elite Badge")]
        [SerializeField] private GameObject eliteBadge;

        [Header("Rank Platters (Top 3)")]
        [SerializeField] private Sprite goldPlatter;
        [SerializeField] private Sprite silverPlatter;
        [SerializeField] private Sprite bronzePlatter;

        [Header("Frame Sprites")]
        [SerializeField] private Sprite defaultFrame;
        [SerializeField] private Sprite eliteFrame;

        [Header("Colors")]
        [SerializeField] private Color defaultBackgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        [SerializeField] private Color currentUserBackgroundColor = new Color(0.2f, 0.3f, 0.5f, 1f);
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f, 1f);
        [SerializeField] private Color silverColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        [SerializeField] private Color bronzeColor = new Color(0.8f, 0.5f, 0.2f, 1f);

        [Header("Default Avatar")]
        [SerializeField] private Sprite defaultAvatarSprite;

        private int _rank;
        private bool _isCurrentUser;
        private bool _hasElite;
        private bool _lockFrameSprite;

        public void SetData(int rank, string username, int score, bool hasElite = false, string avatarUrl = null)
        {
            _rank = rank;
            _hasElite = hasElite;

            if (rankText != null)
            {
                rankText.text = $"#{rank}";
                rankText.color = GetRankColor(rank);
            }

            if (usernameText != null)
            {
                usernameText.text = username ?? "Unknown";
            }

            if (scoreText != null)
            {
                scoreText.text = FormatScore(score);
            }

            UpdateRankPlatter(rank);
            UpdateEliteStatus(hasElite);

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                LoadAvatarAsync(avatarUrl).Forget();
            }
            else if (avatarImage != null && defaultAvatarSprite != null)
            {
                avatarImage.sprite = defaultAvatarSprite;
            }
        }

        public void SetCurrentUser(bool isCurrentUser)
        {
            _isCurrentUser = isCurrentUser;

            if (backgroundImage != null)
            {
                backgroundImage.color = isCurrentUser ? currentUserBackgroundColor : defaultBackgroundColor;
            }

            if (usernameText != null && isCurrentUser)
            {
                usernameText.fontStyle = FontStyles.Bold;
            }
        }

        public void SetLockFrame(bool lockFrame)
        {
            _lockFrameSprite = lockFrame;
        }

        private Color GetRankColor(int rank)
        {
            return rank switch
            {
                1 => goldColor,
                2 => silverColor,
                3 => bronzeColor,
                _ => Color.white
            };
        }

        private void UpdateRankPlatter(int rank)
        {
            if (rankPlatterImage == null) return;

            Sprite platterSprite = rank switch
            {
                1 => goldPlatter,
                2 => silverPlatter,
                3 => bronzePlatter,
                _ => null
            };

            if (platterSprite != null)
            {
                rankPlatterImage.sprite = platterSprite;
                rankPlatterImage.color = Color.white;
                rankPlatterImage.enabled = true;
            }
            else
            {
                rankPlatterImage.enabled = false;
            }
        }

        private void UpdateEliteStatus(bool hasElite)
        {
            if (eliteBadge != null)
            {
                eliteBadge.SetActive(hasElite);
            }

            if (avatarFrameImage != null && !_lockFrameSprite)
            {
                avatarFrameImage.sprite = hasElite ? eliteFrame : defaultFrame;
            }
        }

        private async UniTaskVoid LoadAvatarAsync(string url)
        {
            if (avatarImage == null || string.IsNullOrEmpty(url)) return;

            try
            {
                using var request = UnityWebRequestTexture.GetTexture(url);
                await request.SendWebRequest();

                if (this == null) return;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);
                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    avatarImage.sprite = sprite;
                }
            }
            catch
            {
                if (avatarImage != null && defaultAvatarSprite != null)
                {
                    avatarImage.sprite = defaultAvatarSprite;
                }
            }
        }

        private string FormatScore(int score)
        {
            if (score >= 1000000)
                return $"{score / 1000000f:F1}M";
            if (score >= 1000)
                return $"{score / 1000f:F1}K";
            return score.ToString("N0");
        }

        public int Rank => _rank;
        public bool IsCurrentUser => _isCurrentUser;
        public bool HasElite => _hasElite;
    }
}
