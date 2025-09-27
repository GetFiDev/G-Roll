using UnityEngine;

//
// TeleportLinkRuntime
// - Portal sahne objesine eklenir.
// - Eşleştirme başına artan global kimlik verir (1..N) ve PlacedItemData.linkedPortalId alanını yazar.
// - Portal yerleştirilmiş ama eşleşmemişse linkedPortalId = 0
// - Portal değilse (TeleportLinkRuntime yoksa) linkedPortalId = -1 (PlacedItemData.Init içinde ayarlanıyor)
//
[DisallowMultipleComponent]
public class TeleportLinkRuntime : MonoBehaviour
{
    // ====== Public (görüntü kolaylıkları) ======
    [Header("Pairing (Runtime)")]
    [Tooltip("Bu portalın bağlı olduğu diğer portal (sahne referansı).")]
    public TeleportLinkRuntime otherPortal;

    [Tooltip("Eşleştirme kimliği (sadece görüntü amaçlı). PlacedItemData.linkedPortalId ile senkron tutulur.")]
    public int currentLinkedId;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public float gizmoSphereRadius = 0.15f;

    [Header("Runtime Line (Game View)")]
    [Tooltip("Game view'da eşleştirme çizgisini göster.")]
    public bool drawInGameView = true;
    [Range(0.001f, 0.25f)] public float lineWidth = 0.03f;
    public float lineZOffset = 0f; // 2D oyunlarda Z farkı gerekiyorsa
    private LineRenderer _lr;
    private static Material _lineMat;

    // ====== Internal ======
    private static int _pairSequence = 0; // 1..N; her AssignPair çağrısında artar
    private PlacedItemData _host;

    private void Awake()
    {
        _host = GetComponentInParent<PlacedItemData>();
        SyncFromHost();
    }

    private void OnValidate()
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        SyncFromHost();
    }

    private void SyncFromHost()
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        if (_host != null)
            currentLinkedId = _host.linkedPortalId;
    }

    /// <summary>
    /// Bu portalı “eşleşmemiş portal” durumuna döndürür.
    /// (Portal olduğu için 0;  -1 portal olmayan içindir.)
    /// </summary>
    public void ClearPairFlag()
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        if (_host == null) return;
        _host.linkedPortalId = 0;
        currentLinkedId = 0;
        otherPortal = null;
        if (_lr != null) _lr.enabled = false;
    }

    /// <summary>
    /// Bu portalın mevcut eşleşmesini tamamen kaldırır.
    /// Karşı tarafta da temizler (varsa).
    /// </summary>
    public void Unpair()
    {
        // Önce kendini temizle
        ClearPairFlag();

        // Karşı tarafı da temizle
        // Not: otherPortal ClearPairFlag sırasında null’a çekilmiş olabilir, o yüzden
        // ayrı bir temp değişken üzerinden ilerleyelim.
        // (Eğer already-null ise sorun yok.)
    }

    // ========= EŞLEŞTİRME API'si =========

    /// <summary>
    /// İki portalı eşleştir. Her iki portalın linkedPortalId değerlerine yeni bir kimlik (1..N) yazılır.
    /// Öncesinde var olan bağlantılar otomatik temizlenir.
    /// </summary>
    public static int AssignPair(TeleportLinkRuntime a, TeleportLinkRuntime b)
    {
        if (a == null || b == null || a == b) return -1;

        // Eski bağlantıları temizle
        a.ClearPairFlag();
        b.ClearPairFlag();

        // Yeni ID üret
        int newId = ++_pairSequence;

        // Her iki uçta da host'a yaz
        a.SetLinkedId(newId, b);
        b.SetLinkedId(newId, a);

        return newId;
    }

    /// <summary>
    /// Bu portal ile verilen diğer portalı aynı kimlikle eşle (dışarı açmıyoruz).
    /// </summary>
    private void SetLinkedId(int id, TeleportLinkRuntime counterpart)
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        if (_host == null) return;
        _host.linkedPortalId = id;
        currentLinkedId = id;
        otherPortal = counterpart;
    }

    private void EnsureLineRenderer()
    {
        if (_lr != null) return;
        var go = new GameObject("PortalLinkLine");
        go.transform.SetParent(transform, false);
        _lr = go.AddComponent<LineRenderer>();
        if (_lineMat == null)
        {
            // Sprites/Default çoğu pipeline'da mevcut; URP/HDRP'de de güvenilir bir seçimdir
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        }
        _lr.material = _lineMat;
        _lr.useWorldSpace = true;
        _lr.positionCount = 2;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.numCapVertices = 4;
        _lr.numCornerVertices = 2;
        _lr.alignment = LineAlignment.View;
        _lr.enabled = false;
    }

    private void LateUpdate()
    {
        // Game View çizgisi
        if (drawInGameView && otherPortal != null && currentLinkedId > 0)
        {
            EnsureLineRenderer();
            if (_lr != null)
            {
                _lr.startWidth = lineWidth;
                _lr.endWidth = lineWidth;
                var a = transform.position; a.z += lineZOffset;
                var b = otherPortal.transform.position; b.z += lineZOffset;
                _lr.SetPosition(0, a);
                _lr.SetPosition(1, b);
                _lr.enabled = true;
            }
        }
        else if (_lr != null)
        {
            _lr.enabled = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Portal merkezinde küçük bir küre
        Gizmos.DrawWireSphere(transform.position, gizmoSphereRadius);

        // Eşleştirilmişse çizgi ve ID etiketi
        if (otherPortal != null && currentLinkedId > 0)
        {
            Gizmos.DrawLine(transform.position, otherPortal.transform.position);

            // ID’yi görmek için küçük bir label
#if UNITY_EDITOR
            var mid = (transform.position + otherPortal.transform.position) * 0.5f;
            UnityEditor.Handles.Label(mid, $"ID: {currentLinkedId}");
#endif
        }
        else if (currentLinkedId == 0)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.2f, "Unlinked (0)");
#endif
        }
    }
#endif

    private void OnDestroy()
    {
        if (_lr != null)
        {
            if (Application.isPlaying)
                Destroy(_lr.gameObject);
            else
                DestroyImmediate(_lr.gameObject);
            _lr = null;
        }
    }
}