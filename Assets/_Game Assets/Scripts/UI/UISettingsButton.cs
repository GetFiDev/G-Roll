using UnityEngine;

public class UISettingsButton : MonoBehaviour
{
    public void OnSettingsButtonClicked()
    {
        UIManager.Instance.settings.Show();
    }
}
