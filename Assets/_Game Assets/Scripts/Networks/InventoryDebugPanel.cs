// InventoryDebugPanel.cs
// Sahneye boş bir GameObject ekleyip bu scripti at. Play modda butona bas.
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

public class InventoryDebugPanel : MonoBehaviour
{
    [Header("Options")]
    [Tooltip("Server'dan taze snapshot çekmeden önce logla")]
    public bool logBeforeRefresh = true;

    [Tooltip("Loga sahip olunmayan (owned=false) itemları da dahil et")]
    public bool includeNotOwned = false;

#if ODIN_INSPECTOR
    [Button(ButtonSizes.Large), GUIColor(0.2f, 0.7f, 1f)]
#endif
    [ContextMenu("Log Inventory (Owned/Equipped/Qty)")]
    public void LogInventory()
    {
        var invMgr = FindFirstObjectByType<UserInventoryManager>();
        if (invMgr == null)
        {
            Debug.LogWarning("[InventoryDebug] UserInventoryManager bulunamadı.");
            return;
        }

        Dump(invMgr);
    }

#if ODIN_INSPECTOR
    [Button(ButtonSizes.Large), GUIColor(0.3f, 1f, 0.3f)]
#endif
    [ContextMenu("Refresh From Server, Then Log")]
    public async void RefreshThenLog()
    {
        var invMgr = FindFirstObjectByType<UserInventoryManager>();
        if (invMgr == null)
        {
            Debug.LogWarning("[InventoryDebug] UserInventoryManager bulunamadı.");
            return;
        }

        if (logBeforeRefresh)
            Dump(invMgr, tag: "BEFORE_REFRESH");

        await invMgr.RefreshAsync();
        Dump(invMgr, tag: "AFTER_REFRESH");
    }

    private void Dump(UserInventoryManager invMgr, string tag = "NOW")
    {
        // UserInventoryManager iç yapısına erişim public API ile yapılacak
        // Owns/IsEquipped ile ve (mümkünse) internal dictionary'yi reflection ile değil, güvenli yoldan loglayalım.
        // Ancak owned item setini çıkarmak için InventoryRemoteService tarafında item listesi lazımsa,
        // minimum olarak ShopItemManager veya lokal DB’den id’leri çekebilirsin.
        // Burada güvenli varsayım: ItemLocalDatabase.Load() bize tüm item id’lerini veriyor (normalize ederek kullanalım).

        var local = ItemLocalDatabase.Load(); // case-insensitive dict’e çevrilmişti
        var ids = local?.Keys?.ToList() ?? new List<string>();

        var sb = new StringBuilder();
        sb.AppendLine($"[InventoryDebug::{tag}] totalKnownItems={ids.Count}");

        int ownedCount = 0, equippedCount = 0, notOwnedCount = 0;

        foreach (var rawId in ids.OrderBy(x => x))
        {
            var id = IdUtil.NormalizeId(rawId);
            bool owned = invMgr.IsOwned(id);
            bool equipped = invMgr.IsEquipped(id);

            if (owned) ownedCount++;
            if (equipped) equippedCount++;
            if (!owned) notOwnedCount++;

            if (!owned && !includeNotOwned) continue;

            // quantity bilgisini almak için InventoryRemoteService tarafındaki entry’ye ihtiyacımız var.
            // UserInventoryManager'da public bir erişim yoksa, quantity’yi “?” olarak göstereceğiz.
            // Eğer public bir getter varsa (ör: TryGetEntry) orayı kullan.
            int? qty = TryGetQuantity(invMgr, id);

            sb.AppendLine($" - {id}  | owned={(owned ? "1" : "0")}  equipped={(equipped ? "1" : "0")}  qty={(qty.HasValue ? qty.Value.ToString() : "?")}");
        }

        sb.AppendLine($"[InventoryDebug::{tag}] owned={ownedCount} equipped={equippedCount} notOwned={notOwnedCount}");
        Debug.Log(sb.ToString());
    }

    // UserInventoryManager içindeki quantity'e erişim için küçük bir yardımcı (public API yoksa ? döner)
    private int? TryGetQuantity(UserInventoryManager invMgr, string id)
    {
        // Eğer UserInventoryManager içinde public bir method yoksa, quantity'yi güvenli biçimde atlayalım.
        // İleride ihtiyaç olursa, UserInventoryManager'a:
        //   public bool TryGetEntry(string id, out InventoryRemoteService.InventoryEntry e)
        // eklenebilir ve burada kullanılır.
        return null;
    }
}