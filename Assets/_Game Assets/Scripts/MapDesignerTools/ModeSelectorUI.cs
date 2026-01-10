using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace MapDesignerTool
{
    public class ModeSelectorUI : MonoBehaviour
    {
        [Header("References")]
        public GridPlacer gridPlacer;
        [Tooltip("Text element to display the current active mode")]
        public TextMeshProUGUI activeModeText;

        [Header("Global Sprites")]
        [Tooltip("Sprite to use for any button when it is ACTIVE")]
        public Sprite commonActiveSprite;
        [Tooltip("Sprite to use for any button when it is INACTIVE")]
        public Sprite commonInactiveSprite;

        [Header("Animation")]
        public float activeScale = 1.15f;
        public float normalScale = 1.0f;
        public float animDuration = 0.2f;

        [Header("Buttons")]
        public Button navigateButton;
        public Button demolishButton;
        public Button portalPairButton;
        public Button buttonDoorPairButton;
        public Button modifyButton;
        public Button resetCameraButton;

        [Header("Camera")]
        public OrbitCamera orbitCamera;

        void Start()
        {
            if (gridPlacer == null)
            {
                Debug.LogError("ModeSelectorUI: GridPlacer not assigned!");
                enabled = false;
                return;
            }

            // Bind Buttons
            if (navigateButton) navigateButton.onClick.AddListener(OnNavigateClicked);
            if (demolishButton) demolishButton.onClick.AddListener(OnDemolishClicked);
            if (portalPairButton) portalPairButton.onClick.AddListener(OnPortalPairClicked);
            if (buttonDoorPairButton) buttonDoorPairButton.onClick.AddListener(OnButtonDoorPairClicked);
            if (modifyButton) modifyButton.onClick.AddListener(OnModifyClicked);
            if (resetCameraButton) resetCameraButton.onClick.AddListener(OnResetCameraClicked);

            // Listen for changes
            gridPlacer.OnModeChanged += RefreshUI;
            
            // Initial State: Force Navigate Mode
            gridPlacer.SetNavigateMode();
            RefreshUI();
        }

        void OnDestroy()
        {
            if (gridPlacer != null)
                gridPlacer.OnModeChanged -= RefreshUI;
        }

        // --- Button Callbacks ---

        void OnNavigateClicked()
        {
            gridPlacer.SetNavigateMode();
        }

        void OnDemolishClicked()
        {
            gridPlacer.SetDemolishMode();
        }

        void OnPortalPairClicked()
        {
            gridPlacer.TogglePortalPairMode();
        }

        void OnModifyClicked()
        {
            gridPlacer.SetModifyMode();
        }

        void OnButtonDoorPairClicked()
        {
            gridPlacer.ToggleButtonDoorPairMode();
        }

        void OnResetCameraClicked()
        {
            if (orbitCamera != null)
                orbitCamera.ResetToDefault();
        }

        // --- UI Updates ---

        void RefreshUI()
        {
            // Update Text
            if (activeModeText != null)
            {
                if (gridPlacer.PortalPairMode)
                    activeModeText.text = "MODE: PORTAL PAIRING";
                else if (gridPlacer.ButtonDoorPairMode)
                    activeModeText.text = "MODE: BUTTON-DOOR PAIRING";
                else
                    activeModeText.text = $"MODE: {gridPlacer.currentMode.ToString().ToUpper()}";
            }

            // Update Button Visuals (Shared Sprite Swap + Scale)
            bool isNavigate = !gridPlacer.PortalPairMode && !gridPlacer.ButtonDoorPairMode && gridPlacer.currentMode == BuildMode.Navigate;
            UpdateButtonState(navigateButton, isNavigate);

            bool isDemolish = !gridPlacer.PortalPairMode && !gridPlacer.ButtonDoorPairMode && gridPlacer.currentMode == BuildMode.Demolish;
            UpdateButtonState(demolishButton, isDemolish);

            bool isPortalPair = gridPlacer.PortalPairMode;
            UpdateButtonState(portalPairButton, isPortalPair);

            bool isButtonDoorPair = gridPlacer.ButtonDoorPairMode;
            UpdateButtonState(buttonDoorPairButton, isButtonDoorPair);

            bool isModify = !gridPlacer.PortalPairMode && !gridPlacer.ButtonDoorPairMode && gridPlacer.currentMode == BuildMode.Modify;
            UpdateButtonState(modifyButton, isModify);
        }

        void UpdateButtonState(Button btn, bool isActive)
        {
            if (btn == null) return;
            
            // 1. Sprite Swap
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                if (isActive && commonActiveSprite != null)
                    img.sprite = commonActiveSprite;
                else if (!isActive && commonInactiveSprite != null)
                    img.sprite = commonInactiveSprite;
            }

            // 2. Scale Animation
            float targetScale = isActive ? activeScale : normalScale;
            btn.transform.DOKill(); // Kill any running tween on this button
            btn.transform.DOScale(targetScale, animDuration).SetEase(Ease.OutBack);
        }
    }
}
