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

    [Header("Revive Panel (Endless Only)")]
    [SerializeField] private GameObject revivePanel;
    [SerializeField] private UnityEngine.UI.Button skipReviveButton;
    [SerializeField] private UnityEngine.UI.Button currencyReviveButton;
    [SerializeField] private TextMeshProUGUI currencyRevivePriceText;
    [SerializeField] private UnityEngine.UI.Button adReviveButton;
    [SerializeField] private UIAdProduct adReviveProduct;
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color notAffordableColor = Color.red;

    // Revive state
    private int _reviveCount = 0;
    private const float BASE_REVIVE_PRICE = 0.5f;

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
            // Show session failed panel with revive options
            PreparePanel(sessionFailedPanel, sessionFailedGroup, visible:true);
            
            // Show revive panel within session failed
            ShowRevivePanel();
            
            // Result sayacı başlangıç görünümü (0'dan başlasın)
            if (resultScoreText) resultScoreText.text = scoreAsInteger ? "0" : 0f.ToString("F2");
            if (resultCoinText)  resultCoinText.text  = coinAsInteger  ? "0" : 0f.ToString("F2");
            if (resultCoinDoubleText) resultCoinDoubleText.text = coinAsInteger ? "0" : 0f.ToString("F2");

            // NO auto-advance in revive mode - user must choose
            // (Revive panel buttons handle navigation)
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

    /// <summary>
    /// FIX #2: Chapter success sonrası continue butonu - loading screen ile geçiş
    /// </summary>
    public void OnChapterContinueButton()
    {
        if (!_isRunning) return;
        _isRunning = false;
        
        // 1. Panel'i kapat
        gameObject.SetActive(false);
        
        // 2. Loading screen göster
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameplayLoading(true);
        }
        
        // 3. Kısa delay sonra sequence'ı tamamla (loading hissiyatı için)  
        _ = DelayedCompleteSequence();
    }

    private async System.Threading.Tasks.Task DelayedCompleteSequence()
    {
        await System.Threading.Tasks.Task.Delay(1500); // 1.5 saniye loading
        OnSequenceCompleted?.Invoke();
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

         // Trigger Ad via UIAdProduct (handles limits/recording) with failure callback
         adProduct.TriggerAdFromExternal(
             // SUCCESS CALLBACK
             () => 
             {
                 // Add 1x more (total 2x)
                 GameplayManager.Instance.AddCoins(_targetCoins); 
                     
                 // FIX #2: Chapter modu için farklı kapanış - loading screen ile
                 GameMode mode = GameplayManager.Instance?.CurrentMode ?? GameMode.Endless;
                 if (mode == GameMode.Chapter)
                 {
                     OnChapterContinueButton();
                 }
                 else
                 {
                     CompleteAndClose();
                 }
             },
             // FAILURE CALLBACK - FIX: Re-enable button so user can try again
             () =>
             {
                 Debug.LogWarning("[UILevelEnd] ClaimX2 ad failed or skipped.");
                 if (btn) btn.interactable = true;
             }
         );
    }

    // ---- REVIVE SYSTEM ----

    /// <summary>
    /// Reset revive state for a new session.
    /// Call this when a new endless run starts.
    /// </summary>
    public void ResetReviveState()
    {
        _reviveCount = 0;
        UpdateReviveUI();
    }

    /// <summary>
    /// Current price for revive (doubles each time).
    /// </summary>
    public float GetCurrentRevivePrice()
    {
        return BASE_REVIVE_PRICE * Mathf.Pow(2f, _reviveCount);
    }

    /// <summary>
    /// Update revive UI state (price, button interactability).
    /// </summary>
    private void UpdateReviveUI()
    {
        float price = GetCurrentRevivePrice();
        bool canAfford = false;

        if (UserDatabaseManager.Instance != null && UserDatabaseManager.Instance.currentUserData != null)
        {
            canAfford = UserDatabaseManager.Instance.currentUserData.currency >= price;
        }

        // Update price text
        if (currencyRevivePriceText != null)
        {
            currencyRevivePriceText.text = price.ToString("F1");
            currencyRevivePriceText.color = canAfford ? affordableColor : notAffordableColor;
        }

        // Update currency button interactability
        if (currencyReviveButton != null)
        {
            currencyReviveButton.interactable = canAfford;
        }

        // Ad revive button is handled by UIAdProduct (checks daily limit)
    }

    /// <summary>
    /// Show the revive panel with updated UI.
    /// Called when endless mode fails.
    /// </summary>
    private void ShowRevivePanel()
    {
        if (revivePanel != null)
        {
            revivePanel.SetActive(true);
            UpdateReviveUI();
        }
    }

    // --- Revive Button Handlers (assign in Inspector) ---

    /// <summary>
    /// Skip revive and proceed to result screen.
    /// </summary>
    public void OnSkipReviveClicked()
    {
        Debug.Log("[UILevelEnd] Skip revive clicked.");
        
        // Hide revive panel
        if (revivePanel != null) revivePanel.SetActive(false);
        
        // Proceed to result
        ProceedToResult();
    }

    /// <summary>
    /// Attempt currency-based revive via Cloud Function.
    /// </summary>
    public async void OnCurrencyReviveClicked()
    {
        float price = GetCurrentRevivePrice();
        
        if (UserDatabaseManager.Instance == null || UserDatabaseManager.Instance.currentUserData == null)
        {
            Debug.LogError("[UILevelEnd] Cannot revive - no user data.");
            return;
        }

        // Quick client check (server will verify anyway)
        if (UserDatabaseManager.Instance.currentUserData.currency < price)
        {
            Debug.LogWarning("[UILevelEnd] Cannot afford revive.");
            return;
        }

        Debug.Log($"[UILevelEnd] Currency revive clicked. Price: {price}");

        // Disable button while processing
        if (currencyReviveButton != null) currencyReviveButton.interactable = false;

        // Call Cloud Function for secure currency deduction
        try
        {
            string sessionId = GameplayManager.Instance?.CurrentSessionId ?? "";
            var result = await SpendCurrencyForReviveAsync(sessionId, _reviveCount);
            
            if (result.ok)
            {
                // Update local cache
                if (UserDatabaseManager.Instance.currentUserData != null)
                {
                    UserDatabaseManager.Instance.currentUserData.currency = (float)result.newCurrency;
                }
                
                _reviveCount++;
                
                // Hide revive panel and trigger revive
                if (revivePanel != null) revivePanel.SetActive(false);
                
                // Execute revive
                if (ReviveController.Instance != null)
                {
                    ReviveController.Instance.ExecuteReviveLogic();
                }
                else
                {
                    Debug.LogError("[UILevelEnd] ReviveController missing! Reviving without clearing hazards.");
                    GameplayManager.Instance?.ResumeAfterRevive();
                }
                _isRunning = false;
                gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[UILevelEnd] Revive failed: {result.reason}");
                // Re-enable button
                if (currencyReviveButton != null) currencyReviveButton.interactable = true;
                UpdateReviveUI(); // Refresh UI
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UILevelEnd] Currency revive error: {ex.Message}");
            if (currencyReviveButton != null) currencyReviveButton.interactable = true;
        }
    }

    private struct ReviveResult
    {
        public bool ok;
        public string reason;
        public double spent;
        public double newCurrency;
        public int reviveIndex;
    }

    private async System.Threading.Tasks.Task<ReviveResult> SpendCurrencyForReviveAsync(string sessionId, int reviveIndex)
    {
        var functions = Firebase.Functions.FirebaseFunctions.GetInstance("us-central1");
        var callable = functions.GetHttpsCallable("spendCurrencyForRevive");
        
        var data = new System.Collections.Generic.Dictionary<string, object>
        {
            { "sessionId", sessionId },
            { "reviveIndex", reviveIndex }
        };
        
        var res = await callable.CallAsync(data);
        
        if (res.Data is System.Collections.IDictionary dict)
        {
            return new ReviveResult
            {
                ok = dict.Contains("ok") && (bool)dict["ok"],
                reason = dict.Contains("reason") ? dict["reason"]?.ToString() : null,
                spent = dict.Contains("spent") ? System.Convert.ToDouble(dict["spent"]) : 0,
                newCurrency = dict.Contains("newCurrency") ? System.Convert.ToDouble(dict["newCurrency"]) : 0,
                reviveIndex = dict.Contains("reviveIndex") ? System.Convert.ToInt32(dict["reviveIndex"]) : 0
            };
        }
        
        return new ReviveResult { ok = false, reason = "parse_error" };
    }

    /// <summary>
    /// Attempt ad-based revive.
    /// </summary>
    public void OnAdReviveClicked()
    {
        Debug.Log("[UILevelEnd] Ad revive clicked.");

        if (adReviveProduct == null)
        {
            Debug.LogError("[UILevelEnd] adReviveProduct is not assigned!");
            return;
        }

        // Disable button while ad is loading
        if (adReviveButton != null) adReviveButton.interactable = false;

        // Trigger ad via UIAdProduct with success AND failure callbacks
        adReviveProduct.TriggerAdFromExternal(
            // Success callback
            () =>
            {
                // Ad watched successfully
                Debug.Log("[UILevelEnd] Ad revive success!");
                
                _reviveCount++;

                // Hide revive panel and trigger revive
                if (revivePanel != null) revivePanel.SetActive(false);
                
                // Execute revive
                if (ReviveController.Instance != null)
                {
                    ReviveController.Instance.ExecuteAdRevive(() => {
                        _isRunning = false;
                        gameObject.SetActive(false);
                    });
                }
                else
                {
                    GameplayManager.Instance?.ResumeAfterRevive();
                    _isRunning = false;
                    gameObject.SetActive(false);
                }
            },
            // Failure callback - FIX: Re-enable button so user can try again
            () =>
            {
                Debug.LogWarning("[UILevelEnd] Ad revive failed or skipped.");
                if (adReviveButton != null) adReviveButton.interactable = true;
            }
        );
    }
}