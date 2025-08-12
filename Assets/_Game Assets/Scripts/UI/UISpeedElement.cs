using System.Collections;
using TMPro;
using UnityEngine;

public class UISpeedElement : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI speedText;
    
    private IEnumerator Start()
    {
        var updateInterval = new WaitForSeconds(.1f);

        while (enabled)
        {
            yield return updateInterval;

            speedText.text = Mathf.CeilToInt(PlayerController.Instance.playerMovement.Speed * 100).ToString();
        }
    }
}
