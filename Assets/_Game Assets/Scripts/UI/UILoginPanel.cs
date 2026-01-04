using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UILoginPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        gameObject.SetActive(true);
    }

}