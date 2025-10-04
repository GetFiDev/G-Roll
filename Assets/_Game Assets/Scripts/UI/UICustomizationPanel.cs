using UnityEngine;

public class UICustomizationPanel : MonoBehaviour
{
    [SerializeField] private UIPlayerStatsHandler statsHandler;

    private void OnEnable()
    {
        if (statsHandler == null)
            statsHandler = GetComponentInChildren<UIPlayerStatsHandler>(true);

        if (statsHandler != null)
            statsHandler.Initialize();
    }
}
