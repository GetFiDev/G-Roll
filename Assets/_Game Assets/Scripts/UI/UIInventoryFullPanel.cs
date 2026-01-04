using UnityEngine;
using UnityEngine.UI;

public class UIInventoryFullPanel : MonoBehaviour
{
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void OnCloseClicked()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
