using UnityEngine;
using UnityEngine.UI;
using NetworkingData;
using System.Threading.Tasks;

public class ProfilePicDisplayer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite defaultSprite;

    private void OnEnable()
    {
        DisplayProfilePic();
    }

    private async void DisplayProfilePic()
    {
        // 1. Set default sprite immediately
        if (targetImage != null && defaultSprite != null)
        {
            targetImage.sprite = defaultSprite;
        }

        // 2. Check for UserDatabaseManager
        if (UserDatabaseManager.Instance == null)
        {
            Debug.LogWarning("[ProfilePicDisplayer] UserDatabaseManager.Instance is null");
            return;
        }

        // 3. Get UserData
        var userData = UserDatabaseManager.Instance.currentUserData;
        if (userData == null)
        {
            // User data not loaded yet, keep default
            return;
        }

        // 4. Check for Photo URL
        string photoUrl = userData.photoUrl;
        if (string.IsNullOrEmpty(photoUrl))
        {
            // No photo URL, keep default
            return;
        }

        // 5. Download Texture async
        Texture2D texture = await RemoteItemService.DownloadTextureAsync(photoUrl);
        if (texture == null)
        {
             // Download failed, keep default
             return;
        }

        // 6. Create Sprite and Assign
        // Ensure this GameObject is still active and valid before assigning
        if (this != null && gameObject != null && targetImage != null)
        {
            Sprite sprite = RemoteItemService.CreateSprite(texture);
            if (sprite != null)
            {
                targetImage.sprite = sprite;
            }
        }
    }
}
