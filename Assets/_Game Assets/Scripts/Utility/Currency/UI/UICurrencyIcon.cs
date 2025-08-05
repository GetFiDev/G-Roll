using UnityEngine;
using UnityEngine.UI;

public class UICurrencyIcon : MonoBehaviour
{
    [SerializeField] protected Image currencyImage;
    [SerializeField] protected CurrencyType currencyType;


    private void Start()
    {
        UpdateCurrencyReferenceInEditor();
    }

    private void OnValidate()
    {
        UpdateCurrencyReferenceInEditor();
    }

    private void UpdateCurrencyReferenceInEditor()
    {
        var data = CurrencyManager.GetData(currencyType);
        if (data != null && currencyImage != null)
        {
            currencyImage.sprite = data.Icon;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}