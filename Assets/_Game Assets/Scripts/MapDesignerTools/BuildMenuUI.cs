using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BuildMenuUI : MonoBehaviour
{
    public BuildDatabase database;
    public GridPlacer placer;
    public Transform buttonContainer;
    public Button buttonTemplate;

    void Start()
    {
        foreach (Transform c in buttonContainer) Destroy(c.gameObject);
        buttonTemplate.gameObject.SetActive(false);

        foreach (var item in database.items)
        {
            var btn = Instantiate(buttonTemplate, buttonContainer);
            btn.gameObject.SetActive(true);
            var txt = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            var img = btn.GetComponentsInChildren<Image>(includeInactive: true)
                 .FirstOrDefault(img => img.gameObject != btn.gameObject);

            if (txt) txt.text = item.displayName;
            if (img && item.icon) img.sprite = item.icon;

            btn.onClick.AddListener(() => placer.BeginPlacement(item));
        }
    }
}