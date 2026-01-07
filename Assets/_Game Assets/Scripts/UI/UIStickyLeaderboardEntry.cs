using UnityEngine;

public class UIStickyLeaderboardEntry : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UILeaderboardDisplay visualComponent;

    [Header("Behavior")]
    [SerializeField] private float topThresholdY = -50f; 
    [SerializeField] private float bottomThresholdY = 50f;
    
    // State
    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    public void Refresh()
    {
        // 1. Get My Data
        var lm = LeaderboardManager.Instance;
        if (lm == null) return;
        
        // Update visual
        if (visualComponent)
        {
            visualComponent.SetData(lm.MyRank, lm.MyUsername, lm.MyScore, lm.MyIsElite, lm.MyPhotoUrl);
        }
    }

    public void DockTop()
    {
        if (!_rect) _rect = GetComponent<RectTransform>();
        
        // Top-Center
        _rect.anchorMin = new Vector2(0.5f, 1f);
        _rect.anchorMax = new Vector2(0.5f, 1f);
        _rect.pivot = new Vector2(0.5f, 1f);
        _rect.anchoredPosition = new Vector2(0, topThresholdY);
    }

    public void DockBottom()
    {
        if (!_rect) _rect = GetComponent<RectTransform>();

        // Bottom-Center
        _rect.anchorMin = new Vector2(0.5f, 0f);
        _rect.anchorMax = new Vector2(0.5f, 0f);
        _rect.pivot = new Vector2(0.5f, 0f);
        _rect.anchoredPosition = new Vector2(0, bottomThresholdY);
    }
}
