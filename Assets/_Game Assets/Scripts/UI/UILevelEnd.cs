using UnityEngine;
using DG.Tweening;
using System;

public class UILevelEnd : MonoBehaviour
{
    [Header("Session Failed Panel")] 
    [SerializeField] private GameObject sessionFailedPanel;
    [SerializeField] private CanvasGroup sessionFailedGroup;

    [Header("Result Panel")] 
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private CanvasGroup resultGroup;

    [Header("Sequence Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float autoAdvanceDelay = 0.0f; // 0 => sadece butonla ilerle
    [SerializeField] private bool proceedWithAnyTap = false;  // true ise sessionFailed ekranında herhangi bir tap ile sonuca geç

    public event Action OnSequenceCompleted; // GameplayManager dinler → EndSession(false)

    private bool _isRunning;
    private bool _atResult;

    private void Reset()
    {
        if (sessionFailedPanel != null) sessionFailedGroup = sessionFailedPanel.GetComponent<CanvasGroup>();
        if (resultPanel != null) resultGroup = resultPanel.GetComponent<CanvasGroup>();
    }

    public void ShowSequence(bool isSuccess)
    {
        // Bu UI sadece roguelike fail akışı için kullanılıyor; isSuccess true gelirse yine de result'a geçeriz.
        gameObject.SetActive(true);

        // İlk durumda: SessionFailed ekranda, Result kapalı
        PreparePanel(sessionFailedPanel, sessionFailedGroup, visible:true);
        PreparePanel(resultPanel, resultGroup, visible:false);

        _isRunning = true;
        _atResult = false;

        // Auto-advance veya kullanıcı girişini bekle
        if (autoAdvanceDelay > 0f)
            Invoke(nameof(ProceedToResult), autoAdvanceDelay);
    }

    private void PreparePanel(GameObject panel, CanvasGroup cg, bool visible)
    {
        if (panel == null) return;
        if (cg == null) cg = panel.GetComponent<CanvasGroup>();
        panel.SetActive(true);
        cg.alpha = visible ? 1f : 0f;
        panel.SetActive(visible);
    }

    public void ProceedToResult()
    {
        if (!_isRunning || _atResult) return;
        _atResult = true;

        // SessionFailed -> fade out; Result -> fade in
        if (sessionFailedPanel != null)
        {
            sessionFailedPanel.SetActive(true);
            var cgA = sessionFailedGroup != null ? sessionFailedGroup : sessionFailedPanel.GetComponent<CanvasGroup>();
            if (cgA != null)
                cgA.DOFade(0f, fadeDuration).OnComplete(() => sessionFailedPanel.SetActive(false));
            else
                sessionFailedPanel.SetActive(false);
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            var cgB = resultGroup != null ? resultGroup : resultPanel.GetComponent<CanvasGroup>();
            if (cgB != null)
            {
                cgB.alpha = 0f;
                cgB.DOFade(1f, fadeDuration);
            }
        }
    }

    public void CompleteAndClose()
    {
        if (!_isRunning) return;
        _isRunning = false;
        // UI kapanır, GameplayManager devamını getirir
        gameObject.SetActive(false);
        OnSequenceCompleted?.Invoke();
    }

    // --- UI Button hooks ---
    public void OnTapAnywhere()
    {
        if (!_isRunning) return;
        if (proceedWithAnyTap && !_atResult)
        {
            ProceedToResult();
        }
    }

    public void OnContinueButton() => ProceedToResult();
    public void OnCloseButton()     => CompleteAndClose();
}