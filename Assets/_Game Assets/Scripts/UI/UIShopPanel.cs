using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIShopPanel : MonoBehaviour
{
    [Header("Existing Panels & Tab Visuals")]
    [SerializeField] private GameObject referralActive, referralInactive;
    [SerializeField] private GameObject referralPanel;

    [SerializeField] private GameObject coreActive, coreInactive;
    [SerializeField] private GameObject corePanel;

    [SerializeField] private GameObject proActive, proInactive;
    [SerializeField] private GameObject proPanel;

    [SerializeField] private GameObject bullActive, bullInactive;
    [SerializeField] private GameObject bullPanel;

    [Header("Slide View")]
    [SerializeField] private ScrollRect scrollRect;          // Inspector'dan atayacaksın
    [SerializeField] private int panelCount = 4;             // Toplam yatay panel sayısı
    [SerializeField] private float slideDuration = 0.35f;    // Kayma süresi (sn)
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine slideRoutine;

    private void Start()
    {
        // Orijinal akış: önce aç/kapat görselleri ile Referral'ı aktif et
        OnReferralButtonClicked();
        // Opsiyonel: Scroll konumunu da ilk panele hizala
        if (scrollRect != null) JumpTo(0);
    }

    public void OnReferralButtonClicked()
    {
        CloseAll();

        // Tab visuals (kept as before)
        referralActive.SetActive(true);
        referralInactive.SetActive(false);

        // Panels: do NOT SetActive here; keep layout intact
        ApplyPanelVisibility(0);

        // Slide
        SlideTo(0);
    }

    public void OnCoreButtonClicked()
    {
        CloseAll();

        coreActive.SetActive(true);
        coreInactive.SetActive(false);

        ApplyPanelVisibility(1);
        SlideTo(1);
    }

    public void OnProButtonClicked()
    {
        CloseAll();

        proActive.SetActive(true);
        proInactive.SetActive(false);

        ApplyPanelVisibility(2);
        SlideTo(2);
    }

    public void OnBullButtonClicked()
    {
        CloseAll();

        bullActive.SetActive(true);
        bullInactive.SetActive(false);

        ApplyPanelVisibility(3);
        SlideTo(3);
    }

    private void CloseAll()
    {
        // Do NOT SetActive on panels here — keeping them active preserves layout/content width.
        // Only reset tab visuals.
        referralActive.SetActive(false);
        coreActive.SetActive(false);
        proActive.SetActive(false);
        bullActive.SetActive(false);

        referralInactive.SetActive(true);
        coreInactive.SetActive(true);
        proInactive.SetActive(true);
        bullInactive.SetActive(true);
    }

    // --- Slide Yardımcıları ---
    private void JumpTo(int index)
    {
        float target = TargetOf(index);
        scrollRect.normalizedPosition = new Vector2(target, 0f);
    }

    private void SlideTo(int index)
    {
        if (scrollRect == null) return;
        float target = TargetOf(index);
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SmoothSlideTo(target));
    }

    private float TargetOf(int index)
    {
        if (panelCount <= 1) return 0f;
        index = Mathf.Clamp(index, 0, panelCount - 1);
        // Viewport genişliği = Panel genişliği olduğu varsayımıyla spacing olsa bile doğru hizalar
        return index / (panelCount - 1f);
    }

    private IEnumerator SmoothSlideTo(float target)
    {
        Vector2 start = scrollRect.normalizedPosition;
        Vector2 goal = new Vector2(target, 0f);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, slideDuration);
            float a = ease.Evaluate(Mathf.Clamp01(t));
            scrollRect.normalizedPosition = Vector2.LerpUnclamped(start, goal, a);
            yield return null;
        }

        scrollRect.normalizedPosition = goal;
        slideRoutine = null;
    }
    /// Keep all panels active for layout; if a CanvasGroup exists, fade/hide interaction for non-selected.
    /// If no CanvasGroup is attached, this becomes a no-op to avoid layout jumps.
    /// </summary>
    private void ApplyPanelVisibility(int index)
    {
        // Ensure all panels are active in hierarchy so Horizontal Layout + spacing remains stable
        referralPanel.SetActive(true);
        corePanel.SetActive(true);
        proPanel.SetActive(true);
        bullPanel.SetActive(true);

        // Try CanvasGroup approach (optional)
        void SetCG(GameObject go, bool visible)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) return; // no-op if not present
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        SetCG(referralPanel, index == 0);
        SetCG(corePanel,     index == 1);
        SetCG(proPanel,      index == 2);
        SetCG(bullPanel,     index == 3);
    }
}