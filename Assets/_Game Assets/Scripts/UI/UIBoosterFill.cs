using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UIBoosterFill : MonoBehaviour
{
    [SerializeField] private Slider boosterSlider;

    [SerializeField, Range(0f, 1f)] private float boosterMinValueToUse;
    [SerializeField, Range(0f, 1f)] private float boosterValuePerCurrency;

    [SerializeField, ReadOnly] private float boosterValue;

    private void Start()
    {
        CurrencyEvents.OnCollected.Subscribe(CurrencyType.SoftCurrency, OnCollected);
        UpdateUI();
    }

    private void OnCollected(CurrencyCollectedData collectedData)
    {
        boosterValue = Mathf.Clamp(boosterValue + collectedData.Amount * boosterValuePerCurrency, 0, 1f);
        UpdateUI();
    }

    public void OnBoosterButtonClicked()
    {
        if (boosterValue < boosterMinValueToUse)
            return;

        //PlayerController.Instance.playerMovement.Boost(boosterValue);

        boosterValue = 0;

        UpdateUI();
    }

    private void UpdateUI()
    {
        boosterSlider.DOKill();
        boosterSlider.DOValue(boosterValue, .1f);
    }

    public void InstantFill()
    {
        boosterValue = 1f;

        UpdateUI();
    }
}