using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ToggleButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private Image toggleMainImage;
    [SerializeField] private Sprite toggleMainOn, toggleMainOff;

    [SerializeField] private bool hasKnob = true;
    [SerializeField, ShowIf(nameof(hasKnob))] private Transform toggleKnob;
    [SerializeField, ShowIf(nameof(hasKnob))] private Transform knobOnPosition, knobOffPosition;

    [SerializeField, ReadOnly] private bool _value;
    
    [Header("Events")]
    [SerializeField] private UnityEvent OnToggle; // Parametresiz event - Inspector'da kolay bağlanır
    [SerializeField] private UnityEvent<bool> OnValueChange; // Parametreli event

    /// <summary>
    /// Current value of the toggle (true = on, false = off)
    /// </summary>
    public bool Value => _value;

    public void SetValue(bool state)
    {
        Debug.Log($"[ToggleButton] SetValue called with state={state}, current _value={_value}");
        _value = state;

        toggleMainImage.sprite = _value ? toggleMainOn : toggleMainOff;

        if (hasKnob)
            toggleKnob.DOMove(_value ? knobOnPosition.position : knobOffPosition.position, .1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[ToggleButton] OnPointerDown START: _value={_value}, will toggle to {!_value}");
        SetValue(!_value);
        Debug.Log($"[ToggleButton] OnPointerDown AFTER SetValue: _value={_value}");

        if (GameManager.Instance != null && GameManager.Instance.audioManager != null)
            GameManager.Instance.audioManager.PlayUIButtonClick();
        HapticManager.GenerateHaptic(PresetType.Selection);
        
        OnToggle?.Invoke();
        OnValueChange?.Invoke(_value);
        Debug.Log($"[ToggleButton] OnPointerDown END: Events invoked with _value={_value}");
    }
}