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

    [Header("Chapter - Level Completed Panel")]
    [SerializeField] private GameObject levelCompletedPanel;
    [SerializeField] private CanvasGroup levelCompletedGroup;
    [SerializeField] private TextMeshProUGUI completedScoreText;
    [SerializeField] private TextMeshProUGUI completedCoinText;
    [SerializeField] private TextMeshProUGUI completedX2CoinText;
    [SerializeField] private GameObject claimX2Button; // To hide if ad not available or already claimed

    [Header("Chapter - Failed Panel")]
    [SerializeField] private GameObject chapterFailedPanel;
    [SerializeField] private CanvasGroup chapterFailedGroup;
    [SerializeField] private TextMeshProUGUI failedScoreText;
    [SerializeField] private TextMeshProUGUI failedCoinText;
    [SerializeField] private GameObject failedClaimButton;

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
        if (levelCompletedPanel != null) levelCompletedGroup = levelCompletedPanel.GetComponent<CanvasGroup>();
        if (chapterFailedPanel != null) chapterFailedGroup = chapterFailedPanel.GetComponent<CanvasGroup>();
    }

    public void ShowSequence(bool isSuccess)
    {
        // Determin current mode logic
        // We assume GameplayManager sets mode beforehand or we check it here?
        // Ideally GameplayManager triggers this.
        // For simplicity: If isSuccess -> assume Chapter Win (Endless never calls success)
        // If !isSuccess -> Check mode? Or uses generic fail?
        
        GameMode mode = GameplayManager.Instance ? GameplayManager.Instance.CurrentMode : GameMode.Endless;

        gameObject.SetActive(true);
        _isRunning = true;
        _atResult = false;

        // Reset Panels
        PreparePanel(sessionFailedPanel, sessionFailedGroup, false);
        PreparePanel(resultPanel, resultGroup, false);
        PreparePanel(levelCompletedPanel, levelCompletedGroup, false);
        PreparePanel(chapterFailedPanel, chapterFailedGroup, false);

        if (isSuccess && mode == GameMode.Chapter)
        {
            // --- CHAPTER WIN FLOW ---
            PreparePanel(levelCompletedPanel, levelCompletedGroup, true);
            AnimateChapterCompletion(true);
        }
        else if (!isSuccess && mode == GameMode.Chapter)
        {
            // --- CHAPTER FAIL FLOW ---
            PreparePanel(chapterFailedPanel, chapterFailedGroup, true);
            AnimateChapterCompletion(false);
        }
        else
        {
            // --- ENDLESS / DEFAULT FAIL FLOW ---
            // İlk durumda: SessionFailed ekranda, Result kapalı
            PreparePanel(sessionFailedPanel, sessionFailedGroup, visible:true);
            
             // Result sayacı başlangıç görünümü (0'dan başlasın)
            if (resultScoreText) resultScoreText.text = scoreAsInteger ? "0" : 0f.ToString("F2");
            if (resultCoinText)  resultCoinText.text  = coinAsInteger  ? "0" : 0f.ToString("F2");
            if (resultCoinDoubleText) resultCoinDoubleText.text = coinAsInteger ? "0" : 0f.ToString("F2");

            // Auto-advance veya kullanıcı girişini bekle
            if (autoAdvanceDelay > 0f)
                Invoke(nameof(ProceedToResult), autoAdvanceDelay);
        }
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
        
        if (completedScoreText) completedScoreText.transform.DOKill();
        if (completedCoinText) completedCoinText.transform.DOKill();
        if (completedX2CoinText) completedX2CoinText.transform.DOKill();

        if (failedScoreText) failedScoreText.transform.DOKill();
        if (failedCoinText) failedCoinText.transform.DOKill();

        CancelInvoke();
    }

    // --- CHAPTER LOGIC ---

    private void AnimateChapterCompletion(bool isSuccess)
    {
         if (!_hasTargets) return;

        if (isSuccess)
        {
            // --- SUCCESS ---
            // Score
            if (completedScoreText)
            {
                completedScoreText.transform.DOKill();
                completedScoreText.text = scoreAsInteger ? "0" : 0f.ToString("F2");
                DOVirtual.Float(0f, _targetScore, countUpDuration, v =>
                {
                   completedScoreText.text = scoreAsInteger ? Mathf.FloorToInt(v).ToString() : v.ToString("F2");
                });
            }

            // Coins
            if (completedCoinText)
            {
                completedCoinText.transform.DOKill();
                completedCoinText.text = coinAsInteger ? "0" : 0f.ToString("F2");
                DOVirtual.Float(0f, _targetCoins, countUpDuration, v =>
                {
                   completedCoinText.text = coinAsInteger ? Mathf.FloorToInt(v).ToString() : v.ToString("F2");
                });
            }

            // Coins X2
            if (completedX2CoinText)
            {
                float targetX2 = _targetCoins * 2f;
                completedX2CoinText.transform.DOKill();
                completedX2CoinText.text = coinAsInteger ? "0" : 0f.ToString("F2");
                DOVirtual.Float(0f, targetX2, countUpDuration, v =>
                {
                   completedX2CoinText.text = coinAsInteger ? Mathf.FloorToInt(v).ToString() : v.ToString("F2");
                });
            }
        }
        else
        {
            // --- FAIL ---
             // Failed Score
            if (failedScoreText)
            {
                failedScoreText.transform.DOKill();
                failedScoreText.text = scoreAsInteger ? "0" : 0f.ToString("F2");
                DOVirtual.Float(0f, _targetScore, countUpDuration, v =>
                {
                   failedScoreText.text = scoreAsInteger ? Mathf.FloorToInt(v).ToString() : v.ToString("F2");
                });
            }

            // Failed Coins
            if (failedCoinText)
            {
                failedCoinText.transform.DOKill();
                failedCoinText.text = coinAsInteger ? "0" : 0f.ToString("F2");
                DOVirtual.Float(0f, _targetCoins, countUpDuration, v =>
                {
                   failedCoinText.text = coinAsInteger ? Mathf.FloorToInt(v).ToString() : v.ToString("F2");
                });
            }
        }
    }

    public void OnChapterFailClaimButton()
    {
        // Fail durumunda sadece normal parayı al ve çık
        CompleteAndClose();
    }

    public void OnClaimX2Button()
    {
         if (!_isRunning) return;
         if (claimX2Button == null) return;

         var adProduct = claimX2Button.GetComponent<UIAdProduct>();
         if (adProduct == null)
         {
             Debug.LogError("[UILevelEnd] claimX2Button MUST have a UIAdProduct component!");
             return;
         }
         
         // Disable button (UIAdProduct handles basic interactable state but we can be safe)
         var btn = claimX2Button.GetComponent<UnityEngine.UI.Button>();
         if (btn) btn.interactable = false;

         // Trigger Ad via UIAdProduct (handles limits/recording)
         adProduct.TriggerAdFromExternal(() => 
         {
             // SUCCESS CALLBACK
             // Add 1x more (total 2x)
             GameplayManager.Instance.AddCoins(_targetCoins); 
                 
             // Close
             CompleteAndClose();
         });
    }
}