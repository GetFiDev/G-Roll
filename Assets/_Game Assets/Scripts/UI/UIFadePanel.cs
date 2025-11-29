using UnityEngine;
using DG.Tweening;
using System;

[RequireComponent(typeof(CanvasGroup))]
public class UIFadePanel : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] protected float fadeDuration = 0.1f;
    [SerializeField] protected Ease fadeEase = Ease.OutQuad;
    [SerializeField] protected bool startHidden = false;

    protected CanvasGroup canvasGroup;
    protected bool isVisible;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        // Only hide if requested AND we are not already marked as visible (e.g. by a preceding Show call)
        if (startHidden && !isVisible)
        {
            SetVisible(false, true);
        }
    }

    public virtual void Show(bool instant = false)
    {
        SetVisible(true, instant);
    }

    public virtual void Hide(bool instant = false)
    {
        SetVisible(false, instant);
    }

    protected virtual void SetVisible(bool visible, bool instant)
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        isVisible = visible;
        
        // Ensure GameObject is active so coroutines/tweens run
        if (visible) gameObject.SetActive(true);

        canvasGroup.DOKill();

        float targetAlpha = visible ? 1f : 0f;

        if (instant)
        {
            canvasGroup.alpha = targetAlpha;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (!visible) gameObject.SetActive(false);
        }
        else
        {
            if (visible)
            {
                // Fading In
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.DOFade(1f, fadeDuration).SetEase(fadeEase).SetUpdate(true);
            }
            else
            {
                // Fading Out
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.DOFade(0f, fadeDuration).SetEase(fadeEase).SetUpdate(true)
                    .OnComplete(() => gameObject.SetActive(false));
            }
        }
    }
}
