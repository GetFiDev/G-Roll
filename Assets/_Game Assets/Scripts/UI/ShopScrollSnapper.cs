using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopScrollSnapper : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public UIShopPanel shopPanel;
    public ScrollRect scrollRect;
    public int panelCount;

    private int _startIndex;

    private void Start()
    {
        if (scrollRect != null)
        {
            scrollRect.inertia = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (panelCount <= 1) return;
        float step = 1f / (panelCount - 1);
        _startIndex = Mathf.RoundToInt(scrollRect.normalizedPosition.x / step);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (shopPanel == null || scrollRect == null || panelCount <= 1) return;

        float step = 1f / (panelCount - 1);
        float currentPos = scrollRect.normalizedPosition.x;
        float startPos = _startIndex * step;
        
        float diff = currentPos - startPos;
        
        // Threshold: 25% of the panel width (which is 'step')
        // If we moved more than 25% to the right (positive), go next
        // If we moved more than 25% to the left (negative), go prev
        float threshold = step * 0.25f;
        
        int targetIndex = _startIndex;
        
        if (diff > threshold)
        {
            targetIndex = _startIndex + 1;
        }
        else if (diff < -threshold)
        {
            targetIndex = _startIndex - 1;
        }
        
        targetIndex = Mathf.Clamp(targetIndex, 0, panelCount - 1);
        
        shopPanel.SnapToPage(targetIndex);
    }
}
