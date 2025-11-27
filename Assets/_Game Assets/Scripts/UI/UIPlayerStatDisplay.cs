using TMPro;
using UnityEngine;
using System.Collections;

public class UIPlayerStatDisplay : MonoBehaviour
{
    public string statKey;
    public TextMeshProUGUI statValueText;
    public GameObject fetchingPanel;

    private Coroutine _routine;

    public void ShowStat(string _statValue, bool _isPercent, bool _isDecimal)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowStatRoutine(_statValue, _isPercent, _isDecimal));
    }

    private IEnumerator ShowStatRoutine(string rawValue, bool isPercent, bool isDecimal)
    {
        if (fetchingPanel != null) fetchingPanel.SetActive(true);

        yield return new WaitForSeconds(0.5f);

        if (fetchingPanel != null) fetchingPanel.SetActive(false);

        string output = FormatValue(rawValue, isPercent, isDecimal);
        if (statValueText != null) statValueText.text = output;

        _routine = null;
    }

    private string FormatValue(string rawValue, bool isPercent, bool isDecimal)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) rawValue = "0";

        string formatted = rawValue;

        if (isDecimal)
        {
            if (float.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float f))
            {
                formatted = f.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (float.TryParse(rawValue, out f))
            {
                formatted = f.ToString("F1");
            }
        }

        if (isPercent)
        {
            formatted = formatted + "%";
        }

        return formatted;
    }
}
