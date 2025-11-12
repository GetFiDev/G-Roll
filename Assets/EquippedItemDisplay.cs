using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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


[Header("Stat Chips (Local)")]
[SerializeField] private RectTransform chipsContainer;  // Chiplerin parent'ı (tercihen Back yüzü altında)
[SerializeField] private GameObject chipPrefab;         // Chip prefabı: Image (opsiyonel) + 2 TMP_Text (label, value)
[SerializeField] private bool chipsOnBack = true;       // Chipler back yüzde gösterilecekse true

[Header("Stat Icons")]
[SerializeField] private Sprite iconCoinPercent;
[SerializeField] private Sprite iconComboPower;
[SerializeField] private Sprite iconGameplaySpeedPercent;
[SerializeField] private Sprite iconMagnetPercent;
[SerializeField] private Sprite iconAcceleration;
[SerializeField] private Sprite iconPlayerSizePercent;
[SerializeField] private Sprite iconPlayerSpeed;

private Coroutine _blinkCoroutine;


    private Coroutine _unequipRoutine;
    private bool _unequipInProgress = false;


    private Coroutine _turnRoutine;

    // State for flip/rotation
    private enum FaceState { Front, RotatingToBack, Back, RotatingToFront }
    private FaceState _state = FaceState.Front;
    private bool _isAnimating = false;
    private float _lastActionTime = -999f;
    [SerializeField] private float clickThrottleSec = 0.08f; // arka arkaya basma koruması için
    private Quaternion _baseRotation;
    private Vector3 _baseScale;

    private string _itemId;


    public void Bind(string itemId)
    {
        _itemId = IdUtil.NormalizeId(itemId);

        // Boş slot ise doğrudan boş state göster
        if (string.IsNullOrEmpty(_itemId))
        {
            ApplyEmptyState();
            return;
        }

        // Shop ile aynı kaynak: ReadableItemData
        var data = ItemDatabaseManager.GetItemData(_itemId);

        // Dolu state + isim
        string displayName = data?.name ?? _itemId;
        ApplyEquippedState(displayName);
        if (nameLabel != null) nameLabel.text = displayName;

        // İkon (Shop mantığı: sadece sprite; yoksa blink)
        if (iconImage != null)
        {
            if (data?.iconSprite != null)
            {
                iconImage.sprite = data.iconSprite;
                var col = iconImage.color; col.a = 1f; iconImage.color = col;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.sprite = null;
                iconImage.gameObject.SetActive(true);
                if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = StartCoroutine(BlinkWhileNoIcon());
            }
        }

        // Stat chip'leri: Shop ile birebir aynı Build akışı
        ClearStatChips();
        if (data != null) BuildStatChips(data);
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
        // Stat chiplerini temizle
        ClearStatChips();
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
        // Stat chiplerini güncelle
        RefreshStatChipsFromItemId(_itemId);
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
        // (showStatsOnBack kaldırıldı)
        if (chipsOnBack && backRoot != null && chipsContainer != null && !chipsContainer.IsChildOf(backRoot))
        {
            Debug.LogWarning("[EquippedItemDisplay] 'chipsOnBack' aktif ama 'chipsContainer' backRoot altında değil.");
        }
    }
    /// <summary>
    /// UI elementini Y ekseninde 180° döndürür (turnDuration sürede),
    /// dönüş sırasında 1.2x büyüyüp geri iner. 5 saniye sonra otomatik eski haline döner.
    /// </summary>
    private bool IsBackVisible()
    {
        if (HasFaces()) return backRoot != null && backRoot.gameObject.activeSelf;
        // Faces yoksa rotasyona göre yaklaşıkla (180'e yakınsa back diyelim)
        float y = transform.localRotation.eulerAngles.y;
        y = Mathf.Repeat(y, 360f);
        return Mathf.Abs(Mathf.DeltaAngle(y, 180f)) < 45f;
    }

    public void TurnAround()
    {
        // Throttle: çok hızlı peşpeşe tıklamaları yut
        if (Time.unscaledTime - _lastActionTime < clickThrottleSec)
            return;
        _lastActionTime = Time.unscaledTime;

        bool backVisible = IsBackVisible();

        if (backVisible)
        {
            // Zaten arka yüz görünüyorsa: beklemeden önceki yüze dön
            if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
            SnapToCanonicalByCurrentFace();
            _turnRoutine = StartCoroutine(FlipRoutine(frontToBack:false, withAutoReturn:false));
            return;
        }

        // Ön yüzdeyken: normal flip + auto return
        if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
        SnapToCanonicalByCurrentFace();
        _turnRoutine = StartCoroutine(FlipRoutine(frontToBack:true, withAutoReturn:true));
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

    private IEnumerator FlipRoutine(bool frontToBack, bool withAutoReturn)
    {
        if (turnScaleCurve == null)
            turnScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        _isAnimating = true;
        _state = frontToBack ? FaceState.RotatingToBack : FaceState.RotatingToFront;

        Transform target = transform;
        Quaternion startRot = target.localRotation;
        Quaternion endRot   = startRot * Quaternion.Euler(0f, frontToBack ? 180f : -180f, 0f);
        Vector3 startScale = _baseScale;
        Vector3 maxScale   = _baseScale * peakScale;

        float dur = Mathf.Max(0.0001f, turnDuration);
        float t = 0f;
        bool swapped = false;

        // Başlangıç yüz ayarı
        if (HasFaces()) SetFaceVisibility(frontToBack ? true : false);

        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);

            // 90° civarı yüz değişimi (direction'a göre)
            if (!swapped && n >= 0.5f && HasFaces())
            {
                SetFaceVisibility(frontToBack ? false : true);
                swapped = true;
            }

            // Tek eğri ile 0..1..0 scale
            float half = (n <= 0.5f) ? (n / 0.5f) : ((1f - n) / 0.5f);
            float c = Mathf.Clamp01(turnScaleCurve.Evaluate(half));

            target.localRotation = Quaternion.Slerp(startRot, endRot, n);
            target.localScale    = Vector3.Lerp(startScale, maxScale, c);

            yield return null;
        }

        // Son değerler
        target.localRotation = frontToBack
            ? _baseRotation * Quaternion.Euler(0f, 180f, 0f)
            : _baseRotation;
        target.localScale = _baseScale;
        _state = frontToBack ? FaceState.Back : FaceState.Front;

        // Otomatik geri dönüş
        if (withAutoReturn && frontToBack)
        {
            yield return new WaitForSeconds(autoReturnDelay);
            SnapToCanonicalByCurrentFace();
            // Eğer bu arada kullanıcı tıkladıysa ve rutin kesildiyse burada olmaz; ama biz yine de güvenlik için kontrol edelim
            if (this != null)
            {
                // return flip
                float t0 = Time.unscaledTime; // throttle'a takılmaması için last time'ı geri al
                if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
                _turnRoutine = StartCoroutine(FlipRoutine(frontToBack:false, withAutoReturn:false));
                yield break;
            }
        }

        _isAnimating = false;
        _turnRoutine = null;
    }
    private void Awake()
    {
        // İlk oluşturulduğunda yalnızca FRONT görünsün
        if (HasFaces()) SetFaceVisibility(true);
        _baseRotation = transform.localRotation;
        _baseScale    = transform.localScale;
    }

    private void OnEnable()
    {
        // Pool'dan geri geldiğinde de garanti altına al
        if (HasFaces()) SetFaceVisibility(true);
        SnapToCanonicalByCurrentFace(); // << ek

    }
    private void SnapToCanonicalByCurrentFace()
    {
        // Ölçeği tabana sabitle
        transform.localScale = _baseScale;

        // Şu an hangi yüz görünüyor ise ona göre kanonik rotasyona çek
        bool backVisible = IsBackVisible();
        transform.localRotation = backVisible
            ? _baseRotation * Quaternion.Euler(0f, 180f, 0f)
            : _baseRotation;

        // Face visibility’yi de garanti altına al
        if (HasFaces()) SetFaceVisibility(!backVisible ? true : false);
    }
    
    // --- SHOP-IDENTICAL STAT CHIP SYSTEM ---
    private void BuildStatChips(ItemDatabaseManager.ReadableItemData d)
    {
        var s = d.stats;

        // Sıfır OLMAYAN statlar gösterilir (Shop ile birebir aynı sıra)
        AddPercentChipIfNonZero(iconCoinPercent, s.coinMultiplierPercent, "%");
        AddValueChipIfNonZero(iconComboPower, s.comboPower); // yüzdelik değil
        AddPercentChipIfNonZero(iconGameplaySpeedPercent, s.gameplaySpeedMultiplierPercent, "%");
        AddPercentChipIfNonZero(iconMagnetPercent, s.magnetPowerPercent, "%");
        AddValueChipIfNonZero(iconAcceleration, s.playerAcceleration); // float
        AddPercentChipIfNonZero(iconPlayerSizePercent, s.playerSizePercent, "%");
        AddValueChipIfNonZero(iconPlayerSpeed, s.playerSpeed); // float
    }

    private void AddPercentChipIfNonZero(Sprite icon, double value, string suffix)
    {
        if (Mathf.Approximately((float)value, 0f)) return;
        string txt = $"{Signed(value)}{suffix}";
        SpawnChip(icon, txt);
    }

    private void AddValueChipIfNonZero(Sprite icon, double value)
    {
        if (Mathf.Approximately((float)value, 0f)) return;
        string txt = $"{Signed(value)}";
        SpawnChip(icon, txt);
    }

    private string Signed(double v)
    {
        // +/− işaretli ve 1 ondalık — Shop ile aynı format
        return (v > 0 ? "+" : "") + v.ToString("0.0");
    }

    private void SpawnChip(Sprite icon, string text)
    {
        if (chipPrefab == null || chipsContainer == null) return;

        var go = Instantiate(chipPrefab, chipsContainer);

        // İlk alt Image ve ilk alt TMP_Text — Shop ile birebir
        var imgs = go.GetComponentsInChildren<Image>(true);
        Image img = null;
        foreach (var i in imgs)
        {
            if (i.gameObject != go) { img = i; break; }
        }

        var tmps = go.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text tmp = null;
        foreach (var t in tmps)
        {
            if (t.gameObject != go) { tmp = t; break; }
        }

        if (img != null) img.sprite = icon;
        if (tmp != null) tmp.text = text;
    }

    private IEnumerator BlinkWhileNoIcon()
    {
        const float speed = 2.5f;
        while (iconImage != null && iconImage.sprite == null)
        {
            float alpha = Mathf.Lerp(0.3f, 0.7f, Mathf.PingPong(Time.unscaledTime * speed, 1f));
            var col = iconImage.color; col.a = alpha; iconImage.color = col;
            yield return null;
        }
        if (iconImage != null)
        {
            var finalCol = iconImage.color; finalCol.a = 1f; iconImage.color = finalCol;
        }
        _blinkCoroutine = null;
    }
    // --- LOCAL STAT CHIP SISTEMI ---
    [System.Serializable]
    public struct StatChipData
    {
        public string label;
        public string valueText;
        public Sprite icon;
        public Color color;
    }

    public void ClearStatChips()
    {
        if (chipsContainer == null) return;
        for (int i = chipsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(chipsContainer.GetChild(i).gameObject);
        }
    }

    public void SetStatChips(IEnumerable<StatChipData> chips)
    {
        if (chipsContainer == null || chipPrefab == null)
        {
            Debug.LogWarning("[EquippedItemDisplay] chipsContainer/chipPrefab eksik.");
            return;
        }

        // Eğer back yüzünde göstermek istiyorsak, hiyerarşi uyarısını yap
        if (chipsOnBack && backRoot != null && !chipsContainer.IsChildOf(backRoot))
        {
            Debug.LogWarning("[EquippedItemDisplay] chipsOnBack aktif ama chipsContainer backRoot altında değil.");
        }

        ClearStatChips();

        foreach (var c in chips)
        {
            var go = Instantiate(chipPrefab, chipsContainer);
            // Chip içindeki bileşenleri bul
            Image icon = go.GetComponentInChildren<Image>(true);
            TMP_Text[] texts = go.GetComponentsInChildren<TMP_Text>(true);

            // Icon
            if (icon != null)
            {
                if (c.icon != null)
                {
                    icon.sprite = c.icon;
                    icon.gameObject.SetActive(true);
                }
                else
                {
                    icon.gameObject.SetActive(false);
                }
            }

            // Label / Value
            TMP_Text labelTxt = texts != null && texts.Length > 0 ? texts[0] : null;
            TMP_Text valueTxt = texts != null && texts.Length > 1 ? texts[1] : null;

            if (labelTxt != null) labelTxt.text = c.label ?? string.Empty;
            if (valueTxt != null) valueTxt.text = c.valueText ?? string.Empty;

            // Renk (opsiyonel)
            if (labelTxt != null) labelTxt.color = (c.color == default) ? labelTxt.color : c.color;
            if (valueTxt != null) valueTxt.color = (c.color == default) ? valueTxt.color : c.color;
        }
    }

    private void RefreshStatChipsFromItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            ClearStatChips();
            return;
        }

        var data = ItemDatabaseManager.GetItemData(itemId);
        ClearStatChips();
        if (data != null) BuildStatChips(data);
    }
    /// <summary>
    /// Bu slottaki item'i unequip eder. Başarılı olursa UI'yi boş slota çeker.
    /// </summary>
    public void UnequipItem()
    {
        if (_unequipInProgress) return;
        if (string.IsNullOrEmpty(_itemId)) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            // Aktif değilken çağrılırsa sessizce geç
            return;
        }
        if (_unequipRoutine != null)
        {
            StopCoroutine(_unequipRoutine);
            _unequipRoutine = null;
        }
        _unequipRoutine = StartCoroutine(UnequipRoutine());
    }

    private IEnumerator UnequipRoutine()
    {
        _unequipInProgress = true;

        var inv = UserInventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[EquippedItemDisplay] Unequip failed: UserInventoryManager.Instance is null.");
            _unequipInProgress = false;
            yield break;
        }

        string nid = IdUtil.NormalizeId(_itemId);
        var task = inv.UnequipAsync(nid);
        while (!task.IsCompleted)
            yield return null;

        if (task.Exception != null || task.Result == false)
        {
            if (task.Exception != null)
                Debug.LogWarning($"[EquippedItemDisplay] Unequip EX for {_itemId}: {task.Exception.Message}");
            else
                Debug.LogWarning($"[EquippedItemDisplay] Unequip failed for {_itemId}");

            _unequipInProgress = false;
            yield break;
        }

        // Başarılı: slotu boşalt ve UI'yı resetle
        _itemId = null;
        ApplyEmptyState();
        SnapToCanonicalByCurrentFace(); // görünümü normalize et

        // Dışarıdaki sistemlerin tazelemesi için istersen mesaj yayınlayabilirsin
        // SendMessage("RefreshVisualState", SendMessageOptions.DontRequireReceiver);

        _unequipInProgress = false;
        _unequipRoutine = null;
        UIPlayerStatsHandler.Instance.Refresh();
    }
}