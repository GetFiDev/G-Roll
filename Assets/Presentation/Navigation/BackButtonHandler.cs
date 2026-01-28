using UnityEngine;
using VContainer;

namespace GRoll.Presentation.Navigation
{
    /// <summary>
    /// MonoBehaviour that listens for Android back button and escape key.
    /// Should be placed on a persistent GameObject in the scene.
    /// </summary>
    public class BackButtonHandler : MonoBehaviour
    {
        [Inject] private NavigationService _navigationService;

        [Header("Settings")]
        [SerializeField] private bool handleEscapeKey = true;

        private void Update()
        {
            // Android back button or Escape key (for editor testing)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (handleEscapeKey || Application.platform == RuntimePlatform.Android)
                {
                    HandleBackButton();
                }
            }
        }

        private void HandleBackButton()
        {
            if (_navigationService != null)
            {
                var handled = _navigationService.HandleBackButton();

                // If not handled and we're at root, optionally quit the app
                if (!handled)
                {
                    // Could show an exit confirmation dialog here
                    // For now, we don't auto-quit
#if UNITY_ANDROID && !UNITY_EDITOR
                    // Optionally: Application.Quit();
#endif
                }
            }
        }
    }
}
