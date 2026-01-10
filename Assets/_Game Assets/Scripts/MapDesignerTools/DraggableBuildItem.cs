using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (gridPlacer == null || item == null) return;

            // 1. Block Camera
            if (orbitCam) orbitCam.IsInputBlocked = true;

            // 2. Switch to Navigate (to stop any previous placement ghosts / logic)
            gridPlacer.SetNavigateMode();

            // 3. Create visual drag object (icon)
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
            
            UpdateDragVisual(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragObject == null) return;

            UpdateDragVisual(eventData.position);

            // Check if we are over the grid
            bool onGrid = gridPlacer.IsScreenPointOnGrid(eventData.position, out int gx, out int gz);

            if (onGrid)
            {
                // Show 3D Ghost
                gridPlacer.ShowGhostAt(item, gx, gz);
                // Hide 2D Drag Icon
                dragObject.SetActive(false);
            }
            else
            {
                // Show 2D Drag Icon
                dragObject.SetActive(true);
                // Hide 3D Ghost
                gridPlacer.HideGhost();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Cleanup UI
            if (dragObject) Destroy(dragObject);
            dragObject = null;

            // Cleanup Camera
            if (orbitCam) orbitCam.IsInputBlocked = false;

            // Final Placement Check
            if (gridPlacer.IsScreenPointOnGrid(eventData.position, out int gx, out int gz))
            {
                gridPlacer.PlaceAt(item, gx, gz);
            }

            // Always hide ghost at end
            gridPlacer.HideGhost();
        }

        private void UpdateDragVisual(Vector2 screenPos)
        {
            if (dragRect == null) return;

            // Add offset if needed (user mentioned "finger's slightly above")
            Vector2 offset = new Vector2(0, 150); 
            
            // Convert screen point to local point in canvas
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, 
                screenPos + offset, 
                canvas.worldCamera, 
                out var localPos
            );
            
            dragRect.anchoredPosition = localPos;
        }
    }
}
