using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICurrencyDisplay : MonoBehaviour
{
    [SerializeField] protected Image currencyImage;
    [SerializeField] protected TextMeshProUGUI currencyText;
    
    [SerializeField] protected CurrencyType currencyType;
    
    protected CurrencyData CurrencyData;
    
    protected virtual void OnEnable()
    {
        CurrencyData = CurrencyManager.GetData(currencyType);

        if (CurrencyData == null)
        {
            Debug.LogWarning($"Currency Data Not Found for Type : {currencyType}");
            
            enabled = false;
            return;
        }

        CurrencyData.OnCurrencyUpdated += UpdateCurrencyText;

        currencyImage.sprite = CurrencyData.Icon;
        UpdateCurrencyText(CurrencyData.Value);
    }

    private void Start()
    {
        UpdateCurrencyText(CurrencyData.Value);
    }

    private void UpdateCurrencyText(int value)
    {
        currencyText.text = value.LargeIntToString();
    }
    
    protected virtual void OnDisable()
    {
        if (CurrencyData != null)
            CurrencyData.OnCurrencyUpdated -= UpdateCurrencyText;
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