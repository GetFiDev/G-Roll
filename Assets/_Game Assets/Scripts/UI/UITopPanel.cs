using UnityEngine;
using UnityEngine.UI;

public class UITopPanel : MonoBehaviour
{
    public UIBottomPanel bottomPanel;
    [SerializeField] private Image settingsIcon;
    [SerializeField] private Image profileIcon;
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color selectedColor;
    public void OnSettingsButtonClick()
    {
        ChangeAllColorToDefault();
        bottomPanel.ChangeAllColorToDefault();

        settingsIcon.color = selectedColor;

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Settings);
    }

    public void OnProfileButtonClick()
    {
        ChangeAllColorToDefault();
        bottomPanel.ChangeAllColorToDefault();

        profileIcon.color = selectedColor;

        UIManager.Instance.mainMenu.ShowPanel(UIMainMenu.PanelType.Profile);
    }

    public void ChangeAllColorToDefault()
    {
        settingsIcon.color = defaultColor;
        profileIcon.color = defaultColor;
    }
}
