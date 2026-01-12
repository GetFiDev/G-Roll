using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace MapDesignerTool
{
    public class DraggableBuildItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI References (Assign in Prefab)")]
        public Image iconImage;
        public TextMeshProUGUI nameText;

        [Header("Data (Assigned at Runtime)")]
        public BuildableItem item; 

        [Header("Internal Refs")]
        private GridPlacer gridPlacer;
        private OrbitCamera orbitCam;
        private Canvas canvas;
        
        // Temporary UI for dragging
        private GameObject dragObject;
        private RectTransform dragRect;
        
        // Touch offset to keep item above finger
        private const float TOUCH_OFFSET_Y = 120f;
        
        // Edge pan settings - when finger gets close to screen edge, camera pans
        private const float EDGE_THRESHOLD = 0.15f; // 15% of screen from edges
        private const float PAN_SPEED = 8f; // Pan speed in world units per second
        
        // Store camera state for reset after placement
        private float savedDistance;
        private bool hasSavedCameraState;
        private bool isGhostVisible;
        
        // Drag state for Update loop
        private bool isDragging;
        private Vector2 lastPointerPosition;
        
        // Store the last placed object position
        private int lastPlacedGridZ;

        public void Init(BuildableItem item, GridPlacer placer, OrbitCamera cam, Canvas canvas)
        {
            this.item = item;
            this.gridPlacer = placer;
            this.orbitCam = cam;
            this.canvas = canvas;

            // Setup button visual using explicit references
            if (iconImage != null && item.icon != null)
                iconImage.sprite = item.icon;
            
            if (nameText != null)
                nameText.text = item.displayName;
        }
        
        void Update()
        {
            // Continuous edge-pan while dragging and ghost is visible
            if (isDragging && isGhostVisible)
            {
                HandleEdgePan(lastPointerPosition);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (gridPlacer == null || item == null) return;

            // 1. Block Camera manual input
            if (orbitCam) orbitCam.IsInputBlocked = true;

            // 2. Switch to Navigate (to stop any previous placement ghosts / logic)
            gridPlacer.SetNavigateMode();
            
            // 3. Save camera state (zoom only - position will be calculated based on placed object)
            if (orbitCam)
            {
                savedDistance = orbitCam.distance;
                hasSavedCameraState = true;
            }
            
            isGhostVisible = false;
            isDragging = true;
            lastPointerPosition = eventData.position;
            lastPlacedGridZ = 0;

            // 4. Create visual drag object (icon)
            dragObject = new GameObject("DragIcon");
            dragObject.transform.SetParent(canvas.transform, false);
            dragObject.transform.SetAsLastSibling();

            var img = dragObject.AddComponent<Image>();
            img.sprite = item.icon;
            img.raycastTarget = false;
            
            // Make it slightly transparent
            var c = img.color; c.a = 0.7f; img.color = c;

            dragRect = dragObject.GetComponent<RectTransform>();
            dragRect.sizeDelta = new Vector2(100, 100); 
            
            UpdateDragVisual(GetOffsetPosition(eventData.position));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragObject == null) return;

            // Store pointer position for Update loop
            lastPointerPosition = eventData.position;

            // Apply touch offset for mobile - makes item appear above finger
            Vector2 offsetPosition = GetOffsetPosition(eventData.position);
            
            UpdateDragVisual(offsetPosition);

            // Check if we are over the grid (using offset position)
            bool onGrid = gridPlacer.IsScreenPointOnGrid(offsetPosition, out int gx, out int gz);

            if (onGrid)
            {
                // Store the grid Z for camera return
                lastPlacedGridZ = gz;
                
                // First time entering grid - zoom to max smoothly
                if (!isGhostVisible && orbitCam)
                {
                    orbitCam.SmoothZoomToMax(0.25f);
                }
                isGhostVisible = true;
                
                // Show 3D Ghost
                gridPlacer.ShowGhostAt(item, gx, gz);
                // Hide 2D Drag Icon
                dragObject.SetActive(false);
            }
            else
            {
                isGhostVisible = false;
                
                // Show 2D Drag Icon
                dragObject.SetActive(true);
                // Hide 3D Ghost
                gridPlacer.HideGhost();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            
            // Cleanup UI
            if (dragObject) Destroy(dragObject);
            dragObject = null;

            // Apply touch offset for final placement
            Vector2 offsetPosition = GetOffsetPosition(eventData.position);

            // Final Placement Check (using offset position)
            float placedWorldZ = 0f;
            bool wasPlaced = false;
            if (gridPlacer.IsScreenPointOnGrid(offsetPosition, out int gx, out int gz))
            {
                wasPlaced = gridPlacer.PlaceAt(item, gx, gz);
                if (wasPlaced)
                {
                    // Calculate world Z from grid Z
                    placedWorldZ = gridPlacer.GridToWorld(0, gz).z;
                }
            }

            // Always hide ghost at end
            gridPlacer.HideGhost();
            
            // Reset camera after placement
            if (orbitCam && orbitCam.pivot && hasSavedCameraState)
            {
                // Determine target Z: placed object's Z if placed, otherwise current pivot Z
                float targetZ = wasPlaced ? placedWorldZ : orbitCam.pivot.position.z;
                
                // Smoothly return to X=0 (rail center), Y=0, Z=placed object's position
                Vector3 targetPos = new Vector3(0f, 0f, targetZ);
                orbitCam.pivot.DOMove(targetPos, 0.4f).SetEase(Ease.OutCubic);
                
                // Restore original zoom
                orbitCam.SmoothZoomTo(savedDistance, 0.3f);
                
                hasSavedCameraState = false;
            }

            // Cleanup Camera input block
            if (orbitCam) orbitCam.IsInputBlocked = false;
            
            isGhostVisible = false;
        }
        
        /// <summary>
        /// Handles camera panning when finger is near screen edges.
        /// Pan direction is relative to the camera's view orientation.
        /// </summary>
        private void HandleEdgePan(Vector2 screenPos)
        {
            if (orbitCam == null) return;
            
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            
            // Normalize position to 0-1 range
            float normalizedX = screenPos.x / screenWidth;
            float normalizedY = screenPos.y / screenHeight;
            
            // Calculate pan direction in SCREEN space (like a joystick)
            float screenPanX = 0f; // Left/Right on screen
            float screenPanY = 0f; // Up/Down on screen
            
            // Horizontal edges (left/right)
            if (normalizedX < EDGE_THRESHOLD)
            {
                float intensity = 1f - (normalizedX / EDGE_THRESHOLD);
                screenPanX = -intensity; // Pan left
            }
            else if (normalizedX > (1f - EDGE_THRESHOLD))
            {
                float intensity = (normalizedX - (1f - EDGE_THRESHOLD)) / EDGE_THRESHOLD;
                screenPanX = intensity; // Pan right
            }
            
            // Vertical edges (top/bottom)
            if (normalizedY < EDGE_THRESHOLD)
            {
                float intensity = 1f - (normalizedY / EDGE_THRESHOLD);
                screenPanY = -intensity; // Pan down/backward
            }
            else if (normalizedY > (1f - EDGE_THRESHOLD))
            {
                float intensity = (normalizedY - (1f - EDGE_THRESHOLD)) / EDGE_THRESHOLD;
                screenPanY = intensity; // Pan up/forward
            }
            
            // No pan needed
            if (Mathf.Abs(screenPanX) < 0.001f && Mathf.Abs(screenPanY) < 0.001f)
                return;
            
            // Convert screen-space pan direction to world-space pan direction
            // based on camera's orientation (projected onto XZ plane)
            Transform camTransform = orbitCam.transform;
            
            // Camera's right vector projected onto XZ plane (for horizontal screen movement)
            Vector3 camRight = camTransform.right;
            camRight.y = 0;
            camRight.Normalize();
            
            // Camera's forward vector projected onto XZ plane (for vertical screen movement)
            Vector3 camForward = camTransform.forward;
            camForward.y = 0;
            camForward.Normalize();
            
            // Calculate world-space pan delta
            Vector3 panDelta = (camRight * screenPanX + camForward * screenPanY) * PAN_SPEED * Time.deltaTime;
            
            // Apply pan
            orbitCam.PanPivotDelta(panDelta.x, panDelta.z);
        }

        /// <summary>
        /// Gets position with touch offset applied (for mobile, item appears above finger)
        /// </summary>
        private Vector2 GetOffsetPosition(Vector2 screenPos)
        {
            // Apply offset on touch devices, or always in Editor for testing
#if UNITY_EDITOR
            // Always apply offset in Editor for testing
            return screenPos + new Vector2(0, TOUCH_OFFSET_Y);
#else
            // Only apply offset on touch devices at runtime
            bool isTouchInput = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0;
            if (isTouchInput)
            {
                return screenPos + new Vector2(0, TOUCH_OFFSET_Y);
            }
            return screenPos;
#endif
        }

        private void UpdateDragVisual(Vector2 screenPos)
        {
            if (dragRect == null) return;
            
            // Convert screen point to local point in canvas
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, 
                screenPos, 
                canvas.worldCamera, 
                out var localPos
            );
            
            dragRect.anchoredPosition = localPos;
        }
    }
}
