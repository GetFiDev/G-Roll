using UnityEngine;

/// <summary>
/// ButtonDoorLinkRuntime
/// - Attach to both TriggerPushButton AND TriggerableDoor prefabs.
/// - Manages pairing between buttons and doors with visual LineRenderer.
/// - Similar to TeleportLinkRuntime for portals.
/// </summary>
[DisallowMultipleComponent]
public class ButtonDoorLinkRuntime : MonoBehaviour
{
    [Header("Pairing (Runtime)")]
    [Tooltip("The other end of the pairing (Button or Door).")]
    public ButtonDoorLinkRuntime otherEnd;

    [Tooltip("Pairing ID. Synced with PlacedItemData.linkedButtonDoorId.")]
    public int currentLinkedId;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public float gizmoSphereRadius = 0.15f;

    [Header("Runtime Line (Game View)")]
    public bool drawInGameView = true;
    [Range(0.001f, 0.25f)] public float lineWidth = 0.03f;
    public Color lineColor = new Color(1f, 0.5f, 0.2f, 0.9f);
    
    private LineRenderer _lr;
    private static Material _lineMat;
    private static int _pairSequence = 0;
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
            currentLinkedId = _host.linkedButtonDoorId;
    }

    /// <summary>
    /// Clear pairing flag (set to 0 = unpaired button/door).
    /// </summary>
    public void ClearPairFlag()
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        if (_host == null) return;
        _host.linkedButtonDoorId = 0;
        currentLinkedId = 0;
        otherEnd = null;
        if (_lr != null) _lr.enabled = false;
    }

    /// <summary>
    /// Unpair this button/door (also clears the other end).
    /// </summary>
    public void Unpair()
    {
        var other = otherEnd;
        ClearPairFlag();
        if (other != null) other.ClearPairFlag();
    }

    // ========= PAIRING API =========

    /// <summary>
    /// Pair a button with a door. Both get the same link ID.
    /// </summary>
    public static int AssignPair(ButtonDoorLinkRuntime a, ButtonDoorLinkRuntime b)
    {
        if (a == null || b == null || a == b) return -1;

        // Clear old pairings
        a.ClearPairFlag();
        b.ClearPairFlag();

        // Generate new ID
        int newId = ++_pairSequence;

        // Set both ends
        a.SetLinkedId(newId, b);
        b.SetLinkedId(newId, a);

        return newId;
    }

    private void SetLinkedId(int id, ButtonDoorLinkRuntime counterpart)
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        if (_host == null) return;
        _host.linkedButtonDoorId = id;
        currentLinkedId = id;
        otherEnd = counterpart;
    }

    /// <summary>
    /// Sets the linked ID without setting the counterpart reference.
    /// Used when loading saved maps (counterpart will be linked later by matching IDs).
    /// </summary>
    public void SetLinkedIdOnly(int id)
    {
        if (_host == null) _host = GetComponentInParent<PlacedItemData>();
        if (_host != null) _host.linkedButtonDoorId = id;
        currentLinkedId = id;
    }

    private void EnsureLineRenderer()
    {
        if (_lr != null) return;
        var go = new GameObject("ButtonDoorLinkLine");
        go.transform.SetParent(transform, false);
        _lr = go.AddComponent<LineRenderer>();
        if (_lineMat == null)
        {
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        }
        _lr.material = _lineMat;
        _lr.useWorldSpace = true;
        _lr.positionCount = 2;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.numCapVertices = 4;
        _lr.numCornerVertices = 2;
        _lr.alignment = LineAlignment.View;
        _lr.startColor = lineColor;
        _lr.endColor = lineColor;
        _lr.enabled = false;
    }

    private void LateUpdate()
    {
        if (drawInGameView && otherEnd != null && currentLinkedId > 0)
        {
            EnsureLineRenderer();
            if (_lr != null)
            {
                _lr.startWidth = lineWidth;
                _lr.endWidth = lineWidth;
                _lr.startColor = lineColor;
                _lr.endColor = lineColor;
                _lr.SetPosition(0, transform.position);
                _lr.SetPosition(1, otherEnd.transform.position);
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

        Gizmos.color = lineColor;
        Gizmos.DrawWireSphere(transform.position, gizmoSphereRadius);

        if (otherEnd != null && currentLinkedId > 0)
        {
            Gizmos.DrawLine(transform.position, otherEnd.transform.position);
            var mid = (transform.position + otherEnd.transform.position) * 0.5f;
            UnityEditor.Handles.Label(mid, $"BD-{currentLinkedId}");
        }
        else if (currentLinkedId == 0)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.2f, "Unpaired (0)");
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
