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

    [Header("Fallbacks")]
    [SerializeField] private Sprite placeholderIcon;

    private string _itemId;
    private static readonly Dictionary<string, Sprite> _iconCache = new();

    public void Bind(string itemId)
    {
        _itemId = IdUtil.NormalizeId(itemId);

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
                    StartCoroutine(LoadIcon(iconUrl));
                }
            }
            else
            {
                iconImage.sprite = placeholderIcon;
            }
        }
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

        if (iconImage) iconImage.sprite = sprite;
    }
}