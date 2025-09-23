using UnityEngine;
using DG.Tweening;

public class Teleport : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private Teleport otherPortal;

    [Header("Vortex In")]
    [SerializeField] private float vortexDuration = 0.5f;          // deliğe giriş süresi
    [SerializeField] private float vortexSpinDegrees = 720f;       // girişte toplam dönüş (Y)
    [SerializeField] private float vortexScaleFactor = 0.85f;      // girişte küçülme
    [SerializeField] private Ease vortexMoveEase = Ease.InCubic;   // giriş hareket eğrisi

    [Header("Exit Jump")]
    [SerializeField] private float exitJumpHeight = 3.0f;          // çıkışta zıplama yüksekliği
    [SerializeField] private bool  alignExitYawToOther = true;     // çıkışta diğer portalın Y rotasyonunu al

    private bool _isTeleporting = false;

    private const float SuspendDuration = 1f;
    private float _suspendedTime;

    public void OnInteract(PlayerController player)
    {
        if (_suspendedTime > Time.time || _isTeleporting || otherPortal == null || player == null)
            return;

        // karşılıklı portal spam'ini engelle
        Suspend();
        otherPortal.Suspend();

        StartVortexTeleport(player);
    }

    private void StartVortexTeleport(PlayerController player)
    {
        var pm = PlayerController.Instance != null ? PlayerController.Instance.playerMovement : null;
        if (pm == null) return;

        Transform t = player.transform;
        Vector3 startScale = t.localScale;
        float   startYaw   = t.eulerAngles.y;

        _isTeleporting = true;

        // Hareket scriptini geçici kapat (çakışan hareketleri engelle)
        bool prevEnabled = pm.enabled;
        pm.enabled = false;

        // Olası aktif tween'leri sonlandır
        t.DOKill(true);

        // --- Single Phase: Spiral path (mevcut konumdan portal merkezine, yarıçap küçülerek) ---
        Vector3 center = transform.position; // portal merkezi

        // Başlangıç yarıçapı ve açı: mevcut pozisyona göre hesapla (kesintisiz başlasın)
        Vector3 fromCenter = t.position - center;
        if (fromCenter.sqrMagnitude < 0.0001f)
            fromCenter = transform.forward * 0.01f; // degişmeyi görünür kılmak için küçük offset

        float startRadius = Mathf.Max(0.01f, fromCenter.magnitude);
        float startAngle  = Mathf.Atan2(fromCenter.z, fromCenter.x); // rad

        const int spiralSegments = 24;
        Vector3[] spiralPoints = new Vector3[spiralSegments + 1];
        for (int i = 0; i <= spiralSegments; i++)
        {
            float t01 = i / (float)spiralSegments;
            float ang = startAngle + Mathf.Deg2Rad * (vortexSpinDegrees * t01);
            float rad = Mathf.Lerp(startRadius, 0f, t01);
            Vector3 off = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad;
            spiralPoints[i] = center + off;
        }
        // İlk nokta karakterin mevcut pozisyonuna eşit (tam kesintisiz)
        spiralPoints[0] = t.position;

        // Sekans kur: tek DOPath + scale join
        Sequence seq = DOTween.Sequence();
        seq.Append(
            t.DOPath(spiralPoints, vortexDuration, PathType.CatmullRom)
             .SetOptions(closePath: false)
             .SetEase(vortexMoveEase)
        );
        seq.Join(t.DOScale(startScale * vortexScaleFactor, vortexDuration));

        // Bittiğinde diğer portala ışınla ve zıplat
        seq.OnComplete(() =>
        {
            t.position = otherPortal.transform.position;
            if (alignExitYawToOther)
                t.rotation = Quaternion.Euler(0f, otherPortal.transform.eulerAngles.y, 0f);
            else
                t.rotation = Quaternion.Euler(0f, startYaw, 0f);

            t.localScale = startScale;

            pm.enabled = prevEnabled;
            pm.Jump(exitJumpHeight);

            _isTeleporting = false;
        });
    }

    private void Suspend()
    {
        _suspendedTime = Time.time + SuspendDuration;
    }
}