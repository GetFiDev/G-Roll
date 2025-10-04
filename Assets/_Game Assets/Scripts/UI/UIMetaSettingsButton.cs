using UnityEngine;

public class UIMetaSettingsButton : MonoBehaviour
{
    public void OnSettingsButtonClicked()
    {
        UIManager.Instance.metaSettingsPanel.Show();
    }
}
