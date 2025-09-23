using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GroundColorPaletteUI : MonoBehaviour
{
    public GroundMaterialSwitcher switcher;
    public Transform container;     // Horizontal/Vertical Layout olan bir RectTransform
    public Button buttonTemplate;   // inactive template (içinde Image olabilir)

    [Header("Sampling Settings")]
    [Tooltip("Readable Texture2D'de örneklenecek maksimum grid boyutu (NxN). Daha küçük veya okunamazsa otomatik düşer.")]
    public int readableSampleGrid = 32;
    [Tooltip("RenderTexture fallback çözünürlüğü (örnekleme/ortalama için)")]
    public int fallbackRTSize = 32;

    void Start()
    {
        foreach (Transform c in container) Destroy(c.gameObject);
        buttonTemplate.gameObject.SetActive(false);

        for (int i = 0; i < switcher.presets.Length; i++)
        {
            int idx = i;
            var btn = Instantiate(buttonTemplate, container);
            btn.gameObject.SetActive(true);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            var img = btn.GetComponentsInChildren<Image>(includeInactive: true)
                         .FirstOrDefault(img => img.gameObject != btn.gameObject);

            if (img && switcher.presets[i])
            {
                if (TryGetAverageTextureColor(switcher.presets[i], out var avg))
                {
                    img.color = avg;
                    if (txt) txt.text = ""; // debug yazısını boş bırak
                }
                else
                {
                    // Fallback: _BaseColor veya _Color
                    Color col;
                    if (TryGetTintColor(switcher.presets[i], out col))
                    {
                        img.color = col;
                        if (txt) txt.text = "";
                    }
                }
            }

            btn.onClick.AddListener(() => switcher.Apply(idx));
        }
    }

    // --- Utilities ---

    bool TryGetAverageTextureColor(Material mat, out Color avg)
    {
        avg = Color.white;
        if (!mat) return false;

        // Common texture property names
        string[] texProps = { "_BaseMap", "_MainTex", "_BaseColorMap", "_BaseMap0" };
        Texture tex = null;
        foreach (var p in texProps)
        {
            if (mat.HasProperty(p))
            {
                tex = mat.GetTexture(p);
                if (tex) break;
            }
        }
        if (!tex) return false;

        // If readable Texture2D, sample sparsely
        if (tex is Texture2D t2d && t2d.isReadable)
        {
            int w = Mathf.Max(1, t2d.width);
            int h = Mathf.Max(1, t2d.height);
            int gx = Mathf.Clamp(readableSampleGrid, 2, 128);
            int gy = gx;
            int stepX = Mathf.Max(1, w / gx);
            int stepY = Mathf.Max(1, h / gy);

            Color acc = Color.black; int count = 0;
            for (int y = 0; y < h; y += stepY)
            for (int x = 0; x < w; x += stepX)
            { acc += t2d.GetPixel(x, y); count++; }
            if (count == 0) return false;
            avg = acc / count;

            // Apply tint if any
            if (TryGetTintColor(mat, out var tint)) avg *= tint;
            avg.a = 1f;
            return true;
        }

        // Fallback: render texture downsample, then average
        int size = Mathf.Clamp(fallbackRTSize, 8, 128);
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        var small = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        RenderTexture.active = rt;
        small.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        small.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var pix = small.GetPixels32();
        if (pix == null || pix.Length == 0) { Destroy(small); return false; }
        Color acc2 = Color.black; for (int i = 0; i < pix.Length; i++) acc2 += (Color)pix[i];
        avg = acc2 / pix.Length;
        if (TryGetTintColor(mat, out var tint2)) avg *= tint2;
        avg.a = 1f;
        Destroy(small);
        return true;
    }

    bool TryGetTintColor(Material mat, out Color tint)
    {
        tint = Color.white;
        if (!mat) return false;
        if (mat.HasProperty("_BaseColor")) { tint = mat.GetColor("_BaseColor"); return true; }
        if (mat.HasProperty("_Color"))     { tint = mat.GetColor("_Color");     return true; }
        return false;
    }
}