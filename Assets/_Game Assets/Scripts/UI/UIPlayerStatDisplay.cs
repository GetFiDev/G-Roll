using TMPro;
using UnityEngine;
using DG.Tweening;

public class UIPlayerStatDisplay : MonoBehaviour
{
    public string statKey;
    public TextMeshProUGUI statValueText;
    // fetchingPanel removed

    private Tweener _tweener;

    public void ShowStat(string _statValue, bool _isPercent, bool _isDecimal)
    {
        if (_tweener != null) _tweener.Kill();

        float targetValue = 0f;
        if (!string.IsNullOrWhiteSpace(_statValue))
        {
             float.TryParse(_statValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out targetValue);
        }

        // Start from 0
        float startValue = 0f;
        UpdateText(startValue, _isPercent, _isDecimal);

        _tweener = DOVirtual.Float(startValue, targetValue, 0.5f, (val) =>
        {
            UpdateText(val, _isPercent, _isDecimal);
        });
    }

    private void UpdateText(float val, bool isPercent, bool isDecimal)
    {
        if (statValueText == null) return;
        
        string formatted = "";
        
        if (isDecimal)
        {
            formatted = val.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            // Integer display for non-decimal values
            formatted = Mathf.RoundToInt(val).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (isPercent)
        {
            formatted += "%";
        }

        statValueText.text = formatted;
    }
}
