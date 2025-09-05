using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class DisableScrollViewInEditor : MonoBehaviour
{
    [ShowInInspector, ReadOnly] private ScrollRect _scrollRect;

    private void Awake()
    {
        if (TryGetComponent(out ScrollRect scrollRect))
            scrollRect.enabled = true;
    }

    private void OnValidate()
    {
        _scrollRect = GetComponent<ScrollRect>();
        
        _scrollRect.enabled = false;
    }
}
