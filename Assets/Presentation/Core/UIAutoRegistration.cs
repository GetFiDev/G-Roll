using System.Collections.Generic;
using GRoll.Core.Interfaces.UI;
using GRoll.Presentation.Navigation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GRoll.Presentation.Core
{
    /// <summary>
    /// Auto-registration component for screens and popups.
    /// Attach this to a GameObject in the scene and assign screen/popup references.
    /// On Start, it will automatically register all assigned UI elements with NavigationService.
    /// </summary>
    public class UIAutoRegistration : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private List<UIScreenBase> screens = new();

        [Header("Popups")]
        [SerializeField] private List<UIPopupBase> popups = new();

        [Header("Settings")]
        [SerializeField] private bool autoFindInChildren = true;
        [SerializeField] private bool registerOnStart = true;

        private INavigationService _navigationService;
        private bool _isRegistered;

        [Inject]
        public void Construct(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        private void Start()
        {
            if (registerOnStart)
            {
                RegisterAll();
            }
        }

        /// <summary>
        /// Register all screens and popups with NavigationService.
        /// </summary>
        public void RegisterAll()
        {
            if (_isRegistered)
            {
                Debug.LogWarning("[UIAutoRegistration] Already registered");
                return;
            }

            if (_navigationService == null)
            {
                Debug.LogError("[UIAutoRegistration] NavigationService not injected");
                return;
            }

            // Auto-find if enabled
            if (autoFindInChildren)
            {
                FindAllInChildren();
            }

            // Register screens
            foreach (var screen in screens)
            {
                if (screen != null)
                {
                    if (_navigationService is NavigationService navService)
                    {
                        navService.RegisterScreen(screen);
                    }
                }
            }

            // Register popups
            foreach (var popup in popups)
            {
                if (popup != null)
                {
                    if (_navigationService is NavigationService navService)
                    {
                        navService.RegisterPopup(popup);
                    }
                }
            }

            _isRegistered = true;
            Debug.Log($"[UIAutoRegistration] Registered {screens.Count} screens and {popups.Count} popups");
        }

        /// <summary>
        /// Find all screens and popups in children.
        /// </summary>
        private void FindAllInChildren()
        {
            // Find screens
            var foundScreens = GetComponentsInChildren<UIScreenBase>(true);
            foreach (var screen in foundScreens)
            {
                if (!screens.Contains(screen))
                {
                    screens.Add(screen);
                }
            }

            // Find popups
            var foundPopups = GetComponentsInChildren<UIPopupBase>(true);
            foreach (var popup in foundPopups)
            {
                if (!popups.Contains(popup))
                {
                    popups.Add(popup);
                }
            }
        }

        /// <summary>
        /// Add a screen at runtime.
        /// </summary>
        public void AddScreen(UIScreenBase screen)
        {
            if (screen == null) return;
            if (!screens.Contains(screen))
            {
                screens.Add(screen);
                if (_isRegistered && _navigationService is NavigationService navService)
                {
                    navService.RegisterScreen(screen);
                }
            }
        }

        /// <summary>
        /// Add a popup at runtime.
        /// </summary>
        public void AddPopup(UIPopupBase popup)
        {
            if (popup == null) return;
            if (!popups.Contains(popup))
            {
                popups.Add(popup);
                if (_isRegistered && _navigationService is NavigationService navService)
                {
                    navService.RegisterPopup(popup);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Find All UI Elements")]
        private void EditorFindAll()
        {
            FindAllInChildren();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[UIAutoRegistration] Found {screens.Count} screens and {popups.Count} popups");
        }
#endif
    }
}
