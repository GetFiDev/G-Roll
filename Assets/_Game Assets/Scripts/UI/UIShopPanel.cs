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
    [SerializeField] private GameObject myItemsActive, myItemsInactive;
    [SerializeField] private GameObject myItemsPanel;

    [Header("Slide View")]
    [SerializeField] private ScrollRect scrollRect;          // Inspector'dan atayacaksın
    [SerializeField] private int panelCount = 5;             // Toplam yatay panel sayısı
    [SerializeField] private float slideDuration = 0.35f;    // Kayma süresi (sn)
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine slideRoutine;

    private void Start()
    {
        // Orijinal akış: My Items sekmesini aktif et
        OnMyItemsButtonClicked();
        // Opsiyonel: Scroll konumunu da ilk panele (My Items) hizala
        if (scrollRect != null) JumpTo(0);
    }

    public void OnReferralButtonClicked()
    {
        CloseAll();

        // Tab visuals
        referralActive.SetActive(true);
        referralInactive.SetActive(false);

        // Panel görünürlükleri
        ApplyPanelVisibility(1);

        // Slide
        SlideTo(1);
    }

    public void OnCoreButtonClicked()
    {
        CloseAll();

        coreActive.SetActive(true);
        coreInactive.SetActive(false);

        ApplyPanelVisibility(2);
        SlideTo(2);
    }

    public void OnProButtonClicked()
    {
        CloseAll();

        proActive.SetActive(true);
        proInactive.SetActive(false);

        ApplyPanelVisibility(3);
        SlideTo(3);
    }

    public void OnBullButtonClicked()
    {
        CloseAll();

        bullActive.SetActive(true);
        bullInactive.SetActive(false);

        ApplyPanelVisibility(4);
        SlideTo(4);
    }

    private void CloseAll()
    {
        // Panelleri SetActive(false) yapmıyoruz; layout sabit kalsın diye hepsi aktif kalsın.
        // Sadece tab görsellerini resetliyoruz.
        referralActive.SetActive(false);
        coreActive.SetActive(false);
        proActive.SetActive(false);
        bullActive.SetActive(false);
        myItemsActive.SetActive(false);

        referralInactive.SetActive(true);
        coreInactive.SetActive(true);
        proInactive.SetActive(true);
        bullInactive.SetActive(true);
        myItemsInactive.SetActive(true);
    }
    public void OnMyItemsButtonClicked()
    {
        CloseAll();

        myItemsActive.SetActive(true);
        myItemsInactive.SetActive(false);

        ApplyPanelVisibility(0);
        SlideTo(0);
    }

    // --- Slide Yardımcıları ---
    private void JumpTo(int index)
    {
        if (scrollRect == null) return;
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

    /// <summary>
    /// Left padding + spacing + panel genişliği hesaba katılarak
    /// istenen paneli viewport'a ortalayacak normalized X konumunu hesaplar.
    /// </summary>
    private float TargetOf(int index)
    {
        if (scrollRect == null || scrollRect.content == null)
            return 0f;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : (RectTransform)scrollRect.transform;

        float contentWidth = content.rect.width;
        float viewportWidth = viewport.rect.width;

        // Scroll edilebilir alan yoksa kaydırma yapma
        if (contentWidth <= viewportWidth || panelCount <= 1)
            return 0f;

        // Layout bilgilerini al (padding + spacing)
        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        float paddingLeft = layout != null ? layout.padding.left : 0f;
        float spacing = layout != null ? layout.spacing : 0f;

        // Panel genişliğini, index'e karşılık gelen gerçek panel child'ından al
        float panelWidth = 0f;
        if (content.childCount > index)
        {
            RectTransform child = content.GetChild(index) as RectTransform;
            if (child != null)
                panelWidth = child.rect.width;
        }

        // Panel genişliği okunamazsa eski fallback davranışına dön
        index = Mathf.Clamp(index, 0, panelCount - 1);
        if (panelWidth <= 0.0001f)
        {
            return index / Mathf.Max(1f, (panelCount - 1f));
        }

        // Her ekranda aynı left margin görünmesi için offset hesabında paddingLeft'i
        // sadece ilk panel için kullanmış gibi davranıyoruz:
        // Panel i'nin viewport solundan uzaklığı hep paddingLeft olsun istiyorsak,
        // x = i * (panelWidth + spacing) yeterli.
        float offset = index * (panelWidth + spacing);

        // Toplam scroll mesafesi: contentWidth - viewportWidth
        float maxScroll = Mathf.Max(0.0001f, contentWidth - viewportWidth);

        // ScrollRect.normalizedPosition.x: 0 = en sol, 1 = en sağ
        float normalized = Mathf.Clamp01(offset / maxScroll);
        return normalized;
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

    /// <summary>
    /// Tüm paneller aktif kalsın; eğer CanvasGroup varsa sadece alpha / interactable ile
    /// görünür/görünmez yap. CanvasGroup yoksa hiçbir şey yapma (layout bozulmasın).
    /// </summary>
    private void ApplyPanelVisibility(int index)
    {
        // Horizontal Layout + spacing sabit kalsın diye hepsi aktif
        referralPanel.SetActive(true);
        corePanel.SetActive(true);
        proPanel.SetActive(true);
        bullPanel.SetActive(true);
        myItemsPanel.SetActive(true);

        void SetCG(GameObject go, bool visible)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) return;
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        SetCG(myItemsPanel,  index == 0);
        SetCG(referralPanel, index == 1);
        SetCG(corePanel,     index == 2);
        SetCG(proPanel,      index == 3);
        SetCG(bullPanel,     index == 4);
    }
}