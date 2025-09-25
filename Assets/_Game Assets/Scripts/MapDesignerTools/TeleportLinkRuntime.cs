using UnityEngine;

[DisallowMultipleComponent]
public class TeleportLinkRuntime : MonoBehaviour
{
    [Header("Pair Visual Settings")]
    public Color linkColor = new Color(0.3f, 1f, 0.8f, 0.9f);
    public float yOffset = 0.25f;
    public float lineWidth = 0.03f;
    [Tooltip("Multiplier for dash frequency (1 = normal)")] public float dashDensity = 3f;
    [Tooltip("Approximate dash length in world units")] public float dashSize = 0.5f;
    [Tooltip("If true, draw line whenever this portal has an otherPortal.")]
    public bool alwaysShowWhenLinked = true;

    [Header("Refs")]
    public Teleport teleport; // diğer uç için teleport.otherPortal kullanılacak

    [HideInInspector] public LineRenderer line;

    Texture2D _dashTex;
    Material _dashMat;

    void Awake()
    {
        if (!teleport) teleport = GetComponent<Teleport>();
        EnsureVisuals();
    }

    void Update()
    {
        if (!alwaysShowWhenLinked) return;
        if (!teleport || !teleport.otherPortal) { HideLink(); return; }

        EnsureVisuals();
        var a = transform.position + Vector3.up * yOffset;
        var b = teleport.otherPortal.transform.position + Vector3.up * yOffset;
        ShowDashedLine(a, b);
    }

    public void EnsureVisuals()
    {
        if (!_dashTex)
        {
            _dashTex = GenerateDashTexture();
            _dashTex.wrapMode = TextureWrapMode.Repeat;
            _dashTex.filterMode = FilterMode.Bilinear;
        }
        if (!_dashMat)
        {
            _dashMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            _dashMat.mainTexture = _dashTex;
            _dashMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _dashMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _dashMat.SetInt("_ZWrite", 0);
            _dashMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        }
        if (!line)
        {
            var go = new GameObject("PortalLinkLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = _dashMat;
            line.textureMode = LineTextureMode.Tile;
            line.widthMultiplier = lineWidth;
            line.numCornerVertices = 2;
            line.positionCount = 2;
            line.startColor = line.endColor = linkColor;
            line.enabled = false;
        }
        else
        {
            line.material = _dashMat;
            line.textureMode = LineTextureMode.Tile;
            line.widthMultiplier = lineWidth;
            line.startColor = line.endColor = linkColor;
        }
    }

    void ShowDashedLine(Vector3 a, Vector3 b)
    {
        if (!line) return;
        line.enabled = true;
        line.SetPosition(0, a);
        line.SetPosition(1, b);

        float len = Mathf.Max(0.001f, Vector3.Distance(a, b));
        float tiles = Mathf.Max(1f, (len / Mathf.Max(0.01f, dashSize)) * Mathf.Max(0.01f, dashDensity));
        if (_dashMat && _dashMat.mainTexture)
            _dashMat.mainTextureScale = new Vector2(tiles, 1f);
    }

    void HideLink()
    {
        if (line) line.enabled = false;
    }

    Texture2D GenerateDashTexture()
    {
        int w = 16, h = 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        var cols = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool on = x < w / 2;
                cols[y * w + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();
        return tex;
    }
}