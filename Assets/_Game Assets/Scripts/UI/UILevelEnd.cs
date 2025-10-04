using UnityEngine;
using DG.Tweening;
using System;
using TMPro;

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

    [Header("Result Values UI")]
    [SerializeField] private TextMeshProUGUI resultScoreText;
    [SerializeField] private TextMeshProUGUI resultCoinText;
    [SerializeField] private TextMeshProUGUI resultCoinDoubleText; // kazanılanın 2x gösterimi
    [SerializeField] private bool scoreAsInteger = true;
    [SerializeField] private bool coinAsInteger = true;

    [Header("Result Animation")]
    [SerializeField] private float countUpDuration = 1.5f;
    [SerializeField] private float punchScale = 1.15f;
    [SerializeField] private float punchTime = 0.12f;

    // runtime targets (SetResultValues ile doldurulur)
    private float _targetScore = 0f;
    private float _targetCoins = 0f;
    private bool _hasTargets = false;

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

        // Result sayacı başlangıç görünümü (0'dan başlasın)
        if (resultScoreText) resultScoreText.text = scoreAsInteger ? "0" : 0f.ToString("F2");
        if (resultCoinText)  resultCoinText.text  = coinAsInteger  ? "0" : 0f.ToString("F2");
        if (resultCoinDoubleText) resultCoinDoubleText.text = coinAsInteger ? "0" : 0f.ToString("F2");

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

        // Result paneli görünür olduktan sonra sayacı başlat
        AnimateResults();
    }

    private void AnimateResults()
    {
        if (!_hasTargets)
        {
            // Hedefler dışarıdan set edilmemişse 0 olarak kalır; animasyon atlanır
            return;
        }

        // Skor
        if (resultScoreText)
        {
            resultScoreText.transform.DOKill();
            resultScoreText.text = scoreAsInteger ? "0" : 0f.ToString("F2");
            DOVirtual.Float(0f, _targetScore, countUpDuration, v =>
            {
                if (scoreAsInteger)
                    resultScoreText.text = Mathf.FloorToInt(v).ToString();
                else
                    resultScoreText.text = v.ToString("F2");
            }).OnComplete(() =>
            {
                // küçük bir büyüyüp geri gelme efekti
                var t = resultScoreText.transform;
                var baseScale = t.localScale;
                t.DOKill();
                t.localScale = baseScale;
                t.DOScale(baseScale * punchScale, punchTime).SetEase(Ease.OutQuad)
                 .OnComplete(() => t.DOScale(baseScale, punchTime).SetEase(Ease.InQuad));
            });
        }

        // Coin
        if (resultCoinText)
        {
            resultCoinText.transform.DOKill();
            resultCoinText.text = coinAsInteger ? "0" : 0f.ToString("F2");
            DOVirtual.Float(0f, _targetCoins, countUpDuration, v =>
            {
                if (coinAsInteger)
                    resultCoinText.text = Mathf.FloorToInt(v).ToString();
                else
                    resultCoinText.text = v.ToString("F2");
            }).OnComplete(() =>
            {
                var t = resultCoinText.transform;
                var baseScale = t.localScale;
                t.DOKill();
                t.localScale = baseScale;
                t.DOScale(baseScale * punchScale, punchTime).SetEase(Ease.OutQuad)
                 .OnComplete(() => t.DOScale(baseScale, punchTime).SetEase(Ease.InQuad));
            });
        }

        // Coin (2x)
        if (resultCoinDoubleText)
        {
            float doubleTarget = _targetCoins * 2f;
            resultCoinDoubleText.transform.DOKill();
            resultCoinDoubleText.text = coinAsInteger ? "0" : 0f.ToString("F2");
            DOVirtual.Float(0f, doubleTarget, countUpDuration, v =>
            {
                if (coinAsInteger)
                    resultCoinDoubleText.text = Mathf.FloorToInt(v).ToString();
                else
                    resultCoinDoubleText.text = v.ToString("F2");
            }).OnComplete(() =>
            {
                var t = resultCoinDoubleText.transform;
                var baseScale = t.localScale;
                t.DOKill();
                t.localScale = baseScale;
                t.DOScale(baseScale * punchScale, punchTime).SetEase(Ease.OutQuad)
                 .OnComplete(() => t.DOScale(baseScale, punchTime).SetEase(Ease.InQuad));
            });
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

    /// <summary>
    /// GameplayManager, sekans açılmadan önce final skor/coin değerlerini buraya set etsin.
    /// </summary>
    public void SetResultValues(float finalScore, float finalCoins)
    {
        _targetScore = Mathf.Max(0f, finalScore);
        _targetCoins = Mathf.Max(0f, finalCoins);
        _hasTargets = true;
    }

    /// <summary>
    /// (Opsiyonel) Dışarıdan verilmiş değerleri temizler.
    /// </summary>
    public void ClearResultValues()
    {
        _hasTargets = false;
        _targetScore = 0f;
        _targetCoins = 0f;
    }

    private void OnDisable()
    {
        if (resultScoreText) resultScoreText.transform.DOKill();
        if (resultCoinText)  resultCoinText.transform.DOKill();
        if (resultCoinDoubleText) resultCoinDoubleText.transform.DOKill();
        CancelInvoke();
    }
}