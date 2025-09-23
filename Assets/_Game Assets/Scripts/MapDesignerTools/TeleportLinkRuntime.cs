using UnityEngine;

[DisallowMultipleComponent]
public class TeleportLinkRuntime : MonoBehaviour
{
    [Header("Pair Data")]
    public string channel = "";
    [Range(0,1)] public int endpointIndex = 0;

    [Header("Visuals")]
    public Color linkColor = new Color(0.3f, 1f, 0.8f, 0.9f);
    public float yOffset = 0.25f;
    public float lineWidth = 0.03f;
    [Tooltip("Multiplier for dash frequency (1 = normal)")] public float dashDensity = 3f;
    [Tooltip("Approximate dash length in world units")] public float dashSize = 0.5f;
    [Tooltip("If true, show link whenever this portal has an otherPortal, not only while pairing.")]
    public bool alwaysShowWhenLinked = true;

    public Teleport teleport;
    [HideInInspector] public LineRenderer line;
    [HideInInspector] public TextMesh label;

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
        if (teleport == null || teleport.otherPortal == null)
        {
            HideLink();
            return;
        }

        // keep line and label updated every frame (portals can move)
        var other = teleport.otherPortal;
        EnsureVisuals();
        ShowDashedLine(transform.position + Vector3.up * yOffset,
                       other.transform.position + Vector3.up * yOffset);
        if (label) label.text = string.IsNullOrEmpty(channel) ? "" : $"{channel} [{endpointIndex}]";
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
            // Sprites/Default supports transparency and tiling
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

        if (!label)
        {
            var go = new GameObject("PortalLinkLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * (yOffset + 0.25f);
            label = go.AddComponent<TextMesh>();
            label.fontSize = 32;
            label.characterSize = 0.05f;
            label.alignment = TextAlignment.Center;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = linkColor;
            label.text = "";
        }
    }

    public void ShowLinkTo(TeleportLinkRuntime other)
    {
        EnsureVisuals();
        if (other == null)
        {
            HideLink();
            return;
        }
        var a = transform.position + Vector3.up * yOffset;
        var b = other.transform.position + Vector3.up * yOffset;
        ShowDashedLine(a, b);
        if (label) label.text = string.IsNullOrEmpty(channel) ? "" : $"{channel} [{endpointIndex}]";
    }

    void ShowDashedLine(Vector3 a, Vector3 b)
    {
        if (!line) return;
        line.enabled = true;
        line.SetPosition(0, a);
        line.SetPosition(1, b);

        // tile the dash texture based on world length
        float len = Mathf.Max(0.001f, Vector3.Distance(a, b));
        float tiles = Mathf.Max(1f, (len / Mathf.Max(0.01f, dashSize)) * Mathf.Max(0.01f, dashDensity));
        // use x tiling to repeat dashes along the line
        if (_dashMat && _dashMat.mainTexture)
        {
            _dashMat.mainTextureScale = new Vector2(tiles, 1f);
        }
    }

    void HideLink()
    {
        if (line) line.enabled = false;
        if (label) label.text = "";
    }

    Texture2D GenerateDashTexture()
    {
        // simple 16x2 texture: 8 pixels opaque, 8 pixels transparent → dashed
        int w = 16, h = 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        var cols = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool on = x < w / 2; // first half on, second half off
                cols[y * w + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();
        return tex;
    }
}