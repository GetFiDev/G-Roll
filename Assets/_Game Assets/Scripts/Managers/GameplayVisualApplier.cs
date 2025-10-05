using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using AssetKits.ParticleImage;

/// Görsel: UI metinleri, slider, DOTween, coin UI partikül efektleri
public class GameplayVisualApplier : MonoBehaviour
{
    [Header("Text UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private bool scoreAsInteger = true;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private bool coinAsInteger = true;

    [Header("Combo UI")]
    [SerializeField] private TextMeshProUGUI comboText; // shows like: x1.00
    [SerializeField] private float comboDecreaseDuration = 0.5f;  // reset/sudden drop smoothing
    [SerializeField] private float comboIncreaseDuration = 0.12f; // quick rise
    [SerializeField] private float comboBounceScale = 1.08f;      // scale punch on change

    [Header("Booster UI")]
    [SerializeField] private Slider boosterSlider;

    [Header("Coin UI FX")]
    [SerializeField] private ParticleImage coinPickupParticle;     // UI particle prefab/instance
    [SerializeField] private Vector2 coinFXScreenOffset = Vector2.zero;
    [SerializeField] private Camera uiCameraOverride;
    [SerializeField] private Camera worldCameraForCoinFX;
    [SerializeField] private bool autoPlayUIParticle = true;
    [SerializeField] private bool coinFXUseInstancePerBurst = true;
    [SerializeField] private float coinFXDespawnSeconds = 1.5f;

    [SerializeField] private Transform playerTransformForCoinFX; // Fallback anchor (eski davranış)
    private int lastFXFrame = -1; // aynı frame'de iki kez spawn'ı önlemek için

    [Header("Coin Text Bounce")]
    [SerializeField] private UnityEngine.Events.UnityEvent onCoinPickupFX;

    private Vector3 coinTextBaseScale = Vector3.one;
    private GameplayLogicApplier logic;

    private float displayedCombo = 1f;
    private Color comboBaseColor = Color.white;
    private Tween comboTween;
    private Tween comboColorTween;

    // ---- Public binding API ----
    public void Bind(GameplayLogicApplier logicApplier)
    {
        Unbind();

        logic = logicApplier;
        if (logic == null) return;

        logic.OnRunStarted += HandleRunStarted;
        logic.OnRunStopped += HandleRunStopped;
        logic.OnScoreChanged += HandleScoreChanged;
        logic.OnCoinsChanged += HandleCoinsChanged;
        logic.OnBoosterChanged += HandleBoosterChanged;
        logic.OnCoinPickupFXRequest += HandleCoinFXRequest;
        logic.OnComboMultiplierChanged += HandleComboChanged;
        logic.OnComboReset += HandleComboReset;
    }

    public void Unbind()
    {
        if (logic == null) return;
        ClearCoinFXAnchor();

        logic.OnRunStarted -= HandleRunStarted;
        logic.OnRunStopped -= HandleRunStopped;
        logic.OnScoreChanged -= HandleScoreChanged;
        logic.OnCoinsChanged -= HandleCoinsChanged;
        logic.OnBoosterChanged -= HandleBoosterChanged;
        logic.OnCoinPickupFXRequest -= HandleCoinFXRequest;
        logic.OnComboMultiplierChanged -= HandleComboChanged;
        logic.OnComboReset -= HandleComboReset;

        comboTween?.Kill();
        comboColorTween?.Kill();

        logic = null;
    }

    // ---- Handlers ----
    private void HandleRunStarted()
    {
        // UI reset
        if (scoreText) scoreText.text = scoreAsInteger ? "0" : "0.00";
        if (coinText)
        {
            coinTextBaseScale = coinText.transform.localScale;
            coinText.text = coinAsInteger ? "0" : "0.00";
        }
        if (boosterSlider) boosterSlider.value = 0f;
        if (comboText)
        {
            comboBaseColor = comboText.color;
            displayedCombo = 1f;
            comboText.text = "x1.00";
            comboText.transform.localScale = Vector3.one;
        }
    }

    private void HandleRunStopped() { /* görsel olarak özel bir şey gerekmez */ }

    private void HandleScoreChanged(float score)
    {
        if (!scoreText) return;
        scoreText.text = scoreAsInteger ? Mathf.FloorToInt(score).ToString() : score.ToString("F2");
    }

    private void HandleCoinsChanged(float total, float delta)
    {
        if (coinText)
        {
            coinText.text = coinAsInteger ? Mathf.FloorToInt(total).ToString() : total.ToString("F2");

            // bounce
            var t = coinText.transform;
            t.DOKill();
            if (coinTextBaseScale == Vector3.one) coinTextBaseScale = t.localScale;
            t.localScale = coinTextBaseScale;
            t.DOScale(coinTextBaseScale * 1.15f, 0.08f).SetEase(Ease.OutQuad)
             .OnComplete(() => t.DOScale(coinTextBaseScale, 0.08f).SetEase(Ease.InQuad));
        }

        onCoinPickupFX?.Invoke();

        // Eğer mantık katmanı worldPos ile ayrı bir FX olayı yollamadıysa, oyuncu üstünde tek burst yap
        if (playerTransformForCoinFX != null && lastFXFrame != Time.frameCount)
        {
            EmitCoinFXAtWorld(playerTransformForCoinFX.position, 1);
            lastFXFrame = Time.frameCount;
        }
    }

    private void HandleBoosterChanged(float fill, float min, float max)
    {
        if (!boosterSlider) return;
        float denom = Mathf.Max(0.0001f, max - min);
        boosterSlider.value = (fill - min) / denom;
    }

    private void HandleCoinFXRequest(Vector3 worldPos, int count)
    {
        EmitCoinFXAtWorld(worldPos, Mathf.Max(1, count));
        lastFXFrame = Time.frameCount;
    }

    // --- The old EmitCoinFX logic moved here ---
    private void EmitCoinFXAtWorld(Vector3 worldPos, int count)
    {
        if (coinPickupParticle == null) { onCoinPickupFX?.Invoke(); return; }
        var canvas = coinPickupParticle.canvas;
        if (canvas == null) { onCoinPickupFX?.Invoke(); return; }

        Camera worldCam = worldCameraForCoinFX != null ? worldCameraForCoinFX
                          : (uiCameraOverride != null ? uiCameraOverride
                                                     : Camera.main);
        if (worldCam == null) { onCoinPickupFX?.Invoke(); return; }

        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) { onCoinPickupFX?.Invoke(); return; }

        Vector3 screen = RectTransformUtility.WorldToScreenPoint(worldCam, worldPos);
        if (screen.z < 0f) { onCoinPickupFX?.Invoke(); return; }

        Camera uiCam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            uiCam = uiCameraOverride != null ? uiCameraOverride : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out var local))
        {
            onCoinPickupFX?.Invoke();
            return;
        }

        int burstCount = 1;

        if (coinFXUseInstancePerBurst)
        {
            var inst = Instantiate(coinPickupParticle, canvas.transform);
            var rt = inst.rectTransform;

            if (canvas.renderMode == RenderMode.WorldSpace)
                rt.position = canvasRect.TransformPoint(local) + (Vector3)coinFXScreenOffset;
            else
                rt.anchoredPosition = local + coinFXScreenOffset;

            inst.EmitBurstNow(burstCount, autoPlayUIParticle);
            Destroy(inst.gameObject, Mathf.Max(0.05f, coinFXDespawnSeconds));
        }
        else
        {
            var rt = coinPickupParticle.rectTransform;

            if (canvas.renderMode == RenderMode.WorldSpace)
                rt.position = canvasRect.TransformPoint(local) + (Vector3)coinFXScreenOffset;
            else
                rt.anchoredPosition = local + coinFXScreenOffset;

            coinPickupParticle.EmitBurstNow(burstCount, autoPlayUIParticle);
        }

        onCoinPickupFX?.Invoke();
    }

    public void SetCoinFXAnchor(Transform anchor)
    {
        playerTransformForCoinFX = anchor;
    }

    public void ClearCoinFXAnchor()
    {
        playerTransformForCoinFX = null;
    }

    private void HandleComboChanged(float newCombo)
    {
        if (!comboText) return;
        float dur = newCombo < displayedCombo ? comboDecreaseDuration : comboIncreaseDuration;

        comboTween?.Kill();
        float start = displayedCombo;
        comboTween = DOTween.To(() => start, v => {
            start = v;
            displayedCombo = v;
            comboText.text = $"x{v:0.00}";
        }, newCombo, Mathf.Max(0.01f, dur));

        // bounce
        var t = comboText.transform;
        t.DOKill();
        t.localScale = Vector3.one;
        t.DOScale(Vector3.one * comboBounceScale, dur * 0.5f).SetEase(Ease.OutQuad)
         .OnComplete(() => t.DOScale(Vector3.one, dur * 0.5f).SetEase(Ease.InQuad));
    }

    private void HandleComboReset()
    {
        if (!comboText) return;

        // Smooth to 1.00
        HandleComboChanged(1f);

        // flash red briefly then back to base color
        comboColorTween?.Kill();
        comboText.color = comboBaseColor;
        comboColorTween = comboText.DOColor(Color.red, comboDecreaseDuration * 0.4f)
            .OnComplete(() => comboText.DOColor(comboBaseColor, comboDecreaseDuration * 0.6f));
    }
}