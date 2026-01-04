using System.Collections;
using TMPro;
using UnityEngine;

public class UISpeedElement : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI speedText;
    
    private PlayerMovement _cachedPlayerMovement;

    public void RegisterPlayer(PlayerMovement player)
    {
        Debug.Log($"[UISpeedElement] Player registered: {player.name}");
        _cachedPlayerMovement = player;
        _currentDisplaySpeed = player.Speed;
        _velocity = 0f;
    }

    private float _currentDisplaySpeed;
    private float _velocity; // For SmoothDamp
    private Vector3 _originalScale;

    [Header("Animation Settings")]
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private float colorLerpSpeed = 5f;
    [SerializeField] private float scaleLerpSpeed = 5f;
    
    [Header("Colors")]
    [SerializeField] private Color increaseColor = Color.green;
    [SerializeField] private Color decreaseColor = Color.red;
    [SerializeField] private Color stableColor = Color.white;

    [Header("Scale Factors")]
    [SerializeField] private float increaseScale = 1.1f;
    [SerializeField] private float decreaseScale = 0.9f;

    private void Awake()
    {
        if (speedText != null)
        {
            _originalScale = speedText.transform.localScale;
        }
    }

    private void Update()
    {
        if (_cachedPlayerMovement == null)
        {
            speedText.text = "N/A";
            speedText.color = stableColor;
            if (speedText != null) speedText.transform.localScale = _originalScale;
            return;
        }

        float rawSpeed = _cachedPlayerMovement.Speed;
        float displayed = rawSpeed / Mathf.Max(0.01f, _cachedPlayerMovement.SpeedDisplayDivider);
        float targetSpeed = displayed * 10f;
        
        // 1. Value Smoothing
        float newSpeed = Mathf.SmoothDamp(_currentDisplaySpeed, targetSpeed, ref _velocity, smoothTime);
        float diff = newSpeed - _currentDisplaySpeed;
        _currentDisplaySpeed = newSpeed;
        speedText.text = _currentDisplaySpeed.ToString("F1");

        // 2. Determine Targets
        Color targetColor = stableColor;
        Vector3 targetScale = _originalScale;

        // Threshold for "changing"
        if (Mathf.Abs(targetSpeed - _currentDisplaySpeed) > 0.1f)
        {
            if (diff > 0.01f) 
            {
                targetColor = increaseColor;
                targetScale = _originalScale * increaseScale;
            }
            else if (diff < -0.01f)
            {
                targetColor = decreaseColor;
                targetScale = _originalScale * decreaseScale;
            }
        }

        // 3. Smooth Transitions
        speedText.color = Color.Lerp(speedText.color, targetColor, Time.deltaTime * colorLerpSpeed);
        speedText.transform.localScale = Vector3.Lerp(speedText.transform.localScale, targetScale, Time.deltaTime * scaleLerpSpeed);
    }
}
