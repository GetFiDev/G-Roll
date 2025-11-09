using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using TMPro;

// Eğer projende TextMeshPro kullanıyorsan üst satırı aç:
// using TMPro;

[DisallowMultipleComponent]
public class TooltipHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [TextArea]
    public string tooltipText = "Tooltip message";

    [Header("Layout")]
    [Tooltip("Alt kenardan boşluk (px)")]
    public float bottomPadding = 40f;
    [Tooltip("Maks genişlik (px)")]
    public float maxWidth = 900f;
    [Tooltip("Yazı boyutu")]
    public int fontSize = 28;

    [Header("Style (opsiyonel)")]
    public Color backgroundColor = new Color(0, 0, 0, 0.65f);
    public Color textColor = Color.white;
    public Vector4 backgroundPadding = new Vector4(24, 12, 24, 12); // left, top, right, bottom
    public float cornerRadius = 16f; // sadece Image + 9-slice kullanılacaksa

    private static TooltipRuntime _runtime;   // Paylaşımlı tek UI
    private bool _pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        EnsureRuntime();
        _runtime.Show(tooltipText, this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        if (_runtime != null) _runtime.Hide(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Parmağı butondan dışarı kaydırınca gizle
        if (_pressed)
        {
            _pressed = false;
            if (_runtime != null) _runtime.Hide(this);
        }
    }

    private void OnDisable()
    {
        // Buton disable olursa veya sahne kapanırsa gizle
        if (_runtime != null) _runtime.Hide(this);
        _pressed = false;
    }

    private void EnsureRuntime()
    {
        if (_runtime == null || !_runtime.IsAlive())
        {
            _runtime = TooltipRuntime.Create(backgroundColor, textColor);
        }

        // Her buton kendi stil ayarlarını runtime’a aktarabilir (isteğe göre tek seferlik)
        _runtime.ConfigureLayout(bottomPadding, maxWidth, fontSize, backgroundPadding, cornerRadius);
    }

    // ------------------------
    // İç sınıf: Prefabsiz tooltip UI
    // ------------------------
    private class TooltipRuntime
    {
        private readonly GameObject _canvasGO;
        private readonly GameObject _root;
        private readonly RectTransform _panel;
        private readonly Image _bg;

        private readonly TMP_Text _tmp;

        private TooltipHandler _owner; // Hangi buton gösteriyorken aktif

        public static TooltipRuntime Create(Color bgColor, Color textColor)
        {
            // Canvas
            var canvasGO = new GameObject("RuntimeTooltipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1; // her şeyin üstünde

            Object.DontDestroyOnLoad(canvasGO);

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // Root (block raycast OFF)
            var root = new GameObject("TooltipRoot", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvasGO.transform, false);
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0.5f, 0);
            rootRT.anchorMax = new Vector2(0.5f, 0);
            rootRT.pivot = new Vector2(0.5f, 0);
            rootRT.anchoredPosition = Vector2.zero;

            var cg = root.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // dokunmaları engellemesin
            root.SetActive(false);

            // Panel (background)
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0);
            panelRT.anchorMax = new Vector2(0.5f, 0);
            panelRT.pivot = new Vector2(0.5f, 0);

            var bg = panel.GetComponent<Image>();
            bg.color = bgColor;
            // Eğer 9-slice bir sprite atarsan köşe yarıçapını görselden alır

            // Text (TextMeshPro)
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(panel.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0);
            textRT.anchorMax = new Vector2(1, 1);
            textRT.pivot = new Vector2(0.5f, 0.5f);

            var tmp = textGO.GetComponent<TextMeshProUGUI>();
            // Project default TMP font (falls back to component default if null)
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }
            tmp.enableWordWrapping = true;
            tmp.alignment = TextAlignmentOptions.MidlineGeoAligned;
            tmp.color = textColor;
            tmp.raycastTarget = false;

            return new TooltipRuntime(canvasGO, root, panelRT, bg, tmp);
        }

        private TooltipRuntime(GameObject canvasGO, GameObject root, RectTransform panel, Image bg, TMP_Text tmp)
        {
            _canvasGO = canvasGO;
            _root = root;
            _panel = panel;
            _bg = bg;
            _tmp = tmp;
        }

        public bool IsAlive()
        {
            return _canvasGO != null && _root != null && _panel != null && _bg != null && _tmp != null;
        }

        public void ConfigureLayout(float bottomPadding, float maxWidth, int fontSize, Vector4 bgPadding, float cornerRadius)
        {
            if (!IsAlive()) return;

            var rootRT = _root.GetComponent<RectTransform>();
            rootRT.anchoredPosition = new Vector2(0, bottomPadding);

            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth);

            // Padding için paneli büyütüp text’e inset veriyoruz
            var textRT = _tmp.rectTransform;
            textRT.offsetMin = new Vector2(bgPadding.x, bgPadding.y);             // left, bottom
            textRT.offsetMax = new Vector2(-bgPadding.z, -bgPadding.w);           // -right, -top

            _tmp.enableAutoSizing = false;
            _tmp.fontSize = fontSize;

            // Köşe yarıçapı: Image’da 9-slice sprite yoksa görsel olarak uygulanamaz; sprite atarsan çalışır
            // (Bu alan sadece not, runtime’da corner radius’u direkt veremeyiz.)
        }

        public void Show(string message, TooltipHandler owner)
        {
            if (!IsAlive()) return;
            _owner = owner;

            _tmp.text = message;

            // Height’i içeriğe göre ayarlamak için küçük bir gecikme iyi olur
            _root.SetActive(true);
            owner.StartCoroutine(AdjustHeightNextFrame());
        }

        public void Hide(TooltipHandler owner)
        {
            if (!IsAlive()) return;
            if (_owner != owner) return; // başka bir buton gösteriyorsa dokunma
            _root.SetActive(false);
            _owner = null;
        }

        private IEnumerator AdjustHeightNextFrame()
        {
            yield return null; // layout’un güncellenmesi için bir frame bekle
            if (!IsAlive()) yield break;
            // İçeriğe göre yükseklik:
            // Content Size Fitter yok; preferredHeight’i alıp paneli büyütüyoruz
            float pref = _tmp.preferredHeight;
            float minHeight = Mathf.Max(72f, pref + 0f); // güvenlik payı
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minHeight);
        }
    }
}