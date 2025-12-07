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
    [SerializeField] private Button unequipButton; // Unequip butonu (Inspector'dan bağlayın)

    [Header("Frame Sprites")]
    [SerializeField] private Sprite emptyFrame;
    [SerializeField] private Sprite equippedFrame;

    [Header("Fallbacks")]


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
    private Coroutine _turnRoutine;
    private enum FaceState { Front, RotatingToBack, Back, RotatingToFront }
    private FaceState _state = FaceState.Front;

    private float _lastActionTime = -999f;
    [SerializeField] private float clickThrottleSec = 0.08f;
    private Quaternion _baseRotation;
    private Vector3 _baseScale;
    private string _itemId;
    public void Bind(string itemId)
    {
        _itemId = IdUtil.NormalizeId(itemId);

        if (string.IsNullOrEmpty(_itemId))
        {
            ApplyEmptyState();
            return;
        }

        var data = ItemDatabaseManager.GetItemData(_itemId);

        string displayName = data?.name ?? _itemId;
        ApplyEquippedState(displayName);
        if (nameLabel != null) nameLabel.text = displayName;

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
                iconImage.gameObject.SetActive(false);
                if (_blinkCoroutine != null)
                {
                    StopCoroutine(_blinkCoroutine);
                    _blinkCoroutine = null;
                }
            }
        }

        ClearStatChips();
        if (data != null) BuildStatChips(data);
    }

    public void ApplyEmptyState()
    {
        if (frameImage) frameImage.sprite = emptyFrame;
        if (iconImage)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }
        if (nameLabel) nameLabel.text = string.Empty;
        ClearStatChips();
    }

    public void ApplyEquippedState(string displayNameForLabel = null)
    {
        if (frameImage) frameImage.sprite = equippedFrame;
        if (iconImage)
        {
            iconImage.gameObject.SetActive(true);
        }
        if (nameLabel) nameLabel.text = string.IsNullOrWhiteSpace(displayNameForLabel) ? nameLabel.text : displayNameForLabel;
        RefreshStatChipsFromItemId(_itemId);
    }


    private void OnValidate()
    {
        if (frameImage == null)
        {
            frameImage = GetComponent<Image>();
            if (frameImage == null)
            {
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

        if (iconImage != null)
            iconImage.preserveAspect = true;

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
        if (frontRoot != null && frontCG == null) frontCG = frontRoot.GetComponent<CanvasGroup>();
        if (backRoot != null && backCG == null) backCG = backRoot.GetComponent<CanvasGroup>();

        if (backRoot != null)
        {
            var lr = backRoot.localRotation.eulerAngles;
            backRoot.localRotation = Quaternion.Euler(lr.x, 180f, lr.z);
        }
        if (chipsOnBack && backRoot != null && chipsContainer != null && !chipsContainer.IsChildOf(backRoot))
        {
            Debug.LogWarning("[EquippedItemDisplay] 'chipsOnBack' aktif ama 'chipsContainer' backRoot altında değil.");
        }
    }

    private bool IsBackVisible()
    {
        if (HasFaces()) return backRoot != null && backRoot.gameObject.activeSelf;
        float y = transform.localRotation.eulerAngles.y;
        y = Mathf.Repeat(y, 360f);
        return Mathf.Abs(Mathf.DeltaAngle(y, 180f)) < 45f;
    }

    public void TurnAround()
    {
        if (Time.unscaledTime - _lastActionTime < clickThrottleSec)
            return;
        _lastActionTime = Time.unscaledTime;

        bool backVisible = IsBackVisible();

        if (backVisible)
        {
            if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
            SnapToCanonicalByCurrentFace();
            _turnRoutine = StartCoroutine(FlipRoutine(frontToBack:false, withAutoReturn:false));
            return;
        }

        if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
        SnapToCanonicalByCurrentFace();
        _turnRoutine = StartCoroutine(FlipRoutine(frontToBack:true, withAutoReturn:true));
    }

    private void SetFaceVisibility(bool showFront)
    {
        if (frontRoot) frontRoot.gameObject.SetActive(showFront);
        if (backRoot) backRoot.gameObject.SetActive(!showFront);

        if (frontCG)
        {
            frontCG.alpha = showFront ? 1f : 1f;
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


        _state = frontToBack ? FaceState.RotatingToBack : FaceState.RotatingToFront;

        Transform target = transform;
        Quaternion startRot = target.localRotation;
        Quaternion endRot   = startRot * Quaternion.Euler(0f, frontToBack ? 180f : -180f, 0f);
        Vector3 startScale = _baseScale;
        Vector3 maxScale   = _baseScale * peakScale;

        float dur = Mathf.Max(0.0001f, turnDuration);
        float t = 0f;
        bool swapped = false;

        if (HasFaces()) SetFaceVisibility(frontToBack ? true : false);

        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);

            if (!swapped && n >= 0.5f && HasFaces())
            {
                SetFaceVisibility(frontToBack ? false : true);
                swapped = true;
            }

            float half = (n <= 0.5f) ? (n / 0.5f) : ((1f - n) / 0.5f);
            float c = Mathf.Clamp01(turnScaleCurve.Evaluate(half));

            target.localRotation = Quaternion.Slerp(startRot, endRot, n);
            target.localScale    = Vector3.Lerp(startScale, maxScale, c);

            yield return null;
        }

        target.localRotation = frontToBack
            ? _baseRotation * Quaternion.Euler(0f, 180f, 0f)
            : _baseRotation;
        target.localScale = _baseScale;
        _state = frontToBack ? FaceState.Back : FaceState.Front;

        if (withAutoReturn && frontToBack)
        {
            yield return new WaitForSeconds(autoReturnDelay);
            SnapToCanonicalByCurrentFace();
            if (this != null)
            {
                float t0 = Time.unscaledTime;
                if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
                _turnRoutine = StartCoroutine(FlipRoutine(frontToBack:false, withAutoReturn:false));
                yield break;
            }
        }


        _turnRoutine = null;
    }
    private void Awake()
    {
        if (HasFaces()) SetFaceVisibility(true);
        _baseRotation = transform.localRotation;
        _baseScale    = transform.localScale;
        
        // Unequip button listener
        if (unequipButton != null)
        {
            unequipButton.onClick.AddListener(OnUnequipClicked);
        }
    }

    private void OnEnable()
    {
        if (HasFaces()) SetFaceVisibility(true);
        SnapToCanonicalByCurrentFace();

    }
    private void SnapToCanonicalByCurrentFace()
    {
        transform.localScale = _baseScale;

        bool backVisible = IsBackVisible();
        transform.localRotation = backVisible
            ? _baseRotation * Quaternion.Euler(0f, 180f, 0f)
            : _baseRotation;

        if (HasFaces()) SetFaceVisibility(!backVisible ? true : false);
    }
    
    private void BuildStatChips(ItemDatabaseManager.ReadableItemData d)
    {
        var s = d.stats;

        AddPercentChipIfNonZero(iconCoinPercent, s.coinMultiplierPercent, "%");
        AddValueChipIfNonZero(iconComboPower, s.comboPower); 
        AddPercentChipIfNonZero(iconGameplaySpeedPercent, s.gameplaySpeedMultiplierPercent, "%");
        AddPercentChipIfNonZero(iconMagnetPercent, s.magnetPowerPercent, "%");
        AddValueChipIfNonZero(iconAcceleration, s.playerAcceleration);
        AddPercentChipIfNonZero(iconPlayerSizePercent, s.playerSizePercent, "%");
        AddValueChipIfNonZero(iconPlayerSpeed, s.playerSpeed);
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
        return (v > 0 ? "+" : "") + v.ToString("0.0");
    }

    private void SpawnChip(Sprite icon, string text)
    {
        if (chipPrefab == null || chipsContainer == null) return;

        var go = Instantiate(chipPrefab, chipsContainer);

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

        if (chipsOnBack && backRoot != null && !chipsContainer.IsChildOf(backRoot))
        {
            Debug.LogWarning("[EquippedItemDisplay] chipsOnBack aktif ama chipsContainer backRoot altında değil.");
        }

        ClearStatChips();

        foreach (var c in chips)
        {
            var go = Instantiate(chipPrefab, chipsContainer);
            Image icon = go.GetComponentInChildren<Image>(true);
            TMP_Text[] texts = go.GetComponentsInChildren<TMP_Text>(true);

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

            TMP_Text labelTxt = texts != null && texts.Length > 0 ? texts[0] : null;
            TMP_Text valueTxt = texts != null && texts.Length > 1 ? texts[1] : null;

            if (labelTxt != null) labelTxt.text = c.label ?? string.Empty;
            if (valueTxt != null) valueTxt.text = c.valueText ?? string.Empty;

            if (labelTxt != null) labelTxt.color = (c.color == default) ? labelTxt.color : c.color;
            if (valueTxt != null) valueTxt.color = (c.color == default) ? valueTxt.color : c.color;
        }
    }

    public async void RefreshStatChipsFromItemId(string itemId)
    {
        var data = ItemDatabaseManager.GetItemData(itemId);
        if (data == null) return;
        await System.Threading.Tasks.Task.Yield();
        if (this == null) return;
        ClearStatChips();
        BuildStatChips(data);
    }
    
    /// <summary>
    /// Unequip button onClick handler
    /// </summary>
    private async void OnUnequipClicked()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            Debug.LogWarning("[EquippedItemDisplay] No item to unequip.");
            return;
        }
        
        // Disable button to prevent double-click
        if (unequipButton != null) unequipButton.interactable = false;
        
        Debug.Log($"[EquippedItemDisplay] Unequipping {_itemId}...");
        
        var manager = UserInventoryManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[EquippedItemDisplay] UserInventoryManager not found!");
            if (unequipButton != null) unequipButton.interactable = true;
            return;
        }
        
        bool success = await manager.UnequipAsync(_itemId);
        
        if (success)
        {
            Debug.Log($"[EquippedItemDisplay] Successfully unequipped {_itemId}");
            
            // Refresh the entire Home Panel (and HUD)
            var homePanel = FindObjectOfType<UIHomePanel>();
            if (homePanel != null)
            {
                homePanel.Initialize();
            }
            // Bind will be refreshed by parent (e.g., UIHomePanel) via OnInventoryChanged event
        }
        else
        {
            Debug.LogWarning($"[EquippedItemDisplay] Failed to unequip {_itemId}");
            if (unequipButton != null) unequipButton.interactable = true;
        }
    }
}