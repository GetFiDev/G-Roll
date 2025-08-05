using System;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "New Currency Data", menuName = "Scriptable Object / Currency Data")]
public class CurrencyData : ScriptableObjectWithID, IHasIcon
{
    public Action<int> OnCurrencyUpdated;

    public CurrencyType CurrencyType => currencyType;
    [SerializeField] private CurrencyType currencyType;

    public Sprite Icon => icon;
    [PreviewField(64), SerializeField] private Sprite icon;

    [SerializeField] private int defaultValue;

    [ShowInInspector, ReadOnly] public int Value
    {
        get => PlayerPrefs.GetInt(ObjectID, defaultValue);
        set
        {
            PlayerPrefs.SetInt(ObjectID, value);
            OnCurrencyUpdated?.Invoke(value);
        }
    }

    public void Reset() => ResetToDefault();

    [HorizontalGroup("Change Values")]
    [GUIColor(1f, .4f, 1f)]
    [Button("Increase + 1000", ButtonSizes.Large, ButtonStyle.Box)] 
    private void IncreaseValue() => Value += 1000;

    [HorizontalGroup("Change Values")]
    [GUIColor(1f, .4f, 1f)]
    [Button("Reset to Default", ButtonSizes.Large, ButtonStyle.Box)]
    private void ResetToDefault() => Value = defaultValue;
    
    [HorizontalGroup("Change Values")]
    [GUIColor(1f, .4f, 1f)]
    [Button("Reset to Zero", ButtonSizes.Large, ButtonStyle.Box)]
    private void ResetToZero() => Value = 0;
}