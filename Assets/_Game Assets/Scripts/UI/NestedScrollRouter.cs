using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NestedScrollRouter : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ScrollRect parentScrollRect;
    private ScrollRect _myScrollRect;
    private bool _routeToParent;
    private ShopScrollSnapper _parentSnapper;

    private void Awake()
    {
        _myScrollRect = GetComponent<ScrollRect>();
    }

    private void Start()
    {
        if (parentScrollRect != null)
        {
            _parentSnapper = parentScrollRect.GetComponent<ShopScrollSnapper>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentScrollRect == null) return;

        // Check if movement is predominantly horizontal
        if (Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y))
        {
            _routeToParent = true;
            if (_myScrollRect) _myScrollRect.enabled = false; // Disable vertical scroll
            
            parentScrollRect.OnBeginDrag(eventData);
            if (_parentSnapper != null) _parentSnapper.OnBeginDrag(eventData);
        }
        else
        {
            _routeToParent = false;
            if (_myScrollRect) _myScrollRect.OnBeginDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_routeToParent && parentScrollRect != null)
        {
            parentScrollRect.OnDrag(eventData);
            // Snapper usually doesn't need OnDrag, but if it did, we'd call it here.
        }
        else if (_myScrollRect)
        {
            _myScrollRect.OnDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_routeToParent && parentScrollRect != null)
        {
            parentScrollRect.OnEndDrag(eventData);
            if (_parentSnapper != null) _parentSnapper.OnEndDrag(eventData);
            
            if (_myScrollRect) _myScrollRect.enabled = true; // Re-enable vertical scroll
        }
        else if (_myScrollRect)
        {
            _myScrollRect.OnEndDrag(eventData);
        }
        _routeToParent = false;
    }
}
