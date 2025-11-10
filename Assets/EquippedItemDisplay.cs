using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Reflection;

public class EquippedItemDisplay : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage; // Button'ın arka plan/frame Image'ı (Button component'ındaki Image olabilir)

    [Header("Frame Sprites")]
    [SerializeField] private Sprite emptyFrame;
    [SerializeField] private Sprite equippedFrame;

    [Header("Fallbacks")]
    [SerializeField] private Sprite placeholderIcon;

    [Header("Animation")]
    [SerializeField] private AnimationCurve turnScaleCurve = null; // Inspector'dan ayarlanabilir
    [SerializeField] private float turnDuration = 0.5f;             // Kartın dönüş süresi
    [SerializeField] private float autoReturnDelay = 5f;            // 5 sn sonra geri dönsün
    [SerializeField] private float peakScale = 1.2f;                // Dönüşte 1.2x büyüme

    [Header("Faces (Optional)")]
    [SerializeField] private RectTransform frontRoot;   // Ön yüz GameObject (ikon/isim burada)
    [SerializeField] private RectTransform backRoot;    // Arka yüz GameObject (arka içerikler burada)
    [SerializeField] private CanvasGroup frontCG;       // Raycast kontrolü için (opsiyonel)
    [SerializeField] private CanvasGroup backCG;        // Raycast kontrolü için (opsiyonel)

    private Coroutine _turnRoutine;

    private string _itemId;
    private static readonly Dictionary<string, Sprite> _iconCache = new();
    private string _lastRequestedIconUrl = string.Empty;

    public void Bind(string itemId)
    {
        _itemId = IdUtil.NormalizeId(itemId);

        // Boş slot ise doğrudan boş state göster
        if (string.IsNullOrEmpty(_itemId))
        {
            ApplyEmptyState();
            return;
        }

        // Öncelik: Doğrudan ItemDatabaseManager (reflection yok)
        string displayName = _itemId;
        string iconUrl = string.Empty;
        Sprite iconSprite = null;

        var data = ItemDatabaseManager.GetItemData(_itemId);
        if (data != null)
        {
            displayName = string.IsNullOrWhiteSpace(data.name) ? _itemId : data.name;
            iconSprite = data.iconSprite;
            iconUrl = data.iconUrl ?? string.Empty;
        }
        else
        {
            // Shop DB'de yoksa — fallback olarak ItemLocalDatabase
            if (!TryGetLocalMeta(_itemId, out displayName, out iconUrl))
            {
                Debug.Log($"[EquippedItemDisplay] Using fallback meta for '{_itemId}'");
            }
        }

        // Dolu state: frame ve label
        ApplyEquippedState(displayName);

        if (nameLabel != null) nameLabel.text = displayName;

        // 2) Icon yükle (önce Sprite, yoksa URL)
        if (iconImage != null)
        {
            if (iconSprite != null)
            {
                iconImage.sprite = iconSprite;
            }
            else if (!string.IsNullOrEmpty(iconUrl))
            {
                if (_iconCache.TryGetValue(iconUrl, out var cached))
                {
                    iconImage.sprite = cached ?? placeholderIcon;
                }
                else
                {
                    _lastRequestedIconUrl = iconUrl;
                    StartCoroutine(LoadIcon(iconUrl));
                }
            }
            else
            {
                iconImage.sprite = placeholderIcon;
            }
        }
    }

    /// <summary>UI'yı boş slota göre ayarlar: boş frame, ikon gizli, isim temiz.</summary>
    public void ApplyEmptyState()
    {
        if (frameImage) frameImage.sprite = emptyFrame;
        if (iconImage)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }
        if (nameLabel) nameLabel.text = string.Empty;
    }

    /// <summary>UI'yı dolu slota göre ayarlar: equipped frame, ikon alanı görünür (ikon sonradan gelebilir), isim set edilir.</summary>
    public void ApplyEquippedState(string displayNameForLabel = null)
    {
        if (frameImage) frameImage.sprite = equippedFrame;
        if (iconImage)
        {
            // İkon hemen yoksa bile yer tutucu/placeholder gösterebilirsin; burada aktif edelim
            iconImage.gameObject.SetActive(true);
            // iconImage.sprite'i Bind içinde sprite/URL yüklemesi ile atanacak
        }
        if (nameLabel) nameLabel.text = string.IsNullOrWhiteSpace(displayNameForLabel) ? nameLabel.text : displayNameForLabel;
    }

    // Shop tarafındaki veri modeli: ItemDatabaseManager.ReadableItemData
    // Bu helper, ItemDatabaseManager'dan (static veya instance) ilgili item'ı çekip
    // ad ve Sprite ikon döndürür. UIShopItemDisplay ile aynı kaynaktan beslenmek için.
    private bool TryGetReadableFromItemDatabaseManager(string id, out string displayName, out Sprite iconSprite)
    {
        displayName = id;
        iconSprite = null;

        var normalized = IdUtil.NormalizeId(id);
        var mgrType = System.Type.GetType("ItemDatabaseManager");
        if (mgrType == null) return false;

        // ReadableItemData tipi
        var ridType = mgrType.GetNestedType("ReadableItemData", BindingFlags.Public | BindingFlags.NonPublic);
        if (ridType == null) return false;

        object readable = null;
        bool success = false;

        // Önce static TryGet(string, out ReadableItemData)
        var mi = mgrType.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), ridType.MakeByRefType() }, null);
        if (mi != null)
        {
            var args = new object[] { normalized, null };
            try { success = (bool)mi.Invoke(null, args); if (success) readable = args[1]; } catch { success = false; }
        }

        // Instance üzerinden dene
        if (!success)
        {
            var instProp = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instProp != null ? instProp.GetValue(null) : null;
            if (instance != null)
            {
                // instance TryGet(string, out ReadableItemData)
                mi = mgrType.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), ridType.MakeByRefType() }, null);
                if (mi != null)
                {
                    var args = new object[] { normalized, null };
                    try { success = (bool)mi.Invoke(instance, args); if (success) readable = args[1]; } catch { success = false; }
                }
                
                // instance Get(string) -> ReadableItemData
                if (!success)
                {
                    mi = mgrType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                    if (mi != null)
                    {
                        try { readable = mi.Invoke(instance, new object[] { normalized }); success = readable != null; } catch { success = false; }
                    }
                }
            }
        }

        // static Get(string) -> ReadableItemData
        if (!success)
        {
            mi = mgrType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (mi != null)
            {
                try { readable = mi.Invoke(null, new object[] { normalized }); success = readable != null; } catch { success = false; }
            }
        }

        if (!success || readable == null) return false;

        // ReadableItemData alanlarını oku: name, iconSprite
        string ReadName(object obj)
        {
            var t = obj.GetType();
            var p = t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null && p.PropertyType == typeof(string))
            {
                try { var v = p.GetValue(obj) as string; if (!string.IsNullOrWhiteSpace(v)) return v; } catch { }
            }
            return id;
        }
        Sprite ReadIcon(object obj)
        {
            var t = obj.GetType();
            var p = t.GetProperty("iconSprite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null && typeof(Sprite).IsAssignableFrom(p.PropertyType))
            {
                try { return (Sprite)p.GetValue(obj); } catch { }
            }
            return null;
        }

        displayName = ReadName(readable);
        iconSprite = ReadIcon(readable);
        return true;
    }

    // ItemLocalDatabase'ten meta çekmeye çalışan güvenli adapter.
    // Hem static hem instance pattern'lerini dener. Dönen meta objesinde
    // isim/ikon alanları farklı isimlerde olsa bile akıllı okur.
    private bool TryGetLocalMeta(string id, out string displayName, out string iconUrl)
    {
        displayName = id;
        iconUrl = string.Empty;

        var normalized = IdUtil.NormalizeId(id);
        var dbType = System.Type.GetType("ItemLocalDatabase");
        if (dbType == null)
        {
            Debug.LogWarning("[EquippedItemDisplay] ItemLocalDatabase type not found.");
            return false;
        }

        object meta = null;

        bool TryInvokeTryGet(MethodInfo mi, object target, string key, out object metaOut)
        {
            metaOut = null;
            var parms = mi.GetParameters();
            if (parms.Length == 2 && parms[0].ParameterType == typeof(string) && parms[1].IsOut)
            {
                var args = new object[] { key, null };
                bool ok = false;
                try { ok = (bool)mi.Invoke(target, args); } catch { }
                if (ok) metaOut = args[1];
                return ok;
            }
            return false;
        }

        // 1) static TryGet(string, out T)
        var mi = dbType.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Static);
        if (mi != null && TryInvokeTryGet(mi, null, normalized, out meta)) { }
        else
        {
            // 2) instance TryGet(string, out T)
            var instProp = dbType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instProp != null ? instProp.GetValue(null) : null;
            if (instance != null)
            {
                mi = dbType.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Instance);
                if (mi != null && TryInvokeTryGet(mi, instance, normalized, out meta)) { }
                else
                {
                    // 3) static Get(string) -> T
                    mi = dbType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    if (mi != null)
                    {
                        try { meta = mi.Invoke(null, new object[] { normalized }); } catch { meta = null; }
                    }

                    // 4) instance Get(string) -> T
                    if (meta == null && instance != null)
                    {
                        mi = dbType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                        if (mi != null)
                        {
                            try { meta = mi.Invoke(instance, new object[] { normalized }); } catch { meta = null; }
                        }
                    }
                }
            }
        }

        if (meta == null) return false;

        string ReadStringField(object obj, params string[] fieldNames)
        {
            var t = obj.GetType();
            foreach (var fn in fieldNames)
            {
                var p = t.GetProperty(fn, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null && p.PropertyType == typeof(string))
                {
                    try { var v = p.GetValue(obj) as string; if (!string.IsNullOrWhiteSpace(v)) return v; } catch { }
                }
                var f = t.GetField(fn, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (f != null && f.FieldType == typeof(string))
                {
                    try { var v = f.GetValue(obj) as string; if (!string.IsNullOrWhiteSpace(v)) return v; } catch { }
                }
            }
            return string.Empty;
        }

        var name = ReadStringField(meta, "itemName", "name", "ItemName", "title");
        var icon = ReadStringField(meta, "itemIconUrl", "iconUrl", "pngUrl", "imageUrl", "IconUrl", "PngUrl");

        if (!string.IsNullOrWhiteSpace(name)) displayName = name;
        if (!string.IsNullOrWhiteSpace(icon)) iconUrl = icon;

        return true;
    }

    private IEnumerator LoadIcon(string url)
    {
        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        // Eğer bu arada başka bir ikon istendiyse, bu sonucu yok say
        if (!string.Equals(url, _lastRequestedIconUrl))
            yield break;

#if UNITY_2020_2_OR_NEWER
        if (req.result != UnityWebRequest.Result.Success)
#else
        if (req.isNetworkError || req.isHttpError)
#endif
        {
            Debug.LogWarning($"[EquippedItemDisplay] Icon load failed for '{_itemId}': {req.error}");
            if (iconImage) iconImage.sprite = placeholderIcon;
            yield break;
        }

        var tex = DownloadHandlerTexture.GetContent(req);
        if (tex == null)
        {
            if (iconImage) iconImage.sprite = placeholderIcon;
            yield break;
        }

        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        // cache
        _iconCache[url] = sprite;

        if (string.IsNullOrEmpty(_itemId))
            yield break; // Slot boş duruma dönmüşse ikon basma

        if (iconImage) iconImage.sprite = sprite;
    }

    private void OnValidate()
    {
        if (frameImage == null)
        {
            // Aynı GameObject'te Image ara (genelde Button'ın Image'ı)
            frameImage = GetComponent<Image>();
            if (frameImage == null)
            {
                // Çocuklarda ilk Image'ı bul (ikon değilse daha iyi olur ama en azından boş kalmasın)
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img != iconImage)
                    {
                        frameImage = img;
                        break;
                    }
                }
            }
        }

        // İkonun aspect'ini koru
        if (iconImage != null)
            iconImage.preserveAspect = true;

        // Face auto-wire
        if (frontRoot == null)
        {
            var t = transform.Find("Front");
            if (t) frontRoot = t as RectTransform;
        }
        if (backRoot == null)
        {
            var t = transform.Find("Back");
            if (t) backRoot = t as RectTransform;
        }
        // CanvasGroups auto-wire
        if (frontRoot != null && frontCG == null) frontCG = frontRoot.GetComponent<CanvasGroup>();
        if (backRoot != null && backCG == null) backCG = backRoot.GetComponent<CanvasGroup>();

        // Back yüzeyi parent 180° döndüğünde düz görünmesi için local Y 180° ver
        if (backRoot != null)
        {
            var lr = backRoot.localRotation.eulerAngles;
            // Sadece Y'yi 180'e sabitle
            backRoot.localRotation = Quaternion.Euler(lr.x, 180f, lr.z);
        }
    }
    /// <summary>
    /// UI elementini Y ekseninde 180° döndürür (turnDuration sürede),
    /// dönüş sırasında 1.2x büyüyüp geri iner. 5 saniye sonra otomatik eski haline döner.
    /// </summary>
    public void TurnAround()
    {
        if (_turnRoutine != null)
        {
            StopCoroutine(_turnRoutine);
            _turnRoutine = null;
        }
        _turnRoutine = StartCoroutine(TurnAroundRoutine());
    }

    private void SetFaceVisibility(bool showFront)
    {
        if (frontRoot) frontRoot.gameObject.SetActive(showFront);
        if (backRoot) backRoot.gameObject.SetActive(!showFront);

        // Raycast/Interactable yönetimi (CanvasGroup varsa)
        if (frontCG)
        {
            frontCG.alpha = showFront ? 1f : 1f; // görünürlük görsel olarak aktif GameObject'e bağlı; alpha'yı değiştirmiyoruz
            frontCG.interactable = showFront;
            frontCG.blocksRaycasts = showFront;
        }
        if (backCG)
        {
            backCG.alpha = !showFront ? 1f : 1f;
            backCG.interactable = !showFront;
            backCG.blocksRaycasts = !showFront;
        }
    }

    private bool HasFaces() => frontRoot != null && backRoot != null;

    private IEnumerator TurnAroundRoutine()
    {
        if (turnScaleCurve == null)
            turnScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        Transform target = transform;
        Quaternion startRot = target.localRotation;
        Quaternion midRot = startRot * Quaternion.Euler(0f, 180f, 0f);
        Vector3 startScale = target.localScale;
        Vector3 maxScale = Vector3.one * peakScale;

        float dur = Mathf.Max(0.0001f, turnDuration);
        float t = 0f;

        bool swapped = false;
        // Başlangıçta ön yüz gözüksün
        if (HasFaces()) SetFaceVisibility(true);

        // 0.5 sn içinde 180 derece döndür + büyüt
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            // 90 derecede yüzleri değiştir (tam o anda ekran "ince" olduğundan kesintisiz görünür)
            if (!swapped && n >= 0.5f && HasFaces())
            {
                SetFaceVisibility(false); // arka yüzü aktif et
                swapped = true;
            }
            // Tek eğri ile büyüme -> küçülme: 0..0.5 arası yukarı, 0.5..1 arası aşağı
            float half = (n <= 0.5f) ? (n / 0.5f) : ((1f - n) / 0.5f); // 0..1..0
            float c = Mathf.Clamp01(turnScaleCurve.Evaluate(half));

            target.localRotation = Quaternion.Slerp(startRot, midRot, n);
            target.localScale = Vector3.Lerp(startScale, maxScale, c);
            yield return null;
        }

        // Ortadaki son durum
        target.localRotation = midRot;

        // 5 sn bekle
        yield return new WaitForSeconds(autoReturnDelay);

        // 0.5 sn içinde geri döndür
        t = 0f;
        swapped = false;
        if (HasFaces()) SetFaceVisibility(false); // geri dönüş başlangıcında arka yüz görünüyor
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            if (!swapped && n >= 0.5f && HasFaces())
            {
                SetFaceVisibility(true); // 90 derecede tekrar ön yüze geç
                swapped = true;
            }
            float half = (n <= 0.5f) ? (n / 0.5f) : ((1f - n) / 0.5f); // 0..1..0
            float c = Mathf.Clamp01(turnScaleCurve.Evaluate(half));

            target.localRotation = Quaternion.Slerp(midRot, startRot, n);
            target.localScale = Vector3.Lerp(startScale, maxScale, c);
            yield return null;
        }

        // Eski haline dön
        target.localRotation = startRot;
        target.localScale = startScale;
        _turnRoutine = null;
    }
    private void Awake()
    {
        // İlk oluşturulduğunda yalnızca FRONT görünsün
        if (HasFaces()) SetFaceVisibility(true);
    }

    private void OnEnable()
    {
        // Pool'dan geri geldiğinde de garanti altına al
        if (HasFaces()) SetFaceVisibility(true);
    }
    
}